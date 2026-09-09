using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SokolApplicationBuilder
{
    // Builds the extra MSBuild `-p:` arguments that inject the SokolObfuscator target into a
    // publish/build. Obfuscation engages only when ALL hold (docs/OBFUSCATION_TOOL_DESIGN.md §8.4):
    //   1. it was requested — EITHER --obfuscate was passed OR the project's Directory.Build.props
    //      declares <Obfuscation>true</Obfuscation> (a committed, per-project opt-in), AND
    //   2. it is a release build, AND
    //   3. the project folder contains an obfuscate.xml (supplies the config).
    // Otherwise this returns "" and the build is unchanged.
    public static class ObfuscationInjection
    {
        // Appended verbatim to the dotnet publish/build command (shell or CliWrap). Leading space included.
        public static string BuildArgs(Options opts, string projectPath, string buildType, TaskLoggingHelper log)
        {
            bool viaProps = ProjectDeclaresObfuscation(projectPath);
            if (!opts.Obfuscate && !viaProps) return "";

            bool isRelease = string.Equals(buildType, "Release", StringComparison.OrdinalIgnoreCase)
                             || opts.Type == "release" || opts.Type == "release-harness";
            if (!isRelease)
            {
                log.LogMessage(MessageImportance.High, "ℹ️  Obfuscation ignored: it runs on release builds only.");
                return "";
            }

            // Gate + config: --obfuscation-config override, else <project>/obfuscate.xml.
            string config = !string.IsNullOrEmpty(opts.ObfuscationConfig)
                ? Path.GetFullPath(opts.ObfuscationConfig)
                : FindObfuscateXml(projectPath);
            if (string.IsNullOrEmpty(config) || !File.Exists(config))
            {
                log.LogWarning("--obfuscate was set but no obfuscate.xml was found in the project folder — skipping obfuscation.");
                return "";
            }

            string home = ResolveSokolNetHome(log);
            if (string.IsNullOrEmpty(home)) return "";

            string toolDir = Path.Combine(home, "tools", "SokolObfuscator");
            string toolDll = Path.Combine(toolDir, "bin", "Release", "net10.0", "SokolObfuscator.dll");
            string targets = Path.Combine(toolDir, "Sokol.Obfuscation.targets");

            if (!File.Exists(targets))
            {
                log.LogError($"Obfuscation targets not found: {targets}");
                return "";
            }
            if (!File.Exists(toolDll) && !BuildTool(toolDir, log))
                return "";

            string trigger = opts.Obfuscate ? "--obfuscate" : "<Obfuscation> in Directory.Build.props";
            log.LogMessage(MessageImportance.High, $"🔒 Obfuscation ENABLED ({trigger}) — config: {config}");
            return $" -p:SokolObfuscate=true -p:SokolObfuscatorTool=\"{toolDll}\""
                 + $" -p:SokolObfuscationConfig=\"{config}\""
                 + $" -p:CustomAfterMicrosoftCommonTargets=\"{targets}\""
                 + $" -p:DebugType=portable -p:DebugSymbols=true";
        }

        // A project turns obfuscation on WITHOUT the --obfuscate flag by declaring
        // <Obfuscation>true</Obfuscation> in its Directory.Build.props — the same file the other
        // per-project options live in — so obfuscation becomes a committed, per-project decision
        // rather than something every build command must remember to pass. Namespace-agnostic (the
        // examples' Directory.Build.props has no xmlns); mirrors DesktopAppBuilder's props reading.
        // obfuscate.xml is still required for the config (§8.4).
        static bool ProjectDeclaresObfuscation(string projectPath)
        {
            string props = Path.Combine(projectPath, "Directory.Build.props");
            if (!File.Exists(props)) return false;
            try
            {
                var doc = XDocument.Load(props);
                var val = doc.Descendants("Obfuscation").FirstOrDefault()?.Value?.Trim();
                return bool.TryParse(val, out var b) && b;
            }
            catch
            {
                return false; // malformed props: fall back to the --obfuscate flag only
            }
        }

        static string FindObfuscateXml(string projectPath)
        {
            string here = Path.Combine(projectPath, "obfuscate.xml");
            if (File.Exists(here)) return Path.GetFullPath(here);
            // Web builds run from a subfolder; also check the parent example dir.
            string? parent = Path.GetDirectoryName(Path.GetFullPath(projectPath));
            if (parent != null)
            {
                string up = Path.Combine(parent, "obfuscate.xml");
                if (File.Exists(up)) return up;
            }
            return "";
        }

        static string ResolveSokolNetHome(TaskLoggingHelper log)
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(homeDir) || !Directory.Exists(homeDir))
                homeDir = Environment.GetEnvironmentVariable("HOME") ?? "";

            string file = Path.Combine(homeDir, ".sokolnet_config", "sokolnet_home");
            if (!File.Exists(file))
            {
                log.LogError("SokolNetHome configuration not found — run the 'register' task first.");
                return "";
            }
            return Path.GetFullPath(File.ReadAllText(file).Trim());
        }

        static bool BuildTool(string toolDir, TaskLoggingHelper log)
        {
            string csproj = Path.Combine(toolDir, "SokolObfuscator.csproj");
            log.LogMessage(MessageImportance.High, "🔧 Building SokolObfuscator (first use)…");
            (int exitCode, _) = Utils.RunShellCommand(
                log,
                $"dotnet build \"{csproj}\" -c Release -v quiet",
                new Dictionary<string, string>(),
                workingDir: toolDir,
                logStdErrAsMessage: true,
                debugMessageImportance: MessageImportance.High,
                label: "build-obfuscator");
            if (exitCode != 0) log.LogError("Failed to build SokolObfuscator.");
            return exitCode == 0;
        }
    }
}
