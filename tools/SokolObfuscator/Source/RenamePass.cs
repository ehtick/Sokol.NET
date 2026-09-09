using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;

namespace SokolObfuscator
{
    // Phase 3 (opt-in via <RenameTypesFieldsProperties> / --rename-types): rename TYPES (and collapse
    // the namespace of top-level types), FIELDS, and PROPERTIES (together with their get_/set_
    // accessors) to random identifiers.
    //
    // Like method renaming this is TOKEN-safe — IL references types/fields/methods by metadata token,
    // so every in-module reference updates automatically when a *Def's .Name changes. The only hazards
    // are therefore (a) names bound to an EXTERNAL contract — handled by the same safety exclusions the
    // method pass uses (entry points, P/Invoke, [UnmanagedCallersOnly]/JS interop, external
    // vtable/interface slots) — and (b) reflection / serialization BY NAME (Type.GetType("Ns.T"),
    // GetProperty("Score"), System.Text.Json keys), which cannot be inferred statically. (b) is the
    // reason this whole pass is off by default and must be opted into per project, and why:
    //   * enum members and the enum value__ field are never renamed (Enum.Parse / ToString),
    //   * events are left untouched,
    //   * anything [Obfuscation(Exclude)] / [DynamicDependency] and any out-of-scope (framework) type
    //     is skipped.
    // See docs/OBFUSCATION_TOOL_DESIGN.md §6.3 / §7.
    public sealed class RenamePass
    {
        const string AttrUnmanagedCallersOnly = "System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute";
        const string AttrLibraryImport        = "System.Runtime.InteropServices.LibraryImportAttribute";
        const string AttrJSExport             = "System.Runtime.InteropServices.JavaScript.JSExportAttribute";
        const string AttrJSImport             = "System.Runtime.InteropServices.JavaScript.JSImportAttribute";
        const string AttrDynamicDependency    = "System.Diagnostics.CodeAnalysis.DynamicDependencyAttribute";
        const string AttrObfuscation          = "System.Reflection.ObfuscationAttribute";
        const string InjectedNamespace        = "SokolObfuscator"; // our __Strings__ / __Obfuscated__ helpers

        static readonly HashSet<string> HardEntryPointNames = new(StringComparer.Ordinal)
        {
            "Main", "AndroidMain", "IOSMain"
        };

        readonly ObfuscationConfig _config;
        readonly NameGenerator _gen;
        readonly Dictionary<string, string> _map;
        readonly bool _verbose;

        public int Types { get; private set; }
        public int Fields { get; private set; }
        public int Properties { get; private set; }

        public RenamePass(ObfuscationConfig config, NameGenerator gen, Dictionary<string, string> map, bool verbose)
        {
            _config = config; _gen = gen; _map = map; _verbose = verbose;
        }

        public void Run(ModuleDefMD module)
        {
            var entry = module.EntryPoint;
            var types = module.GetTypes().ToList(); // snapshot; we only mutate names, not the collection

            // ---- members first (fields + properties), while type names/namespaces are still intact ----
            foreach (var t in types)
            {
                if (!TypeInScope(t)) continue;

                foreach (var p in t.Properties.ToList())
                {
                    if (!CanRenameProperty(p)) continue;
                    string r = _gen.Next();
                    Record(p.FullName, r);
                    if (_verbose) Console.WriteLine($"  property   {p.Name,-36} -> {r}   ({t.FullName})");
                    // Rename the accessors consistently so the original name doesn't survive in get_/set_.
                    if (p.GetMethod != null) { Record(p.GetMethod.FullName, "get_" + r); p.GetMethod.Name = "get_" + r; }
                    if (p.SetMethod != null) { Record(p.SetMethod.FullName, "set_" + r); p.SetMethod.Name = "set_" + r; }
                    p.Name = r;
                    Properties++;
                }

                foreach (var f in t.Fields.ToList())
                {
                    if (!CanRenameField(f)) continue;
                    string r = _gen.Next();
                    Record(f.FullName, r);
                    if (_verbose) Console.WriteLine($"  field      {f.Name,-36} -> {r}   ({t.FullName})");
                    f.Name = r;
                    Fields++;
                }
            }

            // ---- types last. Decide the whole set BEFORE mutating any namespace, so a nested type's
            //      scope (which resolves through its enclosing type's namespace) is judged on the
            //      original names, not on a parent we already collapsed. ----
            var toRename = types.Where(t => CanRenameType(t, entry)).ToList();
            foreach (var t in toRename)
            {
                string r = _gen.Next();
                Record(t.FullName, r);
                if (_verbose) Console.WriteLine($"  type       {t.FullName,-40} -> {r}");
                // Namespace only exists on top-level types; nested types inherit it from the encloser.
                if (t.DeclaringType == null && !UTF8String.IsNullOrEmpty(t.Namespace))
                    t.Namespace = UTF8String.Empty; // the random name alone is now opaque
                t.Name = r;
                Types++;
            }
        }

