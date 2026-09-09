using System.Collections.Generic;
using CommandLine;

namespace SokolObfuscator
{
    // CLI surface. Mirrors SokolApplicationBuilder's CommandLineParser conventions.
    // Later phases add: --string-encryption, --rename-types, --control-flow.
    // See docs/OBFUSCATION_TOOL_DESIGN.md.
    public class ObfuscationOptions
    {
        [Option("input", Required = true, HelpText = "Path to the managed assembly (.dll) to process.")]
        public string Input { get; set; } = "";

        [Option("output", Required = false, HelpText = "Output path. Default: overwrite --input in place.")]
        public string Output { get; set; } = "";

        [Option("config", Required = false, HelpText = "Path to obfuscate.xml. Optional; sensible defaults apply without it.")]
        public string Config { get; set; } = "";

        [Option("include", Required = false, HelpText = "Scope include rule 'kind:pattern' (kind = namespace|type|method|file|folder). Repeatable.")]
        public IEnumerable<string> Include { get; set; } = new List<string>();

        [Option("exclude", Required = false, HelpText = "Scope exclude rule 'kind:pattern' (kind also: string|string-regex to keep a literal plaintext). Repeatable.")]
        public IEnumerable<string> Exclude { get; set; } = new List<string>();

        [Option("string-encryption", Required = false, HelpText = "Encrypt string literals: on|off (overrides obfuscate.xml <StringEncryption>).")]
        public string StringEncryption { get; set; } = "";

        [Option("rename-types", Required = false, HelpText = "Rename types/fields/properties: on|off (overrides obfuscate.xml <RenameTypesFieldsProperties>). Off by default — higher reflection/serialization risk.")]
        public string RenameTypes { get; set; } = "";

        [Option("map", Required = false, HelpText = "Write an original->obfuscated symbol map (JSON) to this path.")]
        public string Map { get; set; } = "";

        [Option("seed", Required = false, HelpText = "Integer seed for reproducible name generation. Omit for random.")]
        public int? Seed { get; set; }

        [Option("rid", Required = false, HelpText = "Runtime identifier (informational in Phase 0).")]
        public string Rid { get; set; } = "";

        [Option("dry-run", Required = false, HelpText = "Analyze and report the exclusion set; do NOT modify the assembly.")]
        public bool DryRun { get; set; }

        [Option("verbose", Required = false, HelpText = "Verbose per-member logging.")]
        public bool Verbose { get; set; }
    }
}
