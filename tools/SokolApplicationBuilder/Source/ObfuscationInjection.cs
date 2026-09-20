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
            // An explicit --no-obfuscate wins over everything, including a committed
            // <Obfuscation>true</Obfuscation>. Without this there is no way to get an
            // un-obfuscated build out of a project that has opted in, short of editing its
            // Directory.Build.props — which is exactly what you do NOT want to be doing when you
            // are chasing a crash and need the real method names in the stack trace.
            if (opts.NoObfuscate)
            {
                log.LogMessage(MessageImportance.High, "\U0001F513 Obfuscation OFF for this build (--no-obfuscate).");
                PurgeObfuscatedIntermediates(projectPath, log);
                return "";
            }

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
        /// <summary>
        /// Delete intermediate assemblies left behind by a PREVIOUS obfuscated build.
        ///
        /// ⛔ Without this, --no-obfuscate is a silent no-op. The obfuscation seam stamps the
        /// intermediate assembly (SokolObfuscator.__Obfuscated__), and the force-recompile target
        /// that clears it lives in Sokol.Obfuscation.targets — which is only injected when we ARE
        /// obfuscating. So a --no-obfuscate build straight after an obfuscated one finds the
        /// intermediate "up to date", reuses the stamped assembly, and ships obfuscated code while
        /// the log cheerfully reports that obfuscation is off. Device-observed 2026-09-20.
        ///
        /// The obfuscator writes obfuscation.map.json beside each assembly it rewrites, so that file
        /// is a precise marker: drop the assemblies next to it and MSBuild recompiles those, and only
        /// those, from source.
        /// </summary>
        static void PurgeObfuscatedIntermediates(string projectPath, TaskLoggingHelper log)
        {
            // The app's own obj, plus the obj of every project it <ProjectReference>s — a referenced
            // project's intermediate lives OUTSIDE the app folder (e.g. a vendored engine under ext/),
            // and if it stays stamped its obfuscated types link straight into the "clean" binary.
            var objRoots = new List<string> { Path.Combine(projectPath, "obj") };
            foreach (string referenced in EnumerateProjectReferenceDirs(projectPath))
                objRoots.Add(Path.Combine(referenced, "obj"));

            int cleaned = 0;
            foreach (string objRoot in objRoots.Distinct())
            {
            if (!Directory.Exists(objRoot)) continue;
            foreach (string map in Directory.EnumerateFiles(objRoot, "obfuscation.map.json", SearchOption.AllDirectories))
            {
                string dir = Path.GetDirectoryName(map) ?? "";
                try
                {
                    foreach (string stale in Directory.EnumerateFiles(dir, "*.dll")
                                                      .Concat(Directory.EnumerateFiles(dir, "*.pdb")))
                        File.Delete(stale);
                    File.Delete(map);
                    cleaned++;
                }
                catch (Exception ex)
                {
                    log.LogWarning($"Could not clear a previously obfuscated intermediate in {dir}: {ex.Message}");
                }
            }
            }

            if (cleaned > 0)
                log.LogMessage(MessageImportance.High,
                    $"   cleared {cleaned} intermediate(s) stamped by an earlier obfuscated build, so this one recompiles clean");
        }

        /// <summary>Directories of the projects this project &lt;ProjectReference&gt;s, resolved through
        /// $(SokolNetHome) where the reference uses it. Best-effort: anything unreadable is skipped.</summary>
        static IEnumerable<string> EnumerateProjectReferenceDirs(string projectPath)
        {
            string home = "";
            try
            {
                string cfg = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".sokolnet_config", "sokolnet_home");
                if (File.Exists(cfg)) home = File.ReadAllText(cfg).Trim();
            }
            catch { }

            foreach (string csproj in SafeEnumerate(projectPath, "*.csproj"))
            {
                XDocument doc;
                try { doc = XDocument.Load(csproj); } catch { continue; }

                foreach (var pr in doc.Descendants("ProjectReference"))
                {
                    string include = pr.Attribute("Include")?.Value;
                    if (string.IsNullOrWhiteSpace(include)) continue;

                    include = include.Replace("$(SokolNetHome)", home).Replace('\\', Path.DirectorySeparatorChar);
                    string full;
                    try { full = Path.GetFullPath(Path.Combine(projectPath, include)); } catch { continue; }

                    string dir = Path.GetDirectoryName(full) ?? "";
                    if (dir.Length > 0 && Directory.Exists(dir)) yield return dir;
                }
            }
        }

        static IEnumerable<string> SafeEnumerate(string dir, string pattern)
        {
            try { return Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly); }
            catch { return Array.Empty<string>(); }
        }

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
