using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace SokolObfuscator
{
    // Phase 1 engine: load a managed assembly with dnlib, compute the safety/exclusion set (§7.2),
    // apply config-driven namespace/folder scoping (§7.1/§7.3), then (unless --dry-run) rename the
    // remaining methods to random identifiers and write back.
    //
    // NOTE (why in-module rename is reference-safe): IL references methods by metadata TOKEN,
    // not by name. Renaming a MethodDef keeps every in-module caller and every ldftn/ldvirtftn
    // delegate/function-pointer target working automatically. The ONLY things a rename can break
    // are (a) names that must match an EXTERNAL ABI (entry points, P/Invoke EntryPoint,
    // [UnmanagedCallersOnly] exports, overrides of external virtuals/interfaces) and
    // (b) reflection-by-name — (a) is the safety set below, (b) is handled by scope config.
    public sealed class Obfuscator
    {
        // Non-overridable startup ABI. The native launchers resolve these by name/exported
        // symbol; renaming any of them => the app fails to start. (design doc §7.2)
        static readonly HashSet<string> HardEntryPointNames = new(StringComparer.Ordinal)
        {
            "Main", "AndroidMain", "IOSMain"
        };

        const string AttrUnmanagedCallersOnly = "System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute";
        const string AttrLibraryImport        = "System.Runtime.InteropServices.LibraryImportAttribute";
        const string AttrModuleInitializer    = "System.Runtime.CompilerServices.ModuleInitializerAttribute";
        const string AttrJSExport             = "System.Runtime.InteropServices.JavaScript.JSExportAttribute";
        const string AttrJSImport             = "System.Runtime.InteropServices.JavaScript.JSImportAttribute";
        const string AttrDynamicDependency    = "System.Diagnostics.CodeAnalysis.DynamicDependencyAttribute";
        const string AttrObfuscation          = "System.Reflection.ObfuscationAttribute";

        readonly ObfuscationOptions _opts;

        public Obfuscator(ObfuscationOptions opts) => _opts = opts;

        public int Run()
        {
            if (!File.Exists(_opts.Input))
            {
                Console.Error.WriteLine($"ERROR: input not found: {_opts.Input}");
                return 1;
            }

            var config = ObfuscationConfig.Load(
                string.IsNullOrEmpty(_opts.Config) ? null : _opts.Config,
                _opts.Include, _opts.Exclude);

            // CLI --string-encryption overrides the config's <StringEncryption>.
            string se = _opts.StringEncryption.Trim().ToLowerInvariant();
            if (se.Length > 0) config.EncryptStrings = se != "off" && se != "false" && se != "none";

            // CLI --rename-types overrides the config's <RenameTypesFieldsProperties>.
            string rt = _opts.RenameTypes.Trim().ToLowerInvariant();
            if (rt.Length > 0) config.RenameTypesFieldsProperties = rt != "off" && rt != "false" && rt != "none";

            // Load from a byte[] so the file is not memory-mapped/locked and we can write back in place.
            var module = ModuleDefMD.Load(File.ReadAllBytes(_opts.Input));

            // Idempotency: a `dotnet publish` can run the build pipeline more than once (notably the
            // wasm publish), firing the injected AfterTargets="Compile" step twice on the SAME assembly.
            // Without this guard the second pass re-renames already-renamed methods and corrupts the
            // symbol map. Skip if our marker is already present.
            if (IsAlreadyObfuscated(module))
            {
                Console.WriteLine($"ℹ️  {module.Name} already obfuscated (marker present) — skipping (idempotent).");
                return 0;
            }

            bool pdbLoaded = TryLoadPdb(module, _opts.Input);
            var entryPoint = module.EntryPoint; // may be null for a BuildAsLibrary (Android/iOS) assembly

            var renamable = new List<MethodDef>();
            var safetyExcluded = new List<(MethodDef md, string reason)>();
            int scopeExcluded = 0;

            foreach (var md in module.GetTypes().SelectMany(t => t.Methods))
            {
                var reason = ExclusionReason(md, module, entryPoint);
                if (reason != null) { safetyExcluded.Add((md, reason)); continue; }
                if (!config.InScope(Context(md))) { scopeExcluded++; continue; }
                renamable.Add(md);
            }

            Report(module, pdbLoaded, renamable, safetyExcluded, scopeExcluded);

            if (_opts.DryRun)
            {
                Console.WriteLine("\n--dry-run: no changes written.");
                return 0;
            }

            if (!config.RenameMethods && !config.EncryptStrings && !config.RenameTypesFieldsProperties)
            {
                Console.WriteLine("\nNothing to do (RenameMethods, StringEncryption and RenameTypesFieldsProperties all off).");
                return 0;
            }

            // One name generator shared across the method pass and the Phase 3 pass so every emitted
            // identifier is globally unique and reproducible from the single seed.
            var gen = new NameGenerator(_opts.Seed ?? config.Seed, config.IdentifierLength);

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (config.RenameMethods)
            {
                foreach (var md in renamable)
                {
                    string oldFull = md.FullName;
                    string newName = gen.Next();
                    if (_opts.Verbose)
                        Console.WriteLine($"  rename  {md.Name,-40} -> {newName}   ({md.DeclaringType.FullName})");
                    md.Name = newName;
                    map[oldFull] = newName;
                }
            }

            // String encryption scopes on the ORIGINAL type namespaces/names, so it must run before the
            // Phase 3 type rename collapses them.
            int encrypted = 0;
            if (config.EncryptStrings)
                encrypted = new StringEncryptor(_opts.Seed ?? config.Seed).Encrypt(module, config, Context, _opts.Verbose);

            RenamePass? phase3 = null;
            if (config.RenameTypesFieldsProperties)
            {
                phase3 = new RenamePass(config, gen, map, _opts.Verbose);
                phase3.Run(module);
            }

            string output = string.IsNullOrEmpty(_opts.Output) ? _opts.Input : _opts.Output;
            AddObfuscationMarker(module); // stamp so a repeated pass skips (see IsAlreadyObfuscated)
            // Don't emit a (now-stale) PDB alongside the rewritten assembly.
            module.Write(output, new ModuleWriterOptions(module) { WritePdb = false });
            string phase3Summary = phase3 != null
                ? $" + {phase3.Types} type(s)/{phase3.Fields} field(s)/{phase3.Properties} propert{(phase3.Properties == 1 ? "y" : "ies")}"
                : "";
            Console.WriteLine($"\n✅ Wrote {renamable.Count} renamed method(s){phase3Summary} + {encrypted} encrypted string(s) to {output}");

            string mapPath = _opts.Map;
            if (config.EmitSymbolMap && !string.IsNullOrEmpty(mapPath) && map.Count > 0)
            {
                File.WriteAllText(mapPath, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"🗺️  Symbol map: {mapPath}");
            }

            return 0;
        }

        internal static MethodContext Context(MethodDef md)
        {
            var dt = md.DeclaringType;
            string tf = dt?.FullName ?? "";
            // Nested/compiler-generated types (e.g. lambda display classes) carry an EMPTY
            // Namespace — their namespace belongs to the OUTERMOST enclosing type. Resolve it so
            // namespace scope rules (e.g. framework "Sokol*") also exclude framework closures.
            return new MethodContext(EffectiveNamespace(dt), tf, tf + "::" + md.Name, GetSourceFile(md));
        }

        internal static string EffectiveNamespace(TypeDef? t)
        {
            if (t == null) return "";
            while (t.DeclaringType != null) t = t.DeclaringType;
            return t.Namespace?.String ?? "";
        }

        // Returns null if the method may be renamed (safety-wise), otherwise an exclusion reason.
        string? ExclusionReason(MethodDef md, ModuleDef module, MethodDef? entryPoint)
        {
            if (md.DeclaringType == module.GlobalType) return "<Module> method";
            if (entryPoint != null && md == entryPoint) return "assembly entry point";
            if (HardEntryPointNames.Contains(md.Name)) return "startup ABI (Main/AndroidMain/IOSMain)";

            if (md.IsRuntimeSpecialName || md.IsSpecialName) return "special-name (ctor/accessor/operator)";
            if (md.HasImplMap) return "P/Invoke [DllImport]";
            if (md.IsVirtual || md.IsAbstract) return "virtual/abstract (vtable slot)";
            if (md.HasOverrides) return "explicit interface/override impl";

            if (HasAttr(md, AttrUnmanagedCallersOnly)) return "[UnmanagedCallersOnly] native export";
            if (HasAttr(md, AttrLibraryImport)) return "[LibraryImport]";
            if (HasAttr(md, AttrModuleInitializer)) return "[ModuleInitializer]";
            if (HasAttr(md, AttrJSExport)) return "[JSExport]";
            if (HasAttr(md, AttrJSImport)) return "[JSImport]";
            if (HasAttr(md, AttrDynamicDependency)) return "[DynamicDependency]";
            if (IsObfuscationExcluded(md)) return "[Obfuscation(Exclude)]";

            return null;
        }

        static bool HasAttr(MethodDef md, string fullName)
        {
            foreach (var ca in md.CustomAttributes)
                if (ca.AttributeType?.FullName == fullName)
                    return true;
            return false;
        }

        // [Obfuscation] excludes when Exclude is true; Exclude defaults to true when the
        // attribute is present without the named argument.
        static bool IsObfuscationExcluded(MethodDef md)
        {
            foreach (var ca in md.CustomAttributes)
            {
                if (ca.AttributeType?.FullName != AttrObfuscation) continue;
                foreach (var na in ca.Properties)
                    if (na.Name == "Exclude" && na.Argument.Value is bool b)
                        return b;
                return true; // present, Exclude defaults to true
            }
            return false;
        }

        static string? GetSourceFile(MethodDef md)
        {
            if (!md.HasBody) return null;
            foreach (var instr in md.Body.Instructions)
            {
                var sp = instr.SequencePoint;
                if (sp?.Document != null) return sp.Document.Url;
            }
            return null;
        }

        static bool TryLoadPdb(ModuleDefMD module, string inputPath)
        {
            try
            {
                var pdb = Path.ChangeExtension(inputPath, ".pdb");
                if (File.Exists(pdb)) { module.LoadPdb(File.ReadAllBytes(pdb)); return true; }
                module.LoadPdb(); // embedded PDB, if present
                return module.PdbState != null;
            }
            catch { return false; } // PDB is optional; File/Folder rules simply won't match
        }

        const string MarkerNamespace = "SokolObfuscator";
        const string MarkerTypeName = "__Obfuscated__";

        static bool IsAlreadyObfuscated(ModuleDef m)
        {
            foreach (var t in m.Types)
                if (t.Name == MarkerTypeName && t.Namespace == MarkerNamespace)
                    return true;
            return false;
        }

        static void AddObfuscationMarker(ModuleDef m)
        {
            var marker = new TypeDefUser(MarkerNamespace, MarkerTypeName, m.CorLibTypes.Object.TypeDefOrRef);
            marker.Attributes = dnlib.DotNet.TypeAttributes.NotPublic | dnlib.DotNet.TypeAttributes.Sealed
                              | dnlib.DotNet.TypeAttributes.Abstract | dnlib.DotNet.TypeAttributes.Class;
            m.Types.Add(marker);
        }

        void Report(ModuleDef module, bool pdbLoaded, List<MethodDef> renamable,
                    List<(MethodDef md, string reason)> safetyExcluded, int scopeExcluded)
        {
            int total = renamable.Count + safetyExcluded.Count + scopeExcluded;
            Console.WriteLine($"Assembly : {module.Assembly?.FullName ?? module.Name}");
            Console.WriteLine($"Types    : {module.GetTypes().Count()}");
            Console.WriteLine($"PDB      : {(pdbLoaded ? "loaded (file/folder rules active)" : "not found (file/folder rules inert)")}");
            Console.WriteLine($"Methods  : {total}  →  renamable {renamable.Count} / safety-excluded {safetyExcluded.Count} / scope-excluded {scopeExcluded}");

            Console.WriteLine("\nStartup ABI check:");
            foreach (var name in HardEntryPointNames)
            {
                var hits = safetyExcluded.Where(e => e.md.Name == name).ToList();
                if (hits.Count == 0)
                    Console.WriteLine($"  {name,-12} : not present in this assembly");
                else
                    foreach (var (md, reason) in hits)
                        Console.WriteLine($"  {name,-12} : EXCLUDED ✓  ({reason})  [{md.DeclaringType.Name}]");
            }

            Console.WriteLine("\nSafety exclusion reasons:");
            foreach (var g in safetyExcluded.GroupBy(e => e.reason).OrderByDescending(g => g.Count()))
                Console.WriteLine($"  {g.Count(),5}  {g.Key}");

            if (renamable.Count > 0)
            {
                Console.WriteLine("\nIn-scope namespaces (renamable methods):");
                foreach (var g in renamable.GroupBy(m => { string ns = (string)m.DeclaringType.Namespace ?? ""; return ns.Length == 0 ? "<global>" : ns; })
                                           .OrderByDescending(g => g.Count()))
                    Console.WriteLine($"  {g.Count(),5}  {g.Key}");
            }
        }
    }
}
