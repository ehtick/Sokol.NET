# Build script for camerac library on Windows (Media Foundation)
# Usage: .\build-camerac-windows.ps1 [-Architecture <arch>] [-BuildType <type>]
# Example: .\build-camerac-windows.ps1 -Architecture x64 -BuildType Release
# Architectures: x64, Win32, ARM64

param(
    [string]$Architecture = "x64",
    [string]$BuildType    = "Release"
)

$ErrorActionPreference = "Stop"

$ScriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$CameracDir = Join-Path $ScriptDir ".."
$BuildDir   = Join-Path $CameracDir "build-windows-$Architecture"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Building camerac for Windows"              -ForegroundColor Cyan
Write-Host "Architecture : $Architecture"              -ForegroundColor Cyan
Write-Host "Build Type   : $BuildType"                 -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

New-Item -ItemType Directory -Force -Path $BuildDir | Out-Null
Set-Location $BuildDir

Write-Host "Configuring CMake..." -ForegroundColor Yellow
# Don't hardcode the VS generator: the CI runner image's Visual Studio version moves over
# time (windows-latest was VS2022, the windows-2025 image now ships VS2026), and a stale
# "-G Visual Studio 17 2022" makes CMake fail with "could not find any instance of Visual
# Studio". Let CMake auto-select the installed Visual Studio; -A still picks the platform.
cmake .. `
    -A $Architecture `
    -DCMAKE_BUILD_TYPE="$BuildType" `
    -DBUILD_SHARED_LIBS=ON `
    -DCAMERAC_BUILD_SAMPLE=OFF

if ($LASTEXITCODE -ne 0) {
    Write-Host "X CMake configuration failed" -ForegroundColor Red
    exit 1
}

Write-Host "Building camerac..." -ForegroundColor Yellow
cmake --build . --config $BuildType --target camerac

if ($LASTEXITCODE -ne 0) {
    Write-Host "X Build failed" -ForegroundColor Red
    exit 1
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Build complete!"                            -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$BuildTypeLower = $BuildType.ToLower()
$DestDir = Join-Path $CameracDir "libs\windows\$Architecture\$BuildTypeLower"
New-Item -ItemType Directory -Force -Path $DestDir | Out-Null

# Find the built DLL
$DllPath = Get-ChildItem -Recurse -Path $BuildDir -Filter "camerac.dll" `
    | Where-Object { $_.FullName -like "*$BuildType*" } `
    | Select-Object -First 1

if ($null -eq $DllPath) {
    $DllPath = Get-ChildItem -Recurse -Path $BuildDir -Filter "camerac.dll" `
        | Select-Object -First 1
}

if ($null -ne $DllPath) {
    Copy-Item $DllPath.FullName -Destination (Join-Path $DestDir "camerac.dll")
    Write-Host "OK Copied camerac.dll to $DestDir" -ForegroundColor Green
} else {
    Write-Host "X camerac.dll not found" -ForegroundColor Red
    exit 1
}

# Also copy the import library
$LibPath = Get-ChildItem -Recurse -Path $BuildDir -Filter "camerac.lib" `
    | Where-Object { $_.FullName -like "*$BuildType*" } `
    | Select-Object -First 1

if ($null -ne $LibPath) {
    Copy-Item $LibPath.FullName -Destination (Join-Path $DestDir "camerac.lib")
    Write-Host "OK Copied camerac.lib to $DestDir" -ForegroundColor Green
}
