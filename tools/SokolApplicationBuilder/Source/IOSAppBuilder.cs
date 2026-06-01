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
//

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;
using CliWrap;
using CliWrap.Buffered;


namespace SokolApplicationBuilder
{
    enum ProcessortArchitecture
    {
        Intel=1,
        AppleSilicon
    }

    public class IOSBuildTask : Task
    {
        private readonly Options opts;
        private readonly Dictionary<string, string> envVars = new();

        private string PROJECT_UUID = string.Empty;
        private string PROJECT_NAME = string.Empty;
        private string JAVA_PACKAGE_PATH = string.Empty;
        private string VERSION_CODE = string.Empty;
        private string VERSION_NAME = string.Empty;

        private string URHONET_HOME_PATH = string.Empty;

        private string DEVELOPMENT_TEAM = string.Empty;
        
        // iOS properties from Directory.Build.props
        private string iOSBundlePrefix = "com.elix22";
        private string iOSMinVersion = "14.0";
        private string iOSScreenOrientation = "both";
        private bool iOSRequiresFullScreen = false;
        private bool iOSStatusBarHidden = false;
        private string iOSDevelopmentTeam = string.Empty;
        private string iOSIcon = string.Empty;
        private string appVersion = "1.0"; // Application version (common across all platforms)
        private Dictionary<string, string> iOSNativeLibraries = new Dictionary<string, string>(); // iOS native library paths
        // Arbitrary Info.plist key/value pairs from IOSInfoPlistKey_* properties in Directory.Build.props
        private Dictionary<string, string> iOSInfoPlistKeys = new Dictionary<string, string>();

        private string CLANG_CMD = string.Empty;
        private string AR_CMD = string.Empty;
        private string LIPO_CMD = string.Empty;
        private string IOS_SDK_PATH = string.Empty;

#pragma warning disable CS0414 // The field is assigned but its value is never used
        private ProcessortArchitecture processortArchitecture = ProcessortArchitecture.Intel;
#pragma warning restore CS0414

        public IOSBuildTask(Options opts)
        {
            this.opts = opts;
            Utils.opts = opts;
        }

        public override bool Execute()
        {
            return BuildIOSApp();
        }

        private bool BuildIOSApp()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Log.LogError("Can run only on Apple OSX");
                return false;
            }

            string architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();

            if (architecture == "Arm64")
            {
                Console.WriteLine("Processor Architecture : Apple Silicon");
                processortArchitecture = ProcessortArchitecture.AppleSilicon;
            }
            else if (architecture == "X64")
            {
                Console.WriteLine("Processor Architecture : Intel x86/x64");
                processortArchitecture = ProcessortArchitecture.Intel;
            }
            else
            {
                Log.LogError($"Unknown architecture: {architecture}");
                return false;
            }

