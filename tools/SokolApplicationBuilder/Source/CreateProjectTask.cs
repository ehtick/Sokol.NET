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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SokolApplicationBuilder
{
    public class CreateProjectTask : Microsoft.Build.Utilities.Task
    {
        private Options options;

        public CreateProjectTask(Options opts)
        {
            options = opts;
        }

        public override bool Execute()
        {
            try
            {
                string projectName = options.ProjectName;
                string destination = options.Destination;
                
                if (string.IsNullOrWhiteSpace(projectName))
                {
                    Log.LogError("ERROR: Project name is required. Use --project <name>");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(destination))
                {
                    Log.LogError("ERROR: Destination path is required. Use --destination <path>");
                    return false;
                }

                // Validate project name (alphanumeric and underscore only)
                if (!Regex.IsMatch(projectName, @"^[a-zA-Z][a-zA-Z0-9_]*$"))
                {
                    Log.LogError($"ERROR: Invalid project name '{projectName}'. Name must start with a letter and contain only letters, numbers, and underscores.");
                    return false;
                }

                // Validate destination path
                if (!Directory.Exists(destination))
                {
                    Log.LogError($"ERROR: Destination directory does not exist: '{destination}'");
                    return false;
                }

                // Get SokolNetHome path
                string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrEmpty(homeDir) || !Directory.Exists(homeDir))
                {
                    homeDir = Environment.GetEnvironmentVariable("HOME") ?? "";
                }
                
                string sokolNetHomeFile = Path.Combine(homeDir, ".sokolnet_config", "sokolnet_home");
                if (!File.Exists(sokolNetHomeFile))
                {
                    Log.LogError("ERROR: SokolNetHome configuration not found. Please run 'register' task first.");
                    return false;
                }

                string sokolNetHome = File.ReadAllText(sokolNetHomeFile).Trim();
                
                // Ensure destination is not inside the Sokol.NET repository
                string normalizedDestination = Path.GetFullPath(destination);
                string normalizedSokolNetHome = Path.GetFullPath(sokolNetHome);
                
                if (normalizedDestination.StartsWith(normalizedSokolNetHome, StringComparison.OrdinalIgnoreCase))
                {
                    Log.LogError($"ERROR: Destination path cannot be inside the Sokol.NET repository.");
                    Log.LogError($"Repository path: {normalizedSokolNetHome}");
                    Log.LogError($"Destination path: {normalizedDestination}");
                    return false;
                }

                string templatePath = Path.Combine(sokolNetHome, "templates", "template_app");
                string targetPath = Path.Combine(destination, projectName);

                // Validate template exists
                if (!Directory.Exists(templatePath))
                {
                    Log.LogError($"ERROR: Template directory not found at '{templatePath}'");
                    return false;
                }

                // Check if project already exists at destination
                if (Directory.Exists(targetPath))
                {
                    Log.LogError($"ERROR: Project '{projectName}' already exists at '{targetPath}'");
                    return false;
                }

                Log.LogMessage(MessageImportance.High, $"Creating new project '{projectName}' from template...");
                Log.LogMessage(MessageImportance.High, $"Template: {templatePath}");
                Log.LogMessage(MessageImportance.High, $"Target: {targetPath}");

                // Create target directory
                Directory.CreateDirectory(targetPath);

                // Copy template files
                CopyDirectory(templatePath, targetPath, projectName);

                // Copy imgui and sokol folders from src
                CopySourceFolders(sokolNetHome, targetPath);

                // Rename files and update content
                RenameAndUpdateFiles(targetPath, projectName, sokolNetHome);

                // Update launch.json and tasks.json in the destination .vscode folder
                UpdateVSCodeConfig(targetPath, projectName, sokolNetHome);

                Log.LogMessage(MessageImportance.High, $"");
                Log.LogMessage(MessageImportance.High, $"Successfully created project '{projectName}'!");
                Log.LogMessage(MessageImportance.High, $"Location: {targetPath}");
                Log.LogMessage(MessageImportance.High, $"");
                Log.LogMessage(MessageImportance.High, $"Configuration files updated:");
                Log.LogMessage(MessageImportance.High, $"- Updated .vscode/launch.json (Desktop & Browser debugging)");
                Log.LogMessage(MessageImportance.High, $"- Updated .vscode/tasks.json (prepare tasks for all platforms)");
                Log.LogMessage(MessageImportance.High, $"");
                Log.LogMessage(MessageImportance.High, $"Next steps:");
                Log.LogMessage(MessageImportance.High, $"1. Open the project folder in VS Code: code \"{targetPath}\"");
                Log.LogMessage(MessageImportance.High, $"2. Add your shader code to: {Path.Combine(targetPath, "shaders")}");
                Log.LogMessage(MessageImportance.High, $"3. Add your assets to: {Path.Combine(targetPath, "Assets")}");
                Log.LogMessage(MessageImportance.High, $"4. Edit the main application: {Path.Combine(targetPath, "Source", projectName + "-app.cs")}");
                Log.LogMessage(MessageImportance.High, $"5. Press F5 in VS Code to run (Desktop or Browser)");

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"ERROR: Failed to create project: {ex.Message}");
                Log.LogError($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        private void CopyDirectory(string sourceDir, string targetDir, string projectName)
        {
            // Get all files and subdirectories
            foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = dirPath.Substring(sourceDir.Length + 1);
                
                // Skip imgui and sokol folders - they will be copied from src
                if (relativePath.StartsWith("Source" + Path.DirectorySeparatorChar + "imgui") ||
                    relativePath.StartsWith("Source" + Path.DirectorySeparatorChar + "sokol") ||
                    relativePath == "Source" + Path.DirectorySeparatorChar + "imgui" ||
                    relativePath == "Source" + Path.DirectorySeparatorChar + "sokol")
                {
                    continue;
                }
                
                Directory.CreateDirectory(Path.Combine(targetDir, relativePath));
            }

            foreach (string filePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = filePath.Substring(sourceDir.Length + 1);
                string targetFilePath = Path.Combine(targetDir, relativePath);
                
                // Skip .keep files
                if (Path.GetFileName(filePath) == ".keep")
                {
                    continue;
                }

                // Skip imgui and sokol folders - they will be copied from src
                if (relativePath.StartsWith("Source" + Path.DirectorySeparatorChar + "imgui" + Path.DirectorySeparatorChar) ||
                    relativePath.StartsWith("Source" + Path.DirectorySeparatorChar + "sokol" + Path.DirectorySeparatorChar))
                {
                    continue;
                }

                File.Copy(filePath, targetFilePath, true);
            }
        }

        private void CopySourceFolders(string sokolNetHome, string targetPath)
        {
            // Copy imgui folder from src to destination
            string imguiSource = Path.Combine(sokolNetHome, "src", "imgui");
            string imguiDest = Path.Combine(targetPath, "Source", "imgui");
            
            if (Directory.Exists(imguiSource))
            {
                Log.LogMessage(MessageImportance.Normal, $"Copying imgui from {imguiSource} to {imguiDest}");
                CopyDirectoryRecursive(imguiSource, imguiDest);
            }
            else
            {
                Log.LogWarning($"Warning: imgui source folder not found at '{imguiSource}'");
            }

            // Copy sokol folder from src to destination
            string sokolSource = Path.Combine(sokolNetHome, "src", "sokol");
            string sokolDest = Path.Combine(targetPath, "Source", "sokol");
            
            if (Directory.Exists(sokolSource))
            {
                Log.LogMessage(MessageImportance.Normal, $"Copying sokol from {sokolSource} to {sokolDest}");
                CopyDirectoryRecursive(sokolSource, sokolDest);
            }
            else
            {
                Log.LogWarning($"Warning: sokol source folder not found at '{sokolSource}'");
            }
        }

        private void CopyDirectoryRecursive(string sourceDir, string targetDir)
        {
            // Create target directory
            Directory.CreateDirectory(targetDir);

            // Copy all subdirectories
            foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = dirPath.Substring(sourceDir.Length + 1);
                Directory.CreateDirectory(Path.Combine(targetDir, relativePath));
            }

            // Copy all files
            foreach (string filePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = filePath.Substring(sourceDir.Length + 1);
                string targetFilePath = Path.Combine(targetDir, relativePath);
                File.Copy(filePath, targetFilePath, true);
            }
        }

        private void RenameAndUpdateFiles(string targetPath, string projectName, string sokolNetHome)
        {
            // Rename template.csproj to projectName.csproj
            string oldCsprojPath = Path.Combine(targetPath, "template.csproj");
            string newCsprojPath = Path.Combine(targetPath, projectName + ".csproj");
            if (File.Exists(oldCsprojPath))
            {
                File.Move(oldCsprojPath, newCsprojPath);
            }

            // Rename templateWeb.csproj to projectNameWeb.csproj
            string oldWebCsprojPath = Path.Combine(targetPath, "templateWeb.csproj");
            string newWebCsprojPath = Path.Combine(targetPath, projectName + "Web.csproj");
            if (File.Exists(oldWebCsprojPath))
            {
                File.Move(oldWebCsprojPath, newWebCsprojPath);
            }

            // Rename template-app.cs to projectName-app.cs
            string oldAppPath = Path.Combine(targetPath, "Source", "template-app.cs");
            string newAppPath = Path.Combine(targetPath, "Source", projectName + "-app.cs");
            if (File.Exists(oldAppPath))
            {
                File.Move(oldAppPath, newAppPath);
            }

            // Update content in all C# files
            UpdateFileContent(newAppPath, "TemplateApp", ToPascalCase(projectName) + "App");
            UpdateFileContent(Path.Combine(targetPath, "Source", "Program.cs"), "TemplateApp", ToPascalCase(projectName) + "App");

            // Update .csproj file references
            UpdateFileContent(newCsprojPath, "template", projectName);
            UpdateFileContent(newWebCsprojPath, "template", projectName);
        }

        private void UpdateFileContent(string filePath, string oldValue, string newValue)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            string content = File.ReadAllText(filePath);
            content = content.Replace(oldValue, newValue);
            File.WriteAllText(filePath, content);
        }

        private string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            // Split by underscore and capitalize each word
            var parts = input.Split('_');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                {
                    parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1).ToLower();
                }
            }

            return string.Join("", parts);
        }

        private void UpdateVSCodeConfig(string targetPath, string projectName, string sokolNetHome)
        {
            string vscodeDir = Path.Combine(targetPath, ".vscode");
            if (!Directory.Exists(vscodeDir))
            {
                Log.LogWarning($"Warning: .vscode directory not found at '{vscodeDir}'");
                return;
            }

            // Update launch.json
            UpdateLaunchJson(Path.Combine(vscodeDir, "launch.json"), projectName);

            // Update tasks.json
            UpdateTasksJson(Path.Combine(vscodeDir, "tasks.json"), projectName, sokolNetHome);
        }

        private void UpdateLaunchJson(string launchJsonPath, string projectName)
        {
            if (!File.Exists(launchJsonPath))
            {
                Log.LogWarning($"Warning: launch.json not found at '{launchJsonPath}'");
                return;
            }

            string content = File.ReadAllText(launchJsonPath);

            // Update program path from template.dll to projectName.dll
            content = content.Replace("template.dll", projectName + ".dll");
            
            // Update project name references
            content = content.Replace("template", projectName);

            File.WriteAllText(launchJsonPath, content);
            Log.LogMessage(MessageImportance.Normal, $"Updated launch.json for project '{projectName}'");
        }

        private void UpdateTasksJson(string tasksJsonPath, string projectName, string sokolNetHome)
        {
            if (!File.Exists(tasksJsonPath))
            {
                Log.LogWarning($"Warning: tasks.json not found at '{tasksJsonPath}'");
                return;
            }

            string content = File.ReadAllText(tasksJsonPath);

            // Replace template references with project name
            content = content.Replace("prepare-template", "prepare-" + projectName);
            content = content.Replace("\"template\"", "\"" + projectName + "\"");
            
            // Update the command to use the actual sokolnet_home path
            // The template uses platform-specific commands to read sokolnet_home
            // We'll keep those as-is since they work across platforms
            
            File.WriteAllText(tasksJsonPath, content);
            Log.LogMessage(MessageImportance.Normal, $"Updated tasks.json for project '{projectName}'");
        }
    }
}