        void Record(string oldFullName, string newName) => _map[oldFullName] = newName;

        // A type is a candidate for MEMBER (field/property) renaming when it is in config scope and is
        // not one of our injected helpers or the module's <Module> type. Members are token-safe even in
        // interop/startup types, so those are NOT excluded here (only their TYPE NAME is, below).
        bool TypeInScope(TypeDef t)
        {
            if (t.IsGlobalModuleType) return false;
            if (t.Namespace == InjectedNamespace) return false;
            return _config.InScope(TypeContext(t));
        }

        bool CanRenameType(TypeDef t, MethodDef? entry)
        {
            if (!TypeInScope(t)) return false;
            if (IsObfuscationExcluded(t)) return false;
            if (DeclaresExternalAbi(t, entry)) return false; // keep startup/interop TYPE names intact
            return true;
        }

        bool CanRenameField(FieldDef f)
        {
            if (f.DeclaringType.IsEnum) return false;            // enum members + value__ (Enum.Parse/ToString)
            if (IsObfuscationExcluded(f)) return false;
            if (HasAttr(f, AttrDynamicDependency)) return false;
            return true;
        }

        bool CanRenameProperty(PropertyDef p)
        {
            if (IsObfuscationExcluded(p)) return false;
            var accessors = new[] { p.GetMethod, p.SetMethod };
            if (accessors.All(a => a == null)) return false;
            foreach (var a in accessors)
            {
                if (a == null) continue;
                // An accessor bound to an external vtable/interface slot can't have its name changed;
                // interop/reflection-marked accessors are off-limits too.
                if (a.IsVirtual || a.IsAbstract || a.HasOverrides) return false;
                if (a.HasImplMap) return false;
                if (IsObfuscationExcluded(a)) return false;
                if (HasAttr(a, AttrUnmanagedCallersOnly) || HasAttr(a, AttrLibraryImport)
                    || HasAttr(a, AttrJSExport) || HasAttr(a, AttrJSImport)
                    || HasAttr(a, AttrDynamicDependency)) return false;
            }
            return true;
        }

        bool DeclaresExternalAbi(TypeDef t, MethodDef? entry)
        {
            foreach (var m in t.Methods)
            {
                if (entry != null && m == entry) return true;
                if (HardEntryPointNames.Contains(m.Name)) return true;
                if (m.HasImplMap) return true; // P/Invoke [DllImport]
                if (HasAttr(m, AttrUnmanagedCallersOnly) || HasAttr(m, AttrLibraryImport)
                    || HasAttr(m, AttrJSExport) || HasAttr(m, AttrJSImport)) return true;
            }
            return false;
        }

        MethodContext TypeContext(TypeDef t)
        {
            string tf = t.FullName;
            return new MethodContext(Obfuscator.EffectiveNamespace(t), tf, tf + "::", SourceFileOf(t));
        }

        static string? SourceFileOf(TypeDef t)
        {
            foreach (var m in t.Methods)
            {
                if (!m.HasBody) continue;
                foreach (var instr in m.Body.Instructions)
                    if (instr.SequencePoint?.Document != null)
                        return instr.SequencePoint.Document.Url;
            }
            return null;
        }

        static bool HasAttr(IHasCustomAttribute m, string fullName)
        {
            foreach (var ca in m.CustomAttributes)
                if (ca.AttributeType?.FullName == fullName)
                    return true;
            return false;
        }

        static bool IsObfuscationExcluded(IHasCustomAttribute m)
        {
            foreach (var ca in m.CustomAttributes)
            {
                if (ca.AttributeType?.FullName != AttrObfuscation) continue;
                foreach (var na in ca.Properties)
                    if (na.Name == "Exclude" && na.Argument.Value is bool b)
                        return b;
                return true; // present, Exclude defaults to true
            }
            return false;
        }
    }
}