            try
            {
                // Extract project information
                string projectPath = opts.Path;

                // Use smart project selection logic
                string projectName = GetProjectName(projectPath);
                string projectDir = projectPath;

                Log.LogMessage(MessageImportance.High, $"Building iOS app for project: {projectName}");

                // Read iOS properties from Directory.Build.props
                ReadIOSPropertiesFromDirectoryBuildProps(projectDir);

                // Setup development team (with caching support)
                if (!SetupDevelopmentTeam(projectName))
                    return false;

                // Create iOS build directory structure
                string iosDir = Path.Combine(projectDir, "ios");
                Directory.CreateDirectory(iosDir);

                // Build sokol framework
                if (!BuildSokolFramework(iosDir, projectDir))
                    return false;

                // Generate RegisteredScripts.g.cs for NativeAOT (iOS always needs this)
                Log.LogMessage(MessageImportance.High, "📝 Scanning for GameBehaviour subclasses...");
                Utils.GenerateRegisteredScripts(Log, projectDir, projectName);

                // Compile shaders
                if (!CompileShaders(projectDir))
                    return false;

                // Publish .NET project for iOS
                if (!PublishDotNetProject(projectDir, projectName))
                    return false;

                // Create app framework
                if (!CreateAppFramework(iosDir, projectDir, projectName))
                    return false;

                // Copy iOS templates
                if (!CopyIOSTemplates(iosDir, projectName))
                    return false;

                // Process iOS icon if specified
                ProcessIOSIcon(iosDir, projectDir);

                // Generate Xcode project
                if (!GenerateXcodeProject(iosDir, projectName))
                    return false;

                // Compile Xcode project (always compile when building)
                if (!CompileXcodeProject(iosDir, projectName))
                    return false;

                // Determine build type from opts
                string buildType = opts.Type == "debug" ? "debug" : "release";

                // Always copy to output folder (project's output folder or custom path)
                CopyToOutputPath(iosDir, projectName, buildType);

                // Install on device if requested
                if (opts.Install)
                {
                    // Always launch app after installation (matching Android behavior)
                    if (!InstallOnDevice(iosDir, projectName, true))
                        return false;
                }

                Log.LogMessage(MessageImportance.High, "iOS build completed successfully!");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"iOS build failed: {ex.Message}");
                return false;
            }
        }

        private bool BuildSokolFramework(string iosDir, string projectDir)
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Building sokol framework...");

                string sokolDir = Path.Combine(iosDir, "sokol-ios");
                Directory.CreateDirectory(sokolDir);

                // Find ext directory using the same logic as AndroidAppBuilder
                string extDir;
                string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrEmpty(homeDir) || !Directory.Exists(homeDir))
                {
                    homeDir = Environment.GetEnvironmentVariable("HOME") ?? "";
                }
                string configFile = Path.Combine(homeDir, ".sokolnet_config", "sokolnet_home");
                if (File.Exists(configFile))
                {
                    string sokolNetHome = File.ReadAllText(configFile).Trim();
                    extDir = Path.GetFullPath(Path.Combine(sokolNetHome, "ext"));
                }
                else
                {
                    extDir = Path.GetFullPath(Path.Combine(projectDir, "..", "..", "..", "ext"));
                }
                
                if (!Directory.Exists(extDir))
                {
                    Log.LogError($"ext directory not found at: {extDir}");
                    return false;
                }

                // Build CMake arguments
                string cmakeArgs = $"-G Xcode -DCMAKE_SYSTEM_NAME=iOS -DCMAKE_OSX_DEPLOYMENT_TARGET={iOSMinVersion} -DCMAKE_OSX_ARCHITECTURES=\"arm64\" \"{extDir}\"";

                // Build sokol framework using CMake
                var cmakeResult = Cli.Wrap("cmake")
                    .WithArguments(cmakeArgs)
                    .WithWorkingDirectory(sokolDir)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (cmakeResult.ExitCode != 0)
                {
                    Log.LogError($"CMake configure failed: {cmakeResult.StandardError}");
                    return false;
                }

                // Determine configuration based on build type
                string configuration = string.IsNullOrEmpty(opts.Type) || opts.Type.Equals("release", StringComparison.OrdinalIgnoreCase) 
                    ? "Release" 
                    : "Debug";

                var buildResult = Cli.Wrap("cmake")
                    .WithArguments($"--build . --config {configuration}")
                    .WithWorkingDirectory(sokolDir)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (buildResult.ExitCode != 0)
                {
                    Log.LogError($"CMake build failed: {buildResult.StandardError}");
                    return false;
                }

                // Copy framework to frameworks directory
                string frameworksDir = Path.Combine(iosDir, "frameworks");
                Directory.CreateDirectory(frameworksDir);

                string sourceFramework = Path.Combine(sokolDir, $"{configuration}-iphoneos", "sokol.framework");
                string destFramework = Path.Combine(frameworksDir, "sokol.framework");

                if (Directory.Exists(sourceFramework))
                {
                    CopyDirectory(sourceFramework, destFramework);
                }

                // Copy iOS native libraries to frameworks directory
                CopyIOSNativeLibraries(frameworksDir);

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to build sokol framework: {ex.Message}");
                return false;
            }
        }

        private bool CompileShaders(string projectDir)
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Compiling shaders...");

                // Get the project name using the same logic as the main build process
                string projectName = GetProjectName(projectDir);
                string projectFile = Path.Combine(projectDir, projectName + ".csproj");

                var result = Cli.Wrap("dotnet")
                    .WithArguments($"msbuild \"{projectFile}\" -t:CompileShaders -p:DefineConstants=\"__IOS__\"")
                    .WithWorkingDirectory(projectDir)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (result.ExitCode != 0)
                {
                    Log.LogError($"Shader compilation failed: {result.StandardError}");
                    return false;
                }

                Log.LogMessage(MessageImportance.High, "Shaders compilation completed");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Shader compilation failed: {ex.Message}");
                return false;
            }
        }

        private bool PublishDotNetProject(string projectDir, string projectName)
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Publishing .NET project for iOS...");

                string projectFile = Path.Combine(projectDir, projectName + ".csproj");
                
                // Determine configuration based on build type
                string configuration = string.IsNullOrEmpty(opts.Type) || opts.Type.Equals("release", StringComparison.OrdinalIgnoreCase) 
                    ? "Release" 
                    : "Debug";

                // Include DEBUG symbol for Debug builds (semicolon must be URL-encoded for MSBuild)
                string defineConstants = configuration == "Debug" ? "__IOS__%3BDEBUG" : "__IOS__";

                string publishArgs = $"publish \"{projectFile}\" -r ios-arm64 -c {configuration} -p:BuildAsLibrary=true -p:DefineConstants=\"{defineConstants}\"";
                if (!string.IsNullOrEmpty(opts.LinkerFlags))
                {
                    publishArgs += $" -p:LinkerFlags=\"{opts.LinkerFlags}\"";
                }

                var result = Cli.Wrap("dotnet")
                    .WithArguments(publishArgs)
                    .WithWorkingDirectory(projectDir)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (result.ExitCode != 0)
                {
                    Log.LogError($"Dotnet publish failed: {result.StandardError}");
                    return false;
                }

                Log.LogMessage(MessageImportance.High, "Dotnet publish completed");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Dotnet publish failed: {ex.Message}");
                return false;
            }
        }

        private bool CreateAppFramework(string iosDir, string projectDir, string projectName)
        {
            try
            {
                Log.LogMessage(MessageImportance.High, $"Creating {projectName} framework...");

                string sanitizedProjectName = projectName.Replace("_", "-");
                string frameworksDir = Path.Combine(iosDir, "frameworks");
                string frameworkDir = Path.Combine(frameworksDir, $"{sanitizedProjectName}.framework");
                Directory.CreateDirectory(frameworkDir);

                // Determine configuration based on build type
                string configuration = string.IsNullOrEmpty(opts.Type) || opts.Type.Equals("release", StringComparison.OrdinalIgnoreCase) 
                    ? "Release" 
                    : "Debug";

                string libPath = Path.Combine(projectDir, "bin", configuration, "net10.0", "ios-arm64", "publish", $"lib{projectName}.dylib");

                if (!File.Exists(libPath))
                {
                    Log.LogError($"Library file not found: {libPath}");
                    return false;
                }

                // Copy and modify the library (use sanitized name)
                string destLib = Path.Combine(frameworkDir, sanitizedProjectName);
                File.Copy(libPath, destLib, true);

                // Use install_name_tool to modify the library
                var idResult = Cli.Wrap("install_name_tool")
                    .WithArguments($"-id @rpath/{sanitizedProjectName}.framework/{sanitizedProjectName} \"{destLib}\"")
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (idResult.ExitCode != 0)
                {
                    Log.LogError($"install_name_tool id failed: {idResult.StandardError}");
                    return false;
                }

                // Copy Info.plist
                string infoPlistSource = Path.Combine(opts.TemplatesPath, "ios", "Info.plist");
                string infoPlistDest = Path.Combine(frameworkDir, "Info.plist");
                File.Copy(infoPlistSource, infoPlistDest, true);

                // Replace placeholders in Info.plist
                string content = File.ReadAllText(infoPlistDest);
                content = content.Replace("TEMPLATE_PROJECT_NAME", sanitizedProjectName);
                content = content.Replace("TEMPLATE_BUNDLE_PREFIX", iOSBundlePrefix);
                File.WriteAllText(infoPlistDest, content);

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to create app framework: {ex.Message}");
                return false;
            }
        }

        private bool CopyIOSTemplates(string iosDir, string projectName)
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Copying iOS templates...");

                string templatesDir = Path.Combine(opts.TemplatesPath, "ios");

                // Copy CMakeLists.txt
                string cmakeSource = Path.Combine(templatesDir, "CMakeLists.txt");
                string cmakeDest = Path.Combine(iosDir, "CMakeLists.txt");
                File.Copy(cmakeSource, cmakeDest, true);

                // Replace placeholders
                string content = File.ReadAllText(cmakeDest);
                string sanitizedProjectName = projectName.Replace("_", "-");
                content = content.Replace("TEMPLATE_PROJECT_NAME", sanitizedProjectName);
                content = content.Replace("TEMPLATE_BUNDLE_PREFIX", iOSBundlePrefix);
                content = content.Replace("TEMPLATE_APP_VERSION", appVersion);
                
                // Set orientation: Directory.Build.props takes precedence when it has a specific
                // (non-"both") value; command-line --orientation is a fallback used only when
                // the props file leaves orientation at the default ("both").
                string orientation;
                if (!string.IsNullOrEmpty(iOSScreenOrientation) && iOSScreenOrientation != "both")
                {
                    // Props file explicitly specifies a concrete orientation — always respect it.
                    orientation = iOSScreenOrientation;
                }
                else if (!string.IsNullOrEmpty(opts.Orientation) && opts.Orientation != "both")
                {
                    // Props says "both" (or unset); honour explicit command-line override.
                    orientation = opts.ValidatedOrientation;
                }
                else
                {
                    orientation = iOSScreenOrientation; // "both" / default
                }
                
                string iosOrientations, ipadOrientations;
                string iosOrientationsPlist, ipadOrientationsPlist;
                switch (orientation)
                {
                    case "portrait":
                        iosOrientations = "UIInterfaceOrientationPortrait";
                        ipadOrientations = "UIInterfaceOrientationPortrait";
                        iosOrientationsPlist = "\n        <string>UIInterfaceOrientationPortrait</string>";
                        ipadOrientationsPlist = "\n        <string>UIInterfaceOrientationPortrait</string>";
                        break;
                    case "portrait_upside_down":
                        iosOrientations = "UIInterfaceOrientationPortraitUpsideDown";
                        ipadOrientations = "UIInterfaceOrientationPortraitUpsideDown";
                        iosOrientationsPlist = "\n        <string>UIInterfaceOrientationPortraitUpsideDown</string>";
                        ipadOrientationsPlist = "\n        <string>UIInterfaceOrientationPortraitUpsideDown</string>";
                        break;
                    case "landscape_left":
                        iosOrientations = "UIInterfaceOrientationLandscapeLeft";
                        ipadOrientations = "UIInterfaceOrientationLandscapeLeft";
                        iosOrientationsPlist = "\n        <string>UIInterfaceOrientationLandscapeLeft</string>";
                        ipadOrientationsPlist = "\n        <string>UIInterfaceOrientationLandscapeLeft</string>";
                        break;
                    case "landscape_right":
                        iosOrientations = "UIInterfaceOrientationLandscapeRight";
                        ipadOrientations = "UIInterfaceOrientationLandscapeRight";
                        iosOrientationsPlist = "\n        <string>UIInterfaceOrientationLandscapeRight</string>";
                        ipadOrientationsPlist = "\n        <string>UIInterfaceOrientationLandscapeRight</string>";
                        break;
                    case "landscape":
                        iosOrientations = "UIInterfaceOrientationLandscapeLeft UIInterfaceOrientationLandscapeRight";
                        ipadOrientations = "UIInterfaceOrientationLandscapeLeft UIInterfaceOrientationLandscapeRight";
                        iosOrientationsPlist = "\n        <string>UIInterfaceOrientationLandscapeLeft</string>\n        <string>UIInterfaceOrientationLandscapeRight</string>";
                        ipadOrientationsPlist = "\n        <string>UIInterfaceOrientationLandscapeLeft</string>\n        <string>UIInterfaceOrientationLandscapeRight</string>";
                        break;
                    case "both":
                    default:
                        iosOrientations = "UIInterfaceOrientationPortrait UIInterfaceOrientationLandscapeLeft UIInterfaceOrientationLandscapeRight";
                        ipadOrientations = "UIInterfaceOrientationPortrait UIInterfaceOrientationPortraitUpsideDown UIInterfaceOrientationLandscapeLeft UIInterfaceOrientationLandscapeRight";
                        iosOrientationsPlist = "\n        <string>UIInterfaceOrientationPortrait</string>\n        <string>UIInterfaceOrientationPortraitUpsideDown</string>\n        <string>UIInterfaceOrientationLandscapeLeft</string>\n        <string>UIInterfaceOrientationLandscapeRight</string>";
                        ipadOrientationsPlist = "\n        <string>UIInterfaceOrientationPortrait</string>\n        <string>UIInterfaceOrientationPortraitUpsideDown</string>\n        <string>UIInterfaceOrientationLandscapeLeft</string>\n        <string>UIInterfaceOrientationLandscapeRight</string>";
                        break;
                }
                
                content = content.Replace("TEMPLATE_IOS_ORIENTATIONS", iosOrientations);
                content = content.Replace("TEMPLATE_IPAD_ORIENTATIONS", ipadOrientations);
                
                // Configure native libraries in CMakeLists.txt
                ConfigureIOSNativeLibrariesInCMake(ref content, projectName);
                
                File.WriteAllText(cmakeDest, content);

                // Copy and process Info.plist.in
                string plistSource = Path.Combine(templatesDir, "Info.plist.in");
                string plistDest = Path.Combine(iosDir, "Info.plist.in");
                if (File.Exists(plistSource))
                {
                    string plistContent = File.ReadAllText(plistSource);
                    plistContent = plistContent.Replace("TEMPLATE_PROJECT_NAME", sanitizedProjectName);
                    plistContent = plistContent.Replace("@TEMPLATE_IOS_ORIENTATIONS_PLIST@", iosOrientationsPlist);
                    plistContent = plistContent.Replace("@TEMPLATE_IPAD_ORIENTATIONS_PLIST@", ipadOrientationsPlist);
                    
                    // Add UIStatusBarHidden if enabled
                    string statusBarHiddenPlist = iOSStatusBarHidden 
                        ? "\n    <key>UIStatusBarHidden</key>\n    <true/>"
                        : "";
                    plistContent = plistContent.Replace("@TEMPLATE_STATUS_BAR_HIDDEN@", statusBarHiddenPlist);
                    
                    // Add UIViewControllerBasedStatusBarAppearance when status bar is hidden
                    // This tells iOS to use the Info.plist setting instead of view controller override
                    string statusBarAppearancePlist = iOSStatusBarHidden 
                        ? "\n    <key>UIViewControllerBasedStatusBarAppearance</key>\n    <false/>"
                        : "";
                    plistContent = plistContent.Replace("@TEMPLATE_STATUS_BAR_APPEARANCE@", statusBarAppearancePlist);
                    
                    // Add UIRequiresFullScreen if enabled
                    string requiresFullScreenPlist = iOSRequiresFullScreen 
                        ? "\n    <key>UIRequiresFullScreen</key>\n    <true/>"
                        : "";
                    plistContent = plistContent.Replace("@TEMPLATE_REQUIRES_FULLSCREEN@", requiresFullScreenPlist);

                    // Inject arbitrary Info.plist key/value pairs from IOSInfoPlistKey_* properties.
                    // Each entry becomes:  <key>KeyName</key>\n    <string>value</string>
                    var extraPlistSb = new System.Text.StringBuilder();
                    foreach (var kv in iOSInfoPlistKeys)
                    {
                        extraPlistSb.Append($"\n    <key>{System.Security.SecurityElement.Escape(kv.Key)}</key>");
                        extraPlistSb.Append($"\n    <string>{System.Security.SecurityElement.Escape(kv.Value)}</string>");
                    }
                    plistContent = plistContent.Replace("@TEMPLATE_IOS_PLIST_EXTRA_KEYS@", extraPlistSb.ToString());

                    File.WriteAllText(plistDest, plistContent);
                }

                // Copy main.m
                string mainSource = Path.Combine(templatesDir, "main.m");
                string mainDest = Path.Combine(iosDir, "main.m");
                File.Copy(mainSource, mainDest, true);

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to copy iOS templates: {ex.Message}");
                return false;
            }
        }

        private bool GenerateXcodeProject(string iosDir, string projectName)
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Generating Xcode project...");

                string buildDir = Path.Combine(iosDir, "build-xcode-ios-app");
                Directory.CreateDirectory(buildDir);

                // Build cmake command with optional development team
                string cmakeCmd = ".. -G Xcode";
                if (!string.IsNullOrEmpty(DEVELOPMENT_TEAM))
                {
                    cmakeCmd += $" -DDEVELOPMENT_TEAM={DEVELOPMENT_TEAM}";
                }

                var result = Cli.Wrap("cmake")
                    .WithArguments(cmakeCmd)
                    .WithWorkingDirectory(buildDir)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (result.ExitCode != 0)
                {
                    Log.LogError($"CMake Xcode generation failed: {result.StandardError}");
                    return false;
                }

                Log.LogMessage(MessageImportance.High, "Xcode project generated successfully");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to generate Xcode project: {ex.Message}");
                return false;
            }
        }

        private bool CompileXcodeProject(string iosDir, string projectName)
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Compiling Xcode project...");

                string sanitizedProjectName = projectName.Replace("_", "-");
                string buildDir = Path.Combine(iosDir, "build-xcode-ios-app");
                
                // Determine configuration based on build type
                string configuration = string.IsNullOrEmpty(opts.Type) || opts.Type.Equals("release", StringComparison.OrdinalIgnoreCase) 
                    ? "Release" 
                    : "Debug";

                var result = Cli.Wrap("xcodebuild")
                    .WithArguments($"-target {sanitizedProjectName}-ios-app -configuration {configuration} -sdk iphoneos -arch arm64")
                    .WithWorkingDirectory(buildDir)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (result.ExitCode != 0)
                {
                    Log.LogError($"Xcode build failed: {result.StandardError}");
                    return false;
                }

                string appBundlePath = Path.Combine(buildDir, $"{configuration}-iphoneos", $"{sanitizedProjectName}-ios-app.app");
                
                // Check if the app bundle exists at the expected location, otherwise look in bin/{configuration}
                if (!Directory.Exists(appBundlePath))
                {
                    string altPath = Path.Combine(buildDir, "bin", configuration, $"{sanitizedProjectName}-ios-app.app");
                    if (Directory.Exists(altPath))
                    {
                        appBundlePath = altPath;
                    }
                }
                
                Log.LogMessage(MessageImportance.High, $"Xcode project compiled successfully!");
                Log.LogMessage(MessageImportance.High, $"App bundle location: {appBundlePath}");

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to compile Xcode project: {ex.Message}");
                return false;
            }
        }

        private bool InstallOnDevice(string iosDir, string projectName, bool runAfterInstall = false)
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Installing on iOS device...");

                string sanitizedProjectName = projectName.Replace("_", "-");
                string buildDir = Path.Combine(iosDir, "build-xcode-ios-app");
                
                // Determine configuration based on build type
                string configuration = string.IsNullOrEmpty(opts.Type) || opts.Type.Equals("release", StringComparison.OrdinalIgnoreCase) 
                    ? "Release" 
                    : "Debug";
                
                string appBundlePath = Path.Combine(buildDir, $"{configuration}-iphoneos", $"{sanitizedProjectName}-ios-app.app");

                // Check multiple possible locations for the app bundle
                string[] possiblePaths = new[]
                {
                    appBundlePath,
                    Path.Combine(buildDir, $"{configuration}-iphoneos", $"{sanitizedProjectName}-ios-app", $"{sanitizedProjectName}-ios-app.app"),
                    Path.Combine(buildDir, configuration, $"{sanitizedProjectName}-ios-app.app"),
                    Path.Combine(buildDir, $"{sanitizedProjectName}-ios-app.app"),
                    Path.Combine(buildDir, "bin", configuration, $"{sanitizedProjectName}-ios-app.app")
                };

                string? foundPath = null;
                foreach (var path in possiblePaths)
                {
                    if (Directory.Exists(path))
                    {
                        foundPath = path;
                        break;
                    }
                }

                if (foundPath == null)
                {
                    Log.LogError($"App bundle not found at expected locations:");
                    foreach (var path in possiblePaths)
                    {
                        Log.LogError($"  {path}");
                    }

                    // List contents of build directory to see what's actually there
                    Log.LogMessage(MessageImportance.High, $"Contents of build directory {buildDir}:");
                    if (Directory.Exists(buildDir))
                    {
                        foreach (var item in Directory.GetFileSystemEntries(buildDir))
                        {
                            Log.LogMessage(MessageImportance.Normal, $"  {item}");
                        }
                    }
                    return false;
                }

                appBundlePath = foundPath;
                Log.LogMessage(MessageImportance.High, $"Found app bundle at: {appBundlePath}");

                // Determine the target device ID.
                // If the caller passed --ios-device, use it directly (supports WiFi devices).
                // Otherwise fall back to ios-deploy -c USB scan.
                string targetDeviceId;
                string targetDeviceName;

                if (!string.IsNullOrEmpty(opts.IOSDeviceId))
                {
                    targetDeviceId   = opts.IOSDeviceId;
                    targetDeviceName = opts.IOSDeviceId;
                    Log.LogMessage(MessageImportance.High, $"Using specified iOS device: {targetDeviceId}");
                }
                else
                {
                    // Legacy USB-only scan via ios-deploy -c
                    var checkResult = Cli.Wrap("which")
                        .WithArguments("ios-deploy")
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteBufferedAsync().GetAwaiter().GetResult();

                    if (checkResult.ExitCode != 0)
                    {
                        Log.LogError("ios-deploy not found and no --ios-device specified. Install with: brew install ios-deploy");
                        return false;
                    }

                    var deviceResult = Cli.Wrap("ios-deploy")
                        .WithArguments("-c --timeout 2")
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteBufferedAsync().GetAwaiter().GetResult();

                    var usbDevices = new List<(string Id, string Name)>();
                    foreach (var rawLine in deviceResult.StandardOutput.Split('\n'))
                    {
                        if (!rawLine.Contains("Found")) continue;
                        var afterFound = rawLine.Substring(rawLine.IndexOf("Found") + 6);
                        var idEnd      = afterFound.IndexOf(" ");
                        if (idEnd <= 0) continue;
                        var id   = afterFound.Substring(0, idEnd).Trim();
                        var name = "Unknown";
                        var aka  = afterFound.IndexOf("a.k.a.");
                        if (aka >= 0) name = afterFound.Substring(aka + 7).Trim('\'', ' ');
                        usbDevices.Add((id, name));
                    }

                    if (usbDevices.Count == 0)
                    {
                        Log.LogError("No iOS devices found via USB. Connect a device with a cable or pass --ios-device <UDID> for WiFi install.");
                        return false;
                    }

                    targetDeviceId   = usbDevices[0].Id;
                    targetDeviceName = usbDevices[0].Name;
                    Log.LogMessage(MessageImportance.High, $"Found USB device: {targetDeviceName} ({targetDeviceId})");
                }

                // ── Install ───────────────────────────────────────────────────────
                // Primary: xcrun devicectl — works over WiFi and USB (Xcode 15+, iOS 17+)
                bool installed = TryInstallViaDevicectl(targetDeviceId, targetDeviceName, appBundlePath);

                // Fallback: ios-deploy without --no-wifi (supports WiFi if paired, and USB)
                if (!installed)
                    installed = TryInstallViaIosDeploy(targetDeviceId, targetDeviceName, appBundlePath);

                if (!installed)
                {
                    Log.LogError($"All installation methods failed for device {targetDeviceName} ({targetDeviceId}).");
                    return false;
                }

                Log.LogMessage(MessageImportance.High, $"App installed successfully on device: {targetDeviceName}!");

                // ── Launch ────────────────────────────────────────────────────────
                if (runAfterInstall)
                {
                    Log.LogMessage(MessageImportance.High, $"Launching app on device: {targetDeviceName} ({targetDeviceId})");

                    string infoPlistPath = Path.Combine(appBundlePath, "Info.plist");
                    string bundleId      = "";

                    try
                    {
                        var plistResult = Cli.Wrap("plutil")
                            .WithArguments($"-extract CFBundleIdentifier raw \"{infoPlistPath}\"")
                            .WithValidation(CommandResultValidation.None)
                            .ExecuteBufferedAsync().GetAwaiter().GetResult();

                        if (plistResult.ExitCode == 0)
                            bundleId = plistResult.StandardOutput.Trim();
                        else
                            Log.LogWarning($"Could not extract bundle ID from Info.plist: {plistResult.StandardError}");
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning($"plutil failed: {ex.Message}");
                    }

                    if (!string.IsNullOrEmpty(bundleId))
                    {
                        var launchResult = Cli.Wrap("xcrun")
                            .WithArguments($"devicectl device process launch --device {targetDeviceId} {bundleId}")
                            .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                            .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                            .WithValidation(CommandResultValidation.None)
                            .ExecuteBufferedAsync().GetAwaiter().GetResult();

                        if (launchResult.ExitCode != 0)
                            Log.LogWarning($"App launch failed (exit {launchResult.ExitCode}): {launchResult.StandardError}");
                        else
                            Log.LogMessage(MessageImportance.High, $"App launched successfully on device: {targetDeviceName}!");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to install on device: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Installs the app bundle using <c>xcrun devicectl</c> (Xcode 15+, iOS 17+).
        /// Works over WiFi and USB; no extra tools required beyond a standard Xcode install.
        /// The device must have "Connect via Network" enabled (Xcode → Window → Devices → checkbox).
        /// </summary>
        private bool TryInstallViaDevicectl(string deviceId, string deviceName, string appBundlePath)
        {
            try
            {
                Log.LogMessage(MessageImportance.High, $"[Install] Trying xcrun devicectl for {deviceName}...");

                var result = Cli.Wrap("xcrun")
                    .WithArguments($"devicectl device install app --device {deviceId} \"{appBundlePath}\"")
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync().GetAwaiter().GetResult();

                if (result.ExitCode == 0)
                {
                    Log.LogMessage(MessageImportance.High, $"[Install] devicectl succeeded.");
                    return true;
                }

                Log.LogMessage(MessageImportance.Normal, $"[Install] devicectl failed (exit {result.ExitCode}): {result.StandardError}");
                return false;
            }
            catch (Exception ex)
            {
                Log.LogMessage(MessageImportance.Normal, $"[Install] devicectl not available: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Installs the app bundle using <c>ios-deploy</c> (brew install ios-deploy).
        /// Works over WiFi when the device is network-paired; also works over USB.
        /// Does NOT pass --no-wifi so both transports are allowed.
        /// </summary>
        private bool TryInstallViaIosDeploy(string deviceId, string deviceName, string appBundlePath)
        {
            try
            {
                string iosDeploy = FindTool("ios-deploy");
                if (string.IsNullOrEmpty(iosDeploy))
                {
                    Log.LogMessage(MessageImportance.Normal, "[Install] ios-deploy not found, skipping.");
                    return false;
                }

                Log.LogMessage(MessageImportance.High, $"[Install] Trying ios-deploy for {deviceName}...");

                var result = Cli.Wrap(iosDeploy)
                    .WithArguments($"--id {deviceId} --bundle \"{appBundlePath}\"")
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync().GetAwaiter().GetResult();

                if (result.ExitCode == 0)
                {
                    Log.LogMessage(MessageImportance.High, $"[Install] ios-deploy succeeded.");
                    return true;
                }

                Log.LogMessage(MessageImportance.Normal, $"[Install] ios-deploy failed (exit {result.ExitCode}): {result.StandardError}");
                return false;
            }
            catch (Exception ex)
            {
                Log.LogMessage(MessageImportance.Normal, $"[Install] ios-deploy error: {ex.Message}");
                return false;
            }
        }

        private static string FindTool(string name)
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            char sep    = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
            var dirs    = new List<string>(pathEnv.Split(sep, StringSplitOptions.RemoveEmptyEntries));
            foreach (var extra in new[] { "/usr/bin", "/usr/local/bin", "/opt/homebrew/bin", "/opt/local/bin" })
                if (!dirs.Contains(extra)) dirs.Add(extra);
            foreach (var dir in dirs)
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate)) return candidate;
            }
            return string.Empty;
        }

        private string GetProjectName(string projectPath)
        {
            // If project name is explicitly provided via options, use it
            if (!string.IsNullOrEmpty(opts.ProjectName))
            {
                Log.LogMessage(MessageImportance.Normal, $"Using explicitly specified project name: {opts.ProjectName}");
                return opts.ProjectName;
            }

            // Find all .csproj files in the project directory
            string[] csprojFiles = Directory.GetFiles(projectPath, "*.csproj");

            if (csprojFiles.Length == 0)
            {
                Log.LogError($"No .csproj files found in directory: {projectPath}");
                throw new FileNotFoundException("No .csproj files found in the specified directory");
            }

            if (csprojFiles.Length == 1)
            {
                // Only one project found, use it
                string projectName = Path.GetFileNameWithoutExtension(csprojFiles[0]);
                Log.LogMessage(MessageImportance.Normal, $"Found single project: {projectName}");
                return projectName;
            }

            // Multiple projects found, try to match with parent folder name
            string parentFolderName = Path.GetFileName(projectPath);
            Log.LogMessage(MessageImportance.Normal, $"Found {csprojFiles.Length} projects, looking for match with parent folder: {parentFolderName}");

            foreach (string csprojFile in csprojFiles)
            {
                string projectName = Path.GetFileNameWithoutExtension(csprojFile);
                if (string.Equals(projectName, parentFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    Log.LogMessage(MessageImportance.Normal, $"Matched project with parent folder name: {projectName}");
                    return projectName;
                }
            }

            // No match found, list available projects and use the first one as fallback
            Log.LogMessage(MessageImportance.Normal, $"No project matched parent folder name '{parentFolderName}'. Available projects:");
            foreach (string csprojFile in csprojFiles)
            {
                string projectName = Path.GetFileNameWithoutExtension(csprojFile);
                Log.LogMessage(MessageImportance.Normal, $"  - {projectName}");
            }

            string fallbackProject = Path.GetFileNameWithoutExtension(csprojFiles[0]);
            Log.LogMessage(MessageImportance.Normal, $"Using first project as fallback: {fallbackProject}");
            return fallbackProject;
        }

        private void ReadIOSPropertiesFromDirectoryBuildProps(string projectPath)
        {
            try
            {
                string directoryBuildPropsPath = Path.Combine(projectPath, "Directory.Build.props");
                if (!File.Exists(directoryBuildPropsPath))
                {
                    Log.LogMessage(MessageImportance.Normal, "No Directory.Build.props found, using default iOS properties");
                    return;
                }

                XDocument doc = XDocument.Load(directoryBuildPropsPath);
                int propertyCount = 0;

                // Read all PropertyGroup elements (not just the first one)
                foreach (var propertyGroup in doc.Root?.Elements("PropertyGroup") ?? Enumerable.Empty<XElement>())
                {
                    // iOS Bundle Prefix
                    var bundlePrefixElement = propertyGroup.Element("IOSBundlePrefix");
                    if (bundlePrefixElement != null && !string.IsNullOrEmpty(bundlePrefixElement.Value))
                    {
                        iOSBundlePrefix = bundlePrefixElement.Value;
                        propertyCount++;
                    }

                    // iOS Minimum Version
                    var minVersionElement = propertyGroup.Element("IOSMinVersion");
                    if (minVersionElement != null && !string.IsNullOrEmpty(minVersionElement.Value))
                    {
                        iOSMinVersion = minVersionElement.Value;
                        propertyCount++;
                    }

                    // iOS Screen Orientation
                    var orientationElement = propertyGroup.Element("IOSScreenOrientation");
                    if (orientationElement != null && !string.IsNullOrEmpty(orientationElement.Value))
                    {
                        iOSScreenOrientation = orientationElement.Value.ToLower();
                        propertyCount++;
                    }

                    // iOS Requires Full Screen
                    var fullscreenElement = propertyGroup.Element("IOSRequiresFullScreen");
                    if (fullscreenElement != null && !string.IsNullOrEmpty(fullscreenElement.Value))
                    {
                        iOSRequiresFullScreen = bool.Parse(fullscreenElement.Value);
                        propertyCount++;
                    }

                    // iOS Status Bar Hidden
                    var statusBarElement = propertyGroup.Element("IOSStatusBarHidden");
                    if (statusBarElement != null && !string.IsNullOrEmpty(statusBarElement.Value))
                    {
                        iOSStatusBarHidden = bool.Parse(statusBarElement.Value);
                        propertyCount++;
                    }

                    // iOS Development Team
                    var devTeamElement = propertyGroup.Element("IOSDevelopmentTeam");
                    if (devTeamElement != null && !string.IsNullOrEmpty(devTeamElement.Value))
                    {
                        iOSDevelopmentTeam = devTeamElement.Value;
                        propertyCount++;
                    }

                    // iOS Icon
                    var iconElement = propertyGroup.Element("IOSIcon");
                    if (iconElement != null && !string.IsNullOrEmpty(iconElement.Value))
                    {
                        iOSIcon = iconElement.Value;
                        propertyCount++;
                    }

                    // App Version (common across all platforms)
                    var versionElement = propertyGroup.Element("AppVersion");
                    if (versionElement != null && !string.IsNullOrEmpty(versionElement.Value))
                    {
                        appVersion = versionElement.Value;
                        propertyCount++;
                    }

                    // Detect iOS native libraries (IOSNativeLibrary_*Path properties)
                    // and arbitrary Info.plist key/value pairs (IOSInfoPlistKey_* properties)
                    foreach (var element in propertyGroup.Elements())
                    {
                        string elementName = element.Name.LocalName;
                        if (elementName.StartsWith("IOSNativeLibrary_") && elementName.EndsWith("Path"))
                        {
                            // Extract library name from IOSNativeLibrary_[LibraryName]Path
                            string libraryName = elementName.Substring("IOSNativeLibrary_".Length);
                            libraryName = libraryName.Substring(0, libraryName.Length - "Path".Length);
                            
                            if (!string.IsNullOrEmpty(element.Value))
                            {
                                string libraryBasePath = element.Value;

                                // Expand MSBuild variables that the XML reader leaves unexpanded
                                libraryBasePath = libraryBasePath.Replace("$(SokolNetHome)", Utils.GetSokolNetHome(), StringComparison.OrdinalIgnoreCase);
                                libraryBasePath = libraryBasePath.Replace("$(HomeDir)", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase);

                                string absolutePath = Path.IsPathRooted(libraryBasePath)
                                    ? libraryBasePath
                                    : Path.Combine(projectPath, libraryBasePath);
                                    
                                iOSNativeLibraries[libraryName] = absolutePath;
                                propertyCount++;
                            }
                        }
                        else if (elementName.StartsWith("IOSInfoPlistKey_"))
                        {
                            // Extract the plist key name from IOSInfoPlistKey_[PlistKeyName]
                            string plistKey = elementName.Substring("IOSInfoPlistKey_".Length);
                            if (!string.IsNullOrEmpty(plistKey) && !string.IsNullOrEmpty(element.Value))
                            {
                                iOSInfoPlistKeys[plistKey] = element.Value;
                                propertyCount++;
                            }
                        }
                    }
                }

                if (propertyCount > 0)
                {
                    Log.LogMessage(MessageImportance.High, $"📋 Read {propertyCount} iOS properties from Directory.Build.props");
                    if (!string.IsNullOrEmpty(appVersion))
                        Log.LogMessage(MessageImportance.High, $"   - AppVersion: {appVersion}");
                    if (!string.IsNullOrEmpty(iOSBundlePrefix))
                        Log.LogMessage(MessageImportance.High, $"   - IOSBundlePrefix: {iOSBundlePrefix}");
                    if (!string.IsNullOrEmpty(iOSMinVersion))
                        Log.LogMessage(MessageImportance.High, $"   - IOSMinVersion: {iOSMinVersion}");
                    if (!string.IsNullOrEmpty(iOSScreenOrientation))
                        Log.LogMessage(MessageImportance.High, $"   - IOSScreenOrientation: {iOSScreenOrientation}");
                    Log.LogMessage(MessageImportance.High, $"   - IOSRequiresFullScreen: {iOSRequiresFullScreen}");
                    Log.LogMessage(MessageImportance.High, $"   - IOSStatusBarHidden: {iOSStatusBarHidden}");
                    if (!string.IsNullOrEmpty(iOSDevelopmentTeam))
                        Log.LogMessage(MessageImportance.High, $"   - IOSDevelopmentTeam: {iOSDevelopmentTeam}");
                    if (!string.IsNullOrEmpty(iOSIcon))
                        Log.LogMessage(MessageImportance.High, $"   - IOSIcon: {iOSIcon}");
                    
                    // Log iOS native libraries
                    foreach (var library in iOSNativeLibraries)
                    {
                        Log.LogMessage(MessageImportance.High, $"   - IOSNativeLibrary_{library.Key}Path: {library.Value}");
                    }
                    // Log extra plist keys
                    foreach (var kv in iOSInfoPlistKeys)
                    {
                        Log.LogMessage(MessageImportance.High, $"   - IOSInfoPlistKey_{kv.Key}: {kv.Value}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to read iOS properties from Directory.Build.props: {ex.Message}");
            }
        }

        private string GetTeamIdCacheFile(string projectName)
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string cacheDir = Path.Combine(homeDir, ".Sokol.NET-cache");
            Directory.CreateDirectory(cacheDir);
            return Path.Combine(cacheDir, $"{projectName}.teamid");
        }

        private string? GetCachedTeamId(string projectName)
        {
            try
            {
                string cacheFile = GetTeamIdCacheFile(projectName);
                if (File.Exists(cacheFile))
                {
                    string cachedTeamId = File.ReadAllText(cacheFile).Trim();
                    // Validate team ID format (should be 10 alphanumeric characters)
                    if (!string.IsNullOrEmpty(cachedTeamId) && 
                        System.Text.RegularExpressions.Regex.IsMatch(cachedTeamId, @"^[A-Z0-9]{10}$"))
                    {
                        return cachedTeamId;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage(MessageImportance.Normal, $"Failed to read cached team ID: {ex.Message}");
            }
            return null;
        }

        private void SaveTeamIdToCache(string projectName, string teamId)
        {
            try
            {
                string cacheFile = GetTeamIdCacheFile(projectName);
                File.WriteAllText(cacheFile, teamId);
                Log.LogMessage(MessageImportance.Normal, "💾 Team ID cached for future use");
            }
            catch (Exception ex)
            {
                Log.LogMessage(MessageImportance.Normal, $"Failed to cache team ID: {ex.Message}");
            }
        }

        private bool SetupDevelopmentTeam(string projectName)
        {
            // If team ID provided via command line, use it
            if (!string.IsNullOrEmpty(opts.DevelopmentTeam))
            {
                DEVELOPMENT_TEAM = opts.DevelopmentTeam;
                Log.LogMessage(MessageImportance.High, $"Using development team from command line: {DEVELOPMENT_TEAM}");
                SaveTeamIdToCache(projectName, DEVELOPMENT_TEAM);
                return true;
            }

            // Try to get team ID from Directory.Build.props
            if (!string.IsNullOrEmpty(iOSDevelopmentTeam))
            {
                DEVELOPMENT_TEAM = iOSDevelopmentTeam;
                Log.LogMessage(MessageImportance.High, $"✅ Using Development Team ID from Directory.Build.props: {DEVELOPMENT_TEAM}");
                SaveTeamIdToCache(projectName, DEVELOPMENT_TEAM);
                return true;
            }

            // Try to get cached team ID
            string? cachedTeamId = GetCachedTeamId(projectName);
            if (cachedTeamId != null)
            {
                DEVELOPMENT_TEAM = cachedTeamId;
                Log.LogMessage(MessageImportance.High, $"✅ Using cached Development Team ID: {DEVELOPMENT_TEAM}");
                string cacheFile = GetTeamIdCacheFile(projectName);
                Log.LogMessage(MessageImportance.Normal, $"   (Delete {cacheFile} to reset)");
                return true;
            }

            // Interactive mode - prompt for team ID
            if (opts.Interactive)
            {
                Console.WriteLine();
                Console.WriteLine("🔑 iOS Development Team ID Required");
                Console.WriteLine("===================================");
                Console.WriteLine("Enter your Apple Developer Team ID (found in developer.apple.com/account):");
                Console.Write("Development Team ID: ");
                
                string? teamId = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrEmpty(teamId))
                {
                    Log.LogError("❌ Development Team ID is required for iOS builds");
                    return false;
                }

                // Validate team ID format
                if (!System.Text.RegularExpressions.Regex.IsMatch(teamId, @"^[A-Z0-9]{10}$"))
                {
                    Log.LogWarning("⚠️  Team ID format looks incorrect (should be 10 alphanumeric characters)");
                    Log.LogWarning("   Continuing anyway, but this may cause build failures...");
                }

                DEVELOPMENT_TEAM = teamId;
                SaveTeamIdToCache(projectName, teamId);
                return true;
            }

            // Non-interactive mode without team ID
            Log.LogError("❌ Development Team ID is required for iOS builds");
            Log.LogError("   Provide it via --development-team flag or use --interactive mode");
            return false;
        }

        private void CopyIOSNativeLibraries(string frameworksDir)
        {
            if (iOSNativeLibraries.Count == 0)
            {
                return;
            }

            Log.LogMessage(MessageImportance.High, "📦 Copying iOS native libraries to frameworks directory...");

            foreach (var library in iOSNativeLibraries)
            {
                string libraryName = library.Key;
                string libraryPath = library.Value;
                
                Log.LogMessage(MessageImportance.High, $"   Processing {libraryName} from {libraryPath}");

                if (!Directory.Exists(libraryPath))
                {
                    Log.LogWarning($"iOS native library directory not found: {libraryPath}");
                    continue;
                }

                // Look for .framework directories in the library path
                string[] frameworks = Directory.GetDirectories(libraryPath, "*.framework", SearchOption.TopDirectoryOnly);
                
                if (frameworks.Length == 0)
                {
                    Log.LogWarning($"No .framework directories found in {libraryPath}");
                    continue;
                }

                foreach (string frameworkDir in frameworks)
                {
                    string frameworkName = Path.GetFileName(frameworkDir);
                    string destFramework = Path.Combine(frameworksDir, frameworkName);

                    if (Directory.Exists(destFramework))
                    {
                        Log.LogMessage(MessageImportance.Normal, $"   Removing existing framework: {frameworkName}");
                        Directory.Delete(destFramework, true);
                    }

                    Log.LogMessage(MessageImportance.High, $"   ✅ Copying {frameworkName} framework");
                    CopyDirectory(frameworkDir, destFramework);
                }
            }

            Log.LogMessage(MessageImportance.High, "📦 iOS native libraries copied successfully");
        }

        private void ConfigureIOSNativeLibrariesInCMake(ref string content, string projectName)
        {
            // Build the framework lists for iOS
            var frameworkList = new List<string>();
            var frameworkLinks = new List<string>();

            string sanitizedProjectName = projectName.Replace("_", "-");

            // Always include the required frameworks
            frameworkList.Add("${FRAMEWORK_DIR}/sokol.framework");
            frameworkList.Add($"${{FRAMEWORK_DIR}}/{sanitizedProjectName}.framework");
            
            frameworkLinks.Add("\"-framework sokol\"");
            frameworkLinks.Add($"\"-framework {sanitizedProjectName}\"");

            // Add detected native libraries
            foreach (var library in iOSNativeLibraries)
            {
                string libraryName = library.Key;
                string libraryPath = library.Value;

                if (!Directory.Exists(libraryPath))
                {
                    continue;
                }

                // Look for .framework directories in the library path
                string[] frameworks = Directory.GetDirectories(libraryPath, "*.framework", SearchOption.TopDirectoryOnly);
                
                foreach (string frameworkDir in frameworks)
                {
                    string frameworkName = Path.GetFileNameWithoutExtension(frameworkDir); // Remove .framework extension
                    frameworkList.Add($"${{FRAMEWORK_DIR}}/{frameworkName}.framework");
                    frameworkLinks.Add($"\"-framework {frameworkName}\"");
                }
            }

            // Replace placeholders in CMakeLists.txt
            string embedFrameworksList = string.Join(";", frameworkList);
            string frameworkLinksList = string.Join("\n    ", frameworkLinks);

            content = content.Replace("TEMPLATE_EMBED_FRAMEWORKS_LIST", embedFrameworksList);
            content = content.Replace("TEMPLATE_FRAMEWORK_LINKS", frameworkLinksList);

            if (iOSNativeLibraries.Count > 0)
            {
                Log.LogMessage(MessageImportance.High, $"📋 Configured {iOSNativeLibraries.Count} iOS native libraries in CMakeLists.txt");
                foreach (var library in iOSNativeLibraries)
                {
                    Log.LogMessage(MessageImportance.High, $"   - {library.Key}");
                }
            }
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }

        private void ProcessIOSIcon(string iosDir, string projectDir)
        {
            if (string.IsNullOrWhiteSpace(iOSIcon))
            {
                Log.LogMessage(MessageImportance.Normal, "ℹ️  No IOSIcon specified in Directory.Build.props, using default icon");
                return;
            }

            // Find the icon file
            string sourceIconPath = FindIconFile(iOSIcon, projectDir);
            if (string.IsNullOrEmpty(sourceIconPath) || !File.Exists(sourceIconPath))
            {
                Log.LogWarning($"⚠️  iOS icon not found: {iOSIcon}");
                return;
            }

            Log.LogMessage(MessageImportance.High, $"📱 Processing iOS icon: {Path.GetFileName(sourceIconPath)}");

            try
            {
                // Create Assets.xcassets directory structure
                string assetsDir = Path.Combine(iosDir, "Assets.xcassets");
                string appIconDir = Path.Combine(assetsDir, "AppIcon.appiconset");
                Directory.CreateDirectory(appIconDir);

                // iOS icon sizes (iPhone and iPad)
                var iconSizes = new List<(string name, int size, string idiom, string scale)>
                {
                    // iPhone
                    ("icon-20@2x.png", 40, "iphone", "2x"),
                    ("icon-20@3x.png", 60, "iphone", "3x"),
                    ("icon-29@2x.png", 58, "iphone", "2x"),
                    ("icon-29@3x.png", 87, "iphone", "3x"),
                    ("icon-40@2x.png", 80, "iphone", "2x"),
                    ("icon-40@3x.png", 120, "iphone", "3x"),
                    ("icon-60@2x.png", 120, "iphone", "2x"),
                    ("icon-60@3x.png", 180, "iphone", "3x"),
                    
                    // iPad
                    ("icon-20.png", 20, "ipad", "1x"),
                    ("icon-20@2x-ipad.png", 40, "ipad", "2x"),
                    ("icon-29.png", 29, "ipad", "1x"),
                    ("icon-29@2x-ipad.png", 58, "ipad", "2x"),
                    ("icon-40.png", 40, "ipad", "1x"),
                    ("icon-40@2x-ipad.png", 80, "ipad", "2x"),
                    ("icon-76.png", 76, "ipad", "1x"),
                    ("icon-76@2x.png", 152, "ipad", "2x"),
                    ("icon-83.5@2x.png", 167, "ipad", "2x"),
                    
                    // App Store
                    ("icon-1024.png", 1024, "ios-marketing", "1x")
                };

                // Generate Contents.json
                var contentsJson = new StringBuilder();
                contentsJson.AppendLine("{");
                contentsJson.AppendLine("  \"images\" : [");

                for (int i = 0; i < iconSizes.Count; i++)
                {
                    var icon = iconSizes[i];
                    string destIcon = Path.Combine(appIconDir, icon.name);
                    
                    // Resize and save icon
                    ResizeImage(sourceIconPath, destIcon, icon.size, icon.size);
                    
                    // Extract size from icon size
                    string sizeStr = icon.size < 100 
                        ? $"{icon.size}x{icon.size}" 
                        : icon.size == 1024 
                            ? "1024x1024" 
                            : $"{icon.size / (icon.scale == "2x" ? 2 : icon.scale == "3x" ? 3 : 1)}x{icon.size / (icon.scale == "2x" ? 2 : icon.scale == "3x" ? 3 : 1)}";
                    
                    contentsJson.AppendLine("    {");
                    contentsJson.AppendLine($"      \"filename\" : \"{icon.name}\",");
                    contentsJson.AppendLine($"      \"idiom\" : \"{icon.idiom}\",");
                    contentsJson.AppendLine($"      \"scale\" : \"{icon.scale}\",");
                    contentsJson.AppendLine($"      \"size\" : \"{sizeStr}\"");
                    contentsJson.Append("    }");
                    contentsJson.AppendLine(i < iconSizes.Count - 1 ? "," : "");
                    
                    Log.LogMessage(MessageImportance.Normal, $"   ✅ Created {icon.name} ({icon.size}x{icon.size})");
                }

                contentsJson.AppendLine("  ],");
                contentsJson.AppendLine("  \"info\" : {");
                contentsJson.AppendLine("    \"author\" : \"xcode\",");
                contentsJson.AppendLine("    \"version\" : 1");
                contentsJson.AppendLine("  }");
                contentsJson.AppendLine("}");

                // Write Contents.json
                string contentsJsonPath = Path.Combine(appIconDir, "Contents.json");
                File.WriteAllText(contentsJsonPath, contentsJson.ToString());

                Log.LogMessage(MessageImportance.High, "✅ iOS icon processed successfully");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"⚠️  Failed to process iOS icon: {ex.Message}");
            }
        }

        private string FindIconFile(string iconPath, string projectDir)
        {
            // If it's already an absolute path and exists, use it
            if (Path.IsPathRooted(iconPath) && File.Exists(iconPath))
            {
                return iconPath;
            }

            // Check in Assets folder first
            string assetsPath = Path.Combine(projectDir, "Assets", iconPath);
            if (File.Exists(assetsPath))
            {
                return assetsPath;
            }

            // Check relative to project path
            string relativePath = Path.Combine(projectDir, iconPath);
            if (File.Exists(relativePath))
            {
                return relativePath;
            }

            return null;
        }

        private void ResizeImage(string sourcePath, string destPath, int width, int height)
        {
            // First choice: Use SkiaSharp (pure C# - always available, cross-platform, high quality)
            try
            {
                if (ResizeImageWithSkiaSharp(sourcePath, destPath, width, height))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage(MessageImportance.Low, $"SkiaSharp image resizing failed: {ex.Message}");
            }

            // Fallback: Try ImageMagick 7+ with 'magick' command
            bool resized = false;
            try
            {
                var magickResult = Cli.Wrap("magick")
                    .WithArguments($"\"{sourcePath}\" -resize {width}x{height}! \"{destPath}\"")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (magickResult.ExitCode == 0)
                {
                    resized = true;
                    return;
                }
            }
            catch { }

            // Fallback: Try ImageMagick 6 with 'convert' command
            try
            {
                var convertResult = Cli.Wrap("convert")
                    .WithArguments($"\"{sourcePath}\" -resize {width}x{height}! \"{destPath}\"")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (convertResult.ExitCode == 0)
                {
                    resized = true;
                    return;
                }
            }
            catch { }

            // Fallback: Try sips (macOS only)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                try
                {
                    // Copy file first
                    File.Copy(sourcePath, destPath, true);
                    
                    var sipsResult = Cli.Wrap("sips")
                        .WithArguments($"-z {height} {width} \"{destPath}\"")
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteBufferedAsync()
                        .GetAwaiter()
                        .GetResult();

                    if (sipsResult.ExitCode == 0)
                    {
                        return;
                    }
                }
                catch { }
            }

            // Final fallback: Copy original
            File.Copy(sourcePath, destPath, true);
            Log.LogWarning($"⚠️  All image resizing methods failed. Copied original for {Path.GetFileName(destPath)}");
        }

        private bool ResizeImageWithSkiaSharp(string sourcePath, string destPath, int width, int height)
        {
            // Load the source image
            using var inputStream = File.OpenRead(sourcePath);
            using var original = SkiaSharp.SKBitmap.Decode(inputStream);
            
            if (original == null)
            {
                Log.LogWarning($"   ⚠️  Failed to decode image: {sourcePath}");
                return false;
            }

            // Calculate dimensions to maintain aspect ratio and fill the target size
            int srcWidth = original.Width;
            int srcHeight = original.Height;
            float srcAspect = (float)srcWidth / srcHeight;
            float targetAspect = (float)width / height;

            int cropWidth, cropHeight, cropX, cropY;
            
            if (Math.Abs(srcAspect - targetAspect) < 0.01f)
            {
                // Aspect ratios are similar, use full image
                cropWidth = srcWidth;
                cropHeight = srcHeight;
                cropX = 0;
                cropY = 0;
            }
            else if (srcAspect > targetAspect)
            {
                // Source is wider, crop width
                cropHeight = srcHeight;
                cropWidth = (int)(srcHeight * targetAspect);
                cropX = (srcWidth - cropWidth) / 2;
                cropY = 0;
            }
            else
            {
                // Source is taller, crop height
                cropWidth = srcWidth;
                cropHeight = (int)(srcWidth / targetAspect);
                cropX = 0;
                cropY = (srcHeight - cropHeight) / 2;
            }

            // Create cropped bitmap
            using var cropped = new SkiaSharp.SKBitmap(cropWidth, cropHeight);
            using var canvas = new SkiaSharp.SKCanvas(cropped);
            
            var srcRect = new SkiaSharp.SKRect(cropX, cropY, cropX + cropWidth, cropY + cropHeight);
            var destRect = new SkiaSharp.SKRect(0, 0, cropWidth, cropHeight);
            
            canvas.DrawBitmap(original, srcRect, destRect, new SkiaSharp.SKPaint
            {
                IsAntialias = true,
                FilterQuality = SkiaSharp.SKFilterQuality.High
            });

            // Resize to target size with high-quality sampling
            var imageInfo = new SkiaSharp.SKImageInfo(width, height, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul);
            var samplingOptions = new SkiaSharp.SKSamplingOptions(SkiaSharp.SKCubicResampler.CatmullRom);
            using var resized = cropped.Resize(imageInfo, samplingOptions);
            
            if (resized == null)
            {
                Log.LogWarning($"   ⚠️  Failed to resize image to {width}x{height}");
                return false;
            }

            // Save as PNG
            using var image = SkiaSharp.SKImage.FromBitmap(resized);
            using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            using var outputStream = File.OpenWrite(destPath);
            data.SaveTo(outputStream);

            return true;
        }

        private void CopyToOutputPath(string iosDir, string projectName, string buildType)
        {
            try
            {
                string sanitizedProjectName = projectName.Replace("_", "-");
                string buildDir = Path.Combine(iosDir, "build-xcode-ios-app");
                
                // Determine configuration based on build type
                string configuration = string.IsNullOrEmpty(buildType) || buildType.Equals("release", StringComparison.OrdinalIgnoreCase) 
                    ? "Release" 
                    : "Debug";
                
                string xcodeBuildConfig = $"{configuration}-iphoneos";
                string appBundlePath = Path.Combine(buildDir, xcodeBuildConfig, $"{sanitizedProjectName}-ios-app.app");

                // Check alternate locations
                if (!Directory.Exists(appBundlePath))
                {
                    string altPath = Path.Combine(buildDir, "bin", configuration, $"{sanitizedProjectName}-ios-app.app");
                    if (Directory.Exists(altPath))
                    {
                        appBundlePath = altPath;
                    }
                }

                // Also check the other configuration as fallback
                if (!Directory.Exists(appBundlePath))
                {
                    string fallbackConfig = configuration == "Release" ? "Debug" : "Release";
                    string fallbackPath = Path.Combine(buildDir, $"{fallbackConfig}-iphoneos", $"{sanitizedProjectName}-ios-app.app");
                    if (Directory.Exists(fallbackPath))
                    {
                        appBundlePath = fallbackPath;
                    }
                }

                if (!Directory.Exists(appBundlePath))
                {
                    Log.LogWarning($"App bundle not found, skipping output copy.");
                    return;
                }

                // Determine output base path: use custom path if specified, otherwise use project's output folder
                string outputBasePath = string.IsNullOrEmpty(opts.OutputPath) 
                    ? Path.Combine(opts.ProjectPath, "output") 
                    : opts.OutputPath;

                // Create output directory: {basePath}/iOS/{buildType}/
                string outputDir = Path.Combine(outputBasePath, "iOS", buildType);
                Directory.CreateDirectory(outputDir);

                // Copy .app bundle with descriptive name
                string outputAppBundle = Path.Combine(outputDir, $"{projectName}-{buildType}.app");
                
                // Remove existing bundle if present
                if (Directory.Exists(outputAppBundle))
                {
                    Directory.Delete(outputAppBundle, true);
                }

                // Copy the entire .app bundle directory
                CopyDirectory(appBundlePath, outputAppBundle);

                Log.LogMessage(MessageImportance.High, $"✅ iOS app bundle copied to: {outputAppBundle}");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to copy iOS app bundle to output path: {ex.Message}");
            }
        }
    }
}