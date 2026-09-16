# Build sokol_speech.dll for Windows — standalone Speech plugin library (C++/WinRT).
# Output: plugins\Speech\libs\windows\<arch>\release\sokol_speech.dll
#
# Usage: .\plugins\Speech\scripts\build-windows.ps1 [-BuildType Release] [-Architecture x64]

param(
    [string]$BuildType    = "Release",
    [string]$Architecture = "x64"
)

$ErrorActionPreference = "Stop"

$ScriptDir    = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot     = Resolve-Path (Join-Path $ScriptDir "..\..\..")
$PluginNative = Join-Path $RepoRoot "plugins\Speech\native"
$BuildDir     = Join-Path $RepoRoot "build-sokol-speech-windows-$Architecture"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "sokol_speech — Windows ($Architecture / $BuildType)" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

if (Test-Path $BuildDir) { Remove-Item -Recurse -Force $BuildDir }

# Don't pin the Visual Studio generator: let CMake pick the installed one; -A selects the platform.
cmake -S $PluginNative -B $BuildDir -A $Architecture
cmake --build $BuildDir --config $BuildType

Remove-Item -Recurse -Force $BuildDir

Write-Host "  -> plugins\Speech\libs\windows\$Architecture\$($BuildType.ToLower())\sokol_speech.dll"
