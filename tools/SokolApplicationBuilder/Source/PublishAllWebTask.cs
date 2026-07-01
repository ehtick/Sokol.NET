// Copyright (c) 2022 Eli Aloni (a.k.a  elix22)
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;

namespace SokolApplicationBuilder
{
    public class PublishAllWebTask : Task
    {
        Options opts;
        public PublishAllWebTask(Options opts)
        {
            this.opts = opts;
        }

        public override bool Execute()
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(homeDir) || !Directory.Exists(homeDir))
                homeDir = Environment.GetEnvironmentVariable("HOME") ?? "";

            string sokolNetHomeFile = Path.Combine(homeDir, ".sokolnet_config", "sokolnet_home");
            if (!File.Exists(sokolNetHomeFile))
            {
                Log.LogError("ERROR: SokolNetHome configuration not found. Please run 'register' task first.");
                return false;
            }

            string sokolNetHome = File.ReadAllText(sokolNetHomeFile).Trim();
            string examplesPath = Path.Combine(sokolNetHome, "examples");

            if (!Directory.Exists(examplesPath))
            {
                Log.LogError($"Examples folder not found: {examplesPath}");
                return false;
            }

            var failedProjects = new System.Collections.Generic.List<string>();
            int published = 0;

            foreach (string exampleDir in Directory.GetDirectories(examplesPath))
            {
                string exampleName = Path.GetFileName(exampleDir);
                string webCsproj = Path.Combine(exampleDir, $"{exampleName}Web.csproj");

                if (!File.Exists(webCsproj))
                    continue;

                Log.LogMessage(MessageImportance.High, $"Publishing {exampleName}Web ...");

                (int exitCode, string output) = Utils.RunShellCommand(Log,
                    $"dotnet publish -c Release \"{exampleName}Web.csproj\"",
                    null,
                    workingDir: exampleDir,
                    logStdErrAsMessage: true,
                    debugMessageImportance: MessageImportance.High,
                    label: $"publish {exampleName}Web");

                if (exitCode != 0)
                {
                    Log.LogError($"Failed to publish {exampleName}Web");
                    failedProjects.Add($"{exampleName}Web");
                }
                else
                {
                    Log.LogMessage(MessageImportance.High, $"Published {exampleName}Web successfully.");
                    published++;
                }

                // Remove the SDK-resolver backup temp files the wasm build leaves next to the csproj.
                Utils.CleanupSdkResolverTempFiles(exampleDir);
            }

            Log.LogMessage(MessageImportance.High, $"Publish all web completed: {published} succeeded, {failedProjects.Count} failed.");
            if (failedProjects.Count > 0)
                Log.LogError($"Failed projects:{Environment.NewLine}  {string.Join(Environment.NewLine + "  ", failedProjects)}");

            return failedProjects.Count == 0;
        }

        public override int GetHashCode() => base.GetHashCode();
        public override bool Equals(object? obj) => base.Equals(obj);
        public override string? ToString() => base.ToString();
    }
}
