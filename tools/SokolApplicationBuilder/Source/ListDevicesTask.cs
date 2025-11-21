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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using CliWrap;
using CliWrap.Buffered;

namespace SokolApplicationBuilder
{
    public class ListDevicesTask : Microsoft.Build.Utilities.Task
    {
        private Options options;

        public ListDevicesTask(Options opts)
        {
            options = opts;
        }

        public override bool Execute()
        {
            try
            {
                if (options.Arch == "android")
                {
                    return ListAndroidDevices();
                }
                else if (options.Arch == "ios")
                {
                    return ListIOSDevices();
                }
                else
                {
                    Log.LogError($"ERROR: Unknown architecture '{options.Arch}'. Use 'android' or 'ios'.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"ERROR: Failed to list devices: {ex.Message}");
                return false;
            }
        }

        private bool ListAndroidDevices()
        {
            try
            {
                // Check if adb is available
                string adbPath = FindAdb();
                if (string.IsNullOrEmpty(adbPath))
                {
                    Log.LogError("ERROR: adb not found!");
                    Log.LogError("Please install Android SDK and ensure adb is in your PATH.");
                    return false;
                }

                Log.LogMessage(MessageImportance.High, "🔍 Checking for connected Android devices...");
                Log.LogMessage(MessageImportance.High, "");

                // Run adb devices
                var result = Cli.Wrap(adbPath)
                    .WithArguments("devices")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                // Parse output
                var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var devices = new List<(string id, string status)>();

                foreach (var line in lines)
                {
                    if (line.Contains("List of devices") || string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = Regex.Split(line.Trim(), @"\s+");
                    if (parts.Length >= 2)
                    {
                        devices.Add((parts[0], parts[1]));
                    }
                }

                // Filter only connected devices
                var connectedDevices = devices.Where(d => d.status == "device").ToList();

                if (connectedDevices.Count == 0)
                {
                    Log.LogMessage(MessageImportance.High, "❌ No connected Android devices found.");
                    Log.LogMessage(MessageImportance.High, "Please connect an Android device and enable USB debugging.");
                    return true; // Not an error, just no devices
                }

                Log.LogMessage(MessageImportance.High, "📱 Connected Android devices:");
                Log.LogMessage(MessageImportance.High, "=========================");

                // Get device details
                foreach (var device in connectedDevices)
                {
                    string model = GetDeviceProperty(adbPath, device.id, "ro.product.model");
                    string manufacturer = GetDeviceProperty(adbPath, device.id, "ro.product.manufacturer");

                    if (!string.IsNullOrEmpty(model) && !string.IsNullOrEmpty(manufacturer))
                    {
                        Log.LogMessage(MessageImportance.High, $"Device ID: {device.id} ({manufacturer} {model})");
                    }
                    else
                    {
                        Log.LogMessage(MessageImportance.High, $"Device ID: {device.id}");
                    }
                }

                Log.LogMessage(MessageImportance.High, "");
                Log.LogMessage(MessageImportance.High, "To use a specific device, copy the Device ID and use it with --device parameter.");

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"ERROR: Failed to list Android devices: {ex.Message}");
                return false;
            }
        }

        private bool ListIOSDevices()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Log.LogError("ERROR: iOS device listing is only supported on macOS.");
                return false;
            }

            try
            {
                Log.LogMessage(MessageImportance.High, "🔍 Checking for iOS devices and simulators...");
                Log.LogMessage(MessageImportance.High, "");

                // Check for ios-deploy
                bool hasIOSDeploy = CheckCommandExists("ios-deploy");

                if (!hasIOSDeploy)
                {
                    Log.LogWarning("⚠️  ios-deploy not found!");
                    Log.LogWarning("Install with: brew install ios-deploy");
                    Log.LogWarning("Note: ios-deploy requires Xcode command line tools.");
                    Log.LogMessage(MessageImportance.High, "");
                }

                // List physical devices if ios-deploy is available
                if (hasIOSDeploy)
                {
                    Log.LogMessage(MessageImportance.High, "📱 Physical iOS Devices:");
                    Log.LogMessage(MessageImportance.High, "======================");

                    var deviceResult = Cli.Wrap("ios-deploy")
                        .WithArguments(new[] { "--detect", "--no-wifi" })
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteBufferedAsync()
                        .GetAwaiter()
                        .GetResult();

                    var deviceLines = deviceResult.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    var foundDevices = false;

                    foreach (var line in deviceLines)
                    {
                        if (line.Contains("Found "))
                        {
                            var deviceInfo = line.Replace("[...] Found ", "").Trim();
                            Log.LogMessage(MessageImportance.High, $"Device: {deviceInfo}");
                            foundDevices = true;
                        }
                    }

                    if (!foundDevices)
                    {
                        Log.LogMessage(MessageImportance.High, "No physical iOS devices connected.");
                    }

                    Log.LogMessage(MessageImportance.High, "");
                }

                // List simulators
                Log.LogMessage(MessageImportance.High, "📱 iOS Simulators:");
                Log.LogMessage(MessageImportance.High, "==================");

                var simResult = Cli.Wrap("xcrun")
                    .WithArguments(new[] { "simctl", "list", "devices", "available" })
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                var simLines = simResult.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var foundSimulators = false;

                foreach (var line in simLines)
                {
                    if ((line.Contains("iPhone") || line.Contains("iPad")) && !line.Contains("unavailable"))
                    {
                        // Extract simulator ID and name
                        var match = Regex.Match(line, @"(iPhone|iPad)[^(]*\(([A-F0-9\-]+)\)");
                        if (match.Success)
                        {
                            var simName = match.Groups[0].Value.Split('(')[0].Trim();
                            var simId = match.Groups[2].Value;
                            Log.LogMessage(MessageImportance.High, $"Simulator ID: {simId} ({simName})");
                            foundSimulators = true;
                        }
                    }
                }

                if (!foundSimulators)
                {
                    Log.LogMessage(MessageImportance.High, "No iOS simulators available.");
                }

                Log.LogMessage(MessageImportance.High, "");
                Log.LogMessage(MessageImportance.High, "To use a specific device/simulator, copy the ID and use it with --ios-device parameter.");
                Log.LogMessage(MessageImportance.High, "");
                Log.LogMessage(MessageImportance.High, "Note: Physical devices require trust and pairing with Xcode.");
                Log.LogMessage(MessageImportance.High, "      Simulators can be launched directly.");

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"ERROR: Failed to list iOS devices: {ex.Message}");
                return false;
            }
        }

