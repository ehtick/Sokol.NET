using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SokolObfuscator
{
    public enum RuleKind { Namespace, Type, Method, File, Folder }

    // One include/exclude rule for METHOD selection. Path kinds (File/Folder) match a method's
    // PDB source path; the others match metadata names. See docs/OBFUSCATION_TOOL_DESIGN.md §7.3.
    public sealed class ScopeRule
    {
        public RuleKind Kind;
        public bool Include;
        public Regex Regex = null!;
        public string Raw = "";
        public bool IsPath => Kind == RuleKind.File || Kind == RuleKind.Folder;
    }

    // What the scope matcher needs to know about one method.
    public readonly struct MethodContext
    {
        public readonly string Namespace;
        public readonly string TypeFullName;
        public readonly string MethodKey;   // "Ns.Type::Name"
        public readonly string? SourceFile;  // from PDB, or null
        public MethodContext(string ns, string typeFull, string methodKey, string? src)
        { Namespace = ns; TypeFullName = typeFull; MethodKey = methodKey; SourceFile = src; }
    }

    public sealed class ObfuscationConfig
    {
        public bool RenameMethods = true;
        public bool EncryptStrings = false;
        public bool RenameTypesFieldsProperties = false; // Phase 3, opt-in (higher reflection/serialization risk)
        public int? Seed;
        public int IdentifierLength = 10;
        public bool EmitSymbolMap = true;

        // Method-selection rules (renaming AND which methods' string literals are eligible).
        public readonly List<ScopeRule> DefaultExcludes = new();
        public readonly List<ScopeRule> XmlIncludes = new();
        public readonly List<ScopeRule> XmlExcludes = new();
        public readonly List<ScopeRule> CliIncludes = new();
        public readonly List<ScopeRule> CliExcludes = new();

        // STRING-VALUE excludes: a literal whose VALUE matches any of these is NEVER encrypted
        // (e.g. AdMob ids, API keys, config identifiers the user wants readable). §6.2.
        public readonly List<Regex> StringExcludePatterns = new();

        public static ObfuscationConfig CreateDefault()
        {
            var c = new ObfuscationConfig();
            c.DefaultExcludes.Add(Rule(RuleKind.Namespace, false, "Sokol*"));
            c.DefaultExcludes.Add(Rule(RuleKind.Namespace, false, "Imgui*"));
            c.DefaultExcludes.Add(Rule(RuleKind.Namespace, false, "JoltPhysicsSharp*"));
            c.DefaultExcludes.Add(Rule(RuleKind.Namespace, false, "Ozz*"));
            c.DefaultExcludes.Add(Rule(RuleKind.Folder, false, "**/src/**"));
            // Never encrypt AdMob identifiers (owner requirement) — kept readable if they appear in code.
            c.StringExcludePatterns.Add(CompileValue("ca-app-pub-*", regex: false));
            return c;
        }

        public static ObfuscationConfig Load(string? xmlPath, IEnumerable<string> cliIncludes, IEnumerable<string> cliExcludes)
        {
            var c = CreateDefault();
            if (!string.IsNullOrEmpty(xmlPath) && File.Exists(xmlPath))
                c.ParseXml(xmlPath!);
            foreach (var s in cliIncludes) if (!c.TryAddStringRule(s)) c.CliIncludes.Add(ParseKindPattern(s, true));
            foreach (var s in cliExcludes) if (!c.TryAddStringRule(s)) c.CliExcludes.Add(ParseKindPattern(s, false));
            return c;
        }

        // Method scope (renaming + string-encryption eligibility). Precedence (last wins, §7.3):
        // default-exclude → xml-include → xml-exclude → cli-include → cli-exclude. Safety (§7.2) is
        // applied by the caller on top.
        public bool InScope(in MethodContext ctx)
        {
            bool keep = true;
            foreach (var r in DefaultExcludes) if (Matches(r, ctx)) keep = false;
            foreach (var r in XmlIncludes) if (Matches(r, ctx)) keep = true;
            foreach (var r in XmlExcludes) if (Matches(r, ctx)) keep = false;
            foreach (var r in CliIncludes) if (Matches(r, ctx)) keep = true;
            foreach (var r in CliExcludes) if (Matches(r, ctx)) keep = false;
            return keep;
        }

        // A string literal is encryptable if it's non-trivial and not value-excluded.
        public bool ShouldEncryptString(string? v)
        {
            if (string.IsNullOrEmpty(v) || v!.Length < 2) return false;
            foreach (var r in StringExcludePatterns) if (r.IsMatch(v)) return false;
            return true;
        }

        static bool Matches(ScopeRule r, in MethodContext ctx) => r.Kind switch
        {
            RuleKind.Namespace => r.Regex.IsMatch(ctx.Namespace),
            RuleKind.Type => r.Regex.IsMatch(ctx.TypeFullName),
            RuleKind.Method => r.Regex.IsMatch(ctx.MethodKey),
            RuleKind.File or RuleKind.Folder => ctx.SourceFile != null && r.Regex.IsMatch(ctx.SourceFile),
            _ => false
        };

        void ParseXml(string path)
        {
            var doc = XDocument.Load(path);
            var root = doc.Root;
            if (root == null) return;

            var settings = root.Element("Settings");
            if (settings != null)
            {
                RenameMethods = ReadBool(settings.Element("RenameMethods"), RenameMethods);
                EmitSymbolMap = ReadBool(settings.Element("EmitSymbolMap"), EmitSymbolMap);
                var se = settings.Element("StringEncryption")?.Value?.Trim().ToLowerInvariant();
                if (se != null) EncryptStrings = se.Length > 0 && se != "off" && se != "false" && se != "none";
                // <RenameTypesFieldsProperties> accepts on/off (or true/false); default off (§6.3).
                var rt = settings.Element("RenameTypesFieldsProperties")?.Value?.Trim().ToLowerInvariant();
                if (rt != null) RenameTypesFieldsProperties = rt.Length > 0 && rt != "off" && rt != "false" && rt != "none";
                var seed = settings.Element("Seed")?.Value?.Trim();
                if (!string.IsNullOrEmpty(seed) && int.TryParse(seed, out var s)) Seed = s;
                var len = settings.Element("IdentifierLength")?.Value?.Trim();
                if (!string.IsNullOrEmpty(len) && int.TryParse(len, out var l)) IdentifierLength = l;
            }

            var rules = root.Element("Rules");
            if (rules != null)
            {
                foreach (var e in rules.Elements())
                {
                    bool include = e.Name.LocalName.Equals("Include", StringComparison.OrdinalIgnoreCase);
                    bool exclude = e.Name.LocalName.Equals("Exclude", StringComparison.OrdinalIgnoreCase);
                    if (!include && !exclude) continue;
                    string kindStr = (e.Attribute("kind")?.Value ?? "namespace").Trim().ToLowerInvariant();
                    if (kindStr == "string" || kindStr == "string-regex")
                    {
                        StringExcludePatterns.Add(CompileValue(e.Value.Trim(), regex: kindStr == "string-regex"));
                        continue;
                    }
                    var rule = Rule(ParseKind(kindStr), include, e.Value.Trim());
                    (include ? XmlIncludes : XmlExcludes).Add(rule);
                }
            }
        }

        static bool ReadBool(XElement? e, bool dflt)
            => e != null && bool.TryParse(e.Value.Trim(), out var b) ? b : dflt;

        // "string:pat" / "string-regex:pat" CLI excludes route to StringExcludePatterns.
        bool TryAddStringRule(string s)
        {
            int i = s.IndexOf(':');
            if (i < 0) return false;
            string kind = s[..i].Trim().ToLowerInvariant();
            if (kind != "string" && kind != "string-regex") return false;
            StringExcludePatterns.Add(CompileValue(s[(i + 1)..], regex: kind == "string-regex"));
            return true;
        }

        // "kind:pattern" for CLI method rules, e.g. "namespace:MyGame.Net.*" or "folder:Source/Wire/**".
        static ScopeRule ParseKindPattern(string s, bool include)
        {
            int i = s.IndexOf(':');
            if (i < 0) return Rule(RuleKind.Namespace, include, s);
            return Rule(ParseKind(s[..i]), include, s[(i + 1)..]);
        }

        static RuleKind ParseKind(string k) => k.Trim().ToLowerInvariant() switch
        {
            "type" => RuleKind.Type,
            "method" => RuleKind.Method,
            "file" => RuleKind.File,
            "folder" => RuleKind.Folder,
            _ => RuleKind.Namespace
        };

        static ScopeRule Rule(RuleKind kind, bool include, string pattern)
            => new() { Kind = kind, Include = include, Raw = pattern, Regex = Compile(kind, pattern) };

        static Regex CompileValue(string pattern, bool regex)
            => regex ? new Regex(pattern, RegexOptions.Compiled) : new Regex(PlainGlob(pattern), RegexOptions.Compiled);

        // Name globs: '*' matches anything (incl. dots), '?' one char. Path globs: '/'-aware,
        // '**' spans separators, '*' does not; a relative pattern matches as a path suffix.
        static Regex Compile(RuleKind kind, string pattern)
        {
            bool path = kind is RuleKind.File or RuleKind.Folder;
            if (!path) return new Regex(PlainGlob(pattern), RegexOptions.Compiled);

            var sb = new StringBuilder("^");
            pattern = pattern.Replace('\\', '/');
            if (!pattern.StartsWith("/") && !pattern.StartsWith("**")) sb.Append("(.*/)?");
            for (int i = 0; i < pattern.Length; i++)
            {
                char c = pattern[i];
                if (c == '*' && i + 1 < pattern.Length && pattern[i + 1] == '*') { sb.Append(".*"); i++; }
                else if (c == '*') sb.Append("[^/]*");
                else if (c == '?') sb.Append('.');
                else sb.Append(Regex.Escape(c.ToString()));
            }
            sb.Append('$');
            return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.Compiled); // macOS paths are case-insensitive
        }

        static string PlainGlob(string pattern)
        {
            var sb = new StringBuilder("^");
            foreach (char c in pattern)
            {
                if (c == '*') sb.Append(".*");
                else if (c == '?') sb.Append('.');
                else sb.Append(Regex.Escape(c.ToString()));
            }
            sb.Append('$');
            return sb.ToString();
        }
    }
}