        private string FindAdb()
        {
            // Try common locations
            var paths = new List<string>
            {
                "adb", // System PATH
            };

            // Add Android SDK locations
            string androidSdk = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT") 
                ?? Environment.GetEnvironmentVariable("ANDROID_HOME");

            if (!string.IsNullOrEmpty(androidSdk))
            {
                paths.Add(Path.Combine(androidSdk, "platform-tools", "adb"));
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    paths.Add(Path.Combine(androidSdk, "platform-tools", "adb.exe"));
                }
            }

            // Try default locations
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                paths.Add(Path.Combine(localAppData, "Android", "Sdk", "platform-tools", "adb.exe"));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                paths.Add(Path.Combine(home, "Library", "Android", "sdk", "platform-tools", "adb"));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                paths.Add(Path.Combine(home, "Android", "Sdk", "platform-tools", "adb"));
            }

            foreach (var path in paths)
            {
                if (CheckCommandExists(path))
                {
                    return path;
                }
            }

            return null;
        }

        private bool CheckCommandExists(string command)
        {
            try
            {
                if (File.Exists(command))
                    return true;

                var result = Cli.Wrap(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which")
                    .WithArguments(command)
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                return result.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private string GetDeviceProperty(string adbPath, string deviceId, string property)
        {
            try
            {
                var result = Cli.Wrap(adbPath)
                    .WithArguments(new[] { "-s", deviceId, "shell", "getprop", property })
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                return result.StandardOutput.Trim().Replace("\r", "").Replace("\n", "");
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
