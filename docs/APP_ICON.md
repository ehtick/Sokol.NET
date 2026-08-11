# Application Icon Configuration

This document explains how to configure custom application icons for Android, iOS, Desktop (Windows/macOS/Linux), and Web deployments.

## Overview

The SokolApplicationBuilder now supports custom application icons that can be specified in `Directory.Build.props`. The icon will be automatically processed and resized to all required platform-specific sizes during the build process.

## Icon File Requirements

### Recommended Specifications
- **Format**: PNG (recommended), JPEG also supported
- **Size**: At least 1024x1024 pixels for best quality
- **Aspect Ratio**: Square (1:1)
- **Color Space**: RGB or RGBA
- **Location**: Place in the `Assets` folder of your project

### Image Processing Tools

The build system uses the following tools for image resizing (in order of preference):

1. **ImageMagick** (cross-platform)
   - `magick` command (ImageMagick 7+)
   - `convert` command (ImageMagick 6)

2. **sips** (macOS only)
   - Built-in macOS tool

If no image processing tool is available, the original icon will be copied without resizing (not recommended).

#### Installing ImageMagick

**macOS** (using Homebrew):
```bash
brew install imagemagick
```

**Linux** (Ubuntu/Debian):
```bash
sudo apt-get install imagemagick
```

**Windows** (using Chocolatey):
```bash
choco install imagemagick
```

## Configuration

### Android Icon

Add the following property to your `Directory.Build.props` file under the Android PropertyGroup:

```xml
<PropertyGroup>
    <!-- Other Android properties... -->
    
    <!-- App Icon (path relative to project root, or just filename if in Assets folder) -->
    <AndroidIcon>logo_full_large.png</AndroidIcon>
</PropertyGroup>
```

#### Android Icon Sizes Generated

The build system automatically generates the following icon sizes for Android:

| Density | Size | Use Case |
|---------|------|----------|
| mdpi | 48x48 | Low density screens |
| hdpi | 72x72 | Medium density screens |
| xhdpi | 96x96 | High density screens |
| xxhdpi | 144x144 | Extra high density screens |
| xxxhdpi | 192x192 | Extra extra high density screens |

All icons are saved as `ic_launcher.png` in their respective `mipmap-{density}` folders.

### iOS Icon

Add the following property to your `Directory.Build.props` file under the iOS PropertyGroup:

```xml
<PropertyGroup>
    <!-- Other iOS properties... -->
    
    <!-- App Icon (path relative to project root, or just filename if in Assets folder) -->
    <IOSIcon>logo_full_large.png</IOSIcon>
</PropertyGroup>
```

#### iOS Icon Sizes Generated

The build system automatically generates all required iOS icon sizes:

**iPhone Icons:**
- 40x40 (@2x for 20pt)
- 60x60 (@3x for 20pt)
- 58x58 (@2x for 29pt)
- 87x87 (@3x for 29pt)
- 80x80 (@2x for 40pt)
- 120x120 (@3x for 40pt, @2x for 60pt)
- 180x180 (@3x for 60pt)

**iPad Icons:**
- 20x20 (@1x for 20pt)
- 40x40 (@2x for 20pt)
- 29x29 (@1x for 29pt)
- 58x58 (@2x for 29pt)
- 40x40 (@1x for 40pt)
- 80x80 (@2x for 40pt)
- 76x76 (@1x for 76pt)
- 152x152 (@2x for 76pt)
- 167x167 (@2x for 83.5pt)

**App Store:**
- 1024x1024 (App Store icon)

All icons are organized in an `Assets.xcassets/AppIcon.appiconset` directory with a proper `Contents.json` manifest.

### Desktop Icon

Add the following property to your `Directory.Build.props` file under the Desktop PropertyGroup:

```xml
<PropertyGroup>
    <!-- Other Desktop properties... -->
    
    <!-- App Icon (path relative to project root, or just filename if in Assets folder) -->
    <DesktopIcon>logo_full_large.png</DesktopIcon>
</PropertyGroup>
```

#### Desktop Icon Formats Generated

The build system automatically generates platform-specific icon formats:

**Windows (.ico format):**
- Multi-size ICO file containing: 16x16, 32x32, 48x48, 64x64, 128x128, 256x256
- Saved as `app.ico` in the output directory
- Requires ImageMagick for proper multi-size ICO generation

**macOS (.icns format):**
- ICNS file containing all standard sizes
- Generated sizes: 16x16, 32x32, 64x64, 128x128, 256x256, 512x512, 1024x1024 (@1x and @2x variants)
- Saved as `app.icns` in the output directory
- Uses macOS `iconutil` command to convert .iconset to .icns
- Requires ImageMagick or sips for resizing individual icon files

**Linux (PNG files):**
- Multiple PNG sizes: 16x16, 22x22, 24x24, 32x32, 48x48, 64x64, 128x128, 256x256, 512x512
- Saved as individual PNG files: `icon_16.png`, `icon_32.png`, etc.
- Can be used with freedesktop.org icon standards
- Requires ImageMagick or sips for resizing

### Web Icon

Add the following property to your `Directory.Build.props` file under the Web PropertyGroup:

```xml
<PropertyGroup>
    <!-- Other Web properties... -->
    
    <!-- App Icon (path relative to project root, or just filename if in Assets folder) -->
    <WebIcon>logo_full_large.png</WebIcon>
</PropertyGroup>
```

#### Web Icon Files Generated

The build system automatically generates web-standard icon files:

**Favicon:**
- `favicon.ico` - Multi-size ICO containing 48x48, 32x32, 16x16
- Falls back to PNG if ICO generation fails
- Used by browsers for bookmarks and tabs

**Apple Touch Icons (for iOS/macOS Safari):**
- `apple-touch-icon-180x180.png` - iPhone (@3x)
- `apple-touch-icon-167x167.png` - iPad Pro
- `apple-touch-icon-152x152.png` - iPad (@2x)
- `apple-touch-icon-120x120.png` - iPhone (@2x)

**PWA Manifest Icons:**
- `icon-192x192.png` - Standard icon for manifest
- `icon-512x512.png` - Large icon for splash screens

**Web Manifest:**
- `manifest.json` - Progressive Web App manifest
- Contains icon references and app metadata
- Enables "Add to Home Screen" functionality

## Example Configuration

Here's a complete example showing both Android and iOS icon configuration:

```xml
<Project>
   <PropertyGroup>
      <!-- Project settings... -->
   </PropertyGroup>

   <!-- Android Configuration -->
   <PropertyGroup>
      <AndroidMinSdkVersion>26</AndroidMinSdkVersion>
      <AndroidTargetSdkVersion>36</AndroidTargetSdkVersion>
      <AndroidFullscreen>true</AndroidFullscreen>
      <AndroidScreenOrientation>landscape</AndroidScreenOrientation>
      
      <!-- App Icon -->
      <AndroidIcon>logo_full_large.png</AndroidIcon>
   </PropertyGroup>

   <!-- iOS Configuration -->
   <PropertyGroup>
      <IOSMinVersion>14.0</IOSMinVersion>
      <IOSScreenOrientation>landscape</IOSScreenOrientation>
      <IOSStatusBarHidden>true</IOSStatusBarHidden>
      
      <!-- App Icon -->
      <IOSIcon>logo_full_large.png</IOSIcon>
   </PropertyGroup>

   <!-- Desktop Configuration -->
   <PropertyGroup>
      <!-- App Icon -->
      <DesktopIcon>logo_full_large.png</DesktopIcon>
   </PropertyGroup>

   <!-- Web Configuration -->
   <PropertyGroup>
      <!-- App Icon -->
      <WebIcon>logo_full_large.png</WebIcon>
   </PropertyGroup>
</Project>
```

## Icon File Path Resolution

The build system searches for the icon file in the following order:

1. **Absolute path**: If the path is absolute and the file exists, it will be used directly
2. **Assets folder**: `{ProjectPath}/Assets/{IconPath}`
3. **Relative to project**: `{ProjectPath}/{IconPath}`

### Examples

```xml
<!-- Just filename - looks in Assets folder -->
<AndroidIcon>myicon.png</AndroidIcon>
<!-- Result: {ProjectPath}/Assets/myicon.png -->

<!-- Relative path from Assets folder -->
<AndroidIcon>icons/myicon.png</AndroidIcon>
<!-- Result: {ProjectPath}/Assets/icons/myicon.png -->

<!-- Relative to project root -->
<AndroidIcon>../shared/icon.png</AndroidIcon>
<!-- Result: {ProjectPath}/../shared/icon.png -->

<!-- Absolute path -->
<AndroidIcon>/Users/username/icons/myicon.png</AndroidIcon>
<!-- Result: /Users/username/icons/myicon.png -->
```

## Pre-made icon SETS (a directory instead of a single PNG)

`AndroidIcon` and `IOSIcon` also accept a **directory**, which is copied **verbatim** instead of being
re-rendered. Use this when you already generate a full icon set (adaptive icons, an `.appiconset`) from
a vector source — re-rendering could only degrade it.

The same three resolution rules apply (absolute → `Assets/` → project-relative), so the two forms are
interchangeable. **A file keeps the original behaviour exactly** — existing projects are unaffected.

```xml
<AndroidIcon>docs/branding/icons/android</AndroidIcon>   <!-- a directory -> copied verbatim -->
<IOSIcon>docs/branding/icons/ios</IOSIcon>               <!-- .appiconset, or a folder holding one -->
```

### Android set layout
Every `mipmap-*` / `drawable-*` folder is copied into `app/src/main/res/`:

```
android/
  mipmap-anydpi-v26/  ic_launcher.xml, ic_launcher_round.xml   <- adaptive icon (API 26+)
  mipmap-{m,h,x,xx,xxx}dpi/
      ic_launcher.png              legacy square icon (48dp canvas)
      ic_launcher_foreground.png   adaptive foreground (108dp canvas)
      ic_launcher_background.png   adaptive background (108dp canvas)
```
Loose files at the set root (e.g. `playstore-icon-512.png`, a README) are **not** copied — a Play
Console listing icon is uploaded separately and would only bloat the APK.

`android:roundIcon` is added to the manifest **only** when the set actually provides an
`ic_launcher_round` resource; the single-PNG path never produces one, and referencing a missing mipmap
fails the resource link.

> ⛔ **The adaptive FOREGROUND must be transparent apart from the art.** If it is exported opaque (white
> padding baked in) it completely covers the `<background>` layer, and every launcher shows a white icon
> no matter what colour the background PNG is. Keep the art inside the ~66dp safe zone of the 108dp
> canvas so circle/squircle masks cannot clip it.

### iOS set layout
Point `IOSIcon` at an `*.appiconset` (or a folder containing exactly one). It is copied to
`Assets.xcassets/AppIcon.appiconset` with its own `Contents.json`, and the destination is cleared first
so a stale icon from a previous build cannot survive. The marketing 1024 icon must be **opaque** (no
alpha) or App Store validation rejects it.

## Build Output

When building with icon support, you'll see output like this:

### Android
```
📱 Processing Android icon: logo_full_large.png
   ✅ Created mipmap-mdpi/ic_launcher.png (48x48)
   ✅ Created mipmap-hdpi/ic_launcher.png (72x72)
   ✅ Created mipmap-xhdpi/ic_launcher.png (96x96)
   ✅ Created mipmap-xxhdpi/ic_launcher.png (144x144)
   ✅ Created mipmap-xxxhdpi/ic_launcher.png (192x192)
✅ Android icon processed successfully
```

### iOS
```
📱 Processing iOS icon: logo_full_large.png
   ✅ Created icon-20@2x.png (40x40)
   ✅ Created icon-20@3x.png (60x60)
   ... (additional sizes)
   ✅ Created icon-1024.png (1024x1024)
✅ iOS icon processed successfully
```

### Desktop
```
🖥️  Processing Desktop icon: logo_full_large.png
   Platform: windows (win-x64)
   ✅ Created app.ico (256x256, 128x128, 64x64, 48x48, 32x32, 16x16)
✅ Desktop icon processed successfully
```

or

```
🖥️  Processing Desktop icon: logo_full_large.png
   Platform: macOS (osx-arm64)
   ✅ Created icon_16.png (16x16)
   ✅ Created icon_16@2x.png (32x32)
   ... (additional sizes)
   ✅ Created icon_512@2x.png (1024x1024)
   ✅ Created app.icns from iconset
✅ Desktop icon processed successfully
```

or

```
🖥️  Processing Desktop icon: logo_full_large.png
   Platform: Linux (linux-x64)
   ✅ Created icon_16.png (16x16)
   ✅ Created icon_32.png (32x32)
   ... (additional sizes)
   ✅ Created icon_512.png (512x512)
✅ Desktop icon processed successfully
```

### Web
```
🌐 Processing Web icon: logo_full_large.png
   ✅ Created favicon.ico (48x48, 32x32, 16x16)
   ✅ Created apple-touch-icon-180x180.png
   ✅ Created apple-touch-icon-167x167.png
   ✅ Created apple-touch-icon-152x152.png
   ✅ Created apple-touch-icon-120x120.png
   ✅ Created icon-192x192.png
   ✅ Created icon-512x512.png
   ✅ Created manifest.json
✅ Web icon processed successfully
```

## Troubleshooting

### Warning: Image resizing tools not found

If you see this warning:
```
⚠️  Image resizing tools not found (ImageMagick or sips). Copying original icon.
```

**Solution**: Install ImageMagick (recommended) or use macOS sips:
```bash
# macOS
brew install imagemagick

# Linux
sudo apt-get install imagemagick

# Windows
choco install imagemagick
```

### Warning: Icon not found

If you see:
```
⚠️  Android icon not found: myicon.png
```

**Solutions**:
1. Verify the icon file exists in the `Assets` folder
2. Check the filename spelling (case-sensitive on Linux/macOS)
3. Use an absolute path for testing
4. Check the file extension matches (.png, .jpg, etc.)

### No icon specified

If you see:
```
ℹ️  No AndroidIcon specified in Directory.Build.props, using default icon
```

This is informational - the build will use default template icons. Add the icon property to `Directory.Build.props` to use a custom icon.

## Default Behavior

If no icon is specified in `Directory.Build.props`, the build system will use the default icons included in the templates:

- **Android**: Default blue/green icons in each mipmap density folder
- **iOS**: No custom icon (uses iOS default placeholder)

## Advanced Usage

### Using Different Icons for Each Platform

You can specify different icons for each platform:

```xml
<!-- Android PropertyGroup -->
<PropertyGroup>
    <AndroidIcon>android_icon.png</AndroidIcon>
</PropertyGroup>

<!-- iOS PropertyGroup -->
<PropertyGroup>
    <IOSIcon>ios_icon.png</IOSIcon>
</PropertyGroup>

<!-- Desktop PropertyGroup -->
<PropertyGroup>
    <DesktopIcon>desktop_icon.png</DesktopIcon>
</PropertyGroup>

<!-- Web PropertyGroup -->
<PropertyGroup>
    <WebIcon>web_icon.png</WebIcon>
</PropertyGroup>
```

### Testing Icon Changes

After changing the icon in `Directory.Build.props`:

1. Clean the build output:
   ```bash
   rm -rf Android/  # For Android
   rm -rf ios/      # For iOS
   ```

2. Rebuild the app:
   ```bash
   dotnet run --project path/to/SokolApplicationBuilder -- --task build --architecture android
   dotnet run --project path/to/SokolApplicationBuilder -- --task build --architecture ios
   dotnet run --project path/to/SokolApplicationBuilder -- --task build --architecture desktop --rid osx-arm64
   dotnet run --project path/to/SokolApplicationBuilder -- --task build --architecture web
   ```

3. Install and verify on device or test locally

## Best Practices

1. **Use high-resolution source images** (1024x1024 or larger) for best quality
2. **Square images work best** - maintain 1:1 aspect ratio
3. **Avoid text in icons** at small sizes (text becomes unreadable)
4. **Use simple, bold designs** that work well at all sizes
5. **Test on actual devices** to verify icon appearance
6. **Keep icons in the Assets folder** for better project organization
7. **Use PNG format** for transparency support
8. **Install ImageMagick** for best cross-platform compatibility

## Desktop and Web Icon Configuration

For desktop (Windows/macOS/Linux) and web applications, you have **multiple approaches** to configure icons:

### Quick Setup (Automatic Generation)

The easiest approach - add to your `Directory.Build.props`:

```xml
<!-- Desktop Icon -->
<PropertyGroup>
    <DesktopIcon>logo_full_large.png</DesktopIcon>
</PropertyGroup>

<!-- Web Icon -->
<PropertyGroup>
    <WebIcon>logo_full_large.png</WebIcon>
</PropertyGroup>
```

Build with:
```bash
# Desktop
dotnet run --project tools/SokolApplicationBuilder -- --task build --architecture desktop --rid osx-arm64 --path /path/to/project

# Web
dotnet run --project tools/SokolApplicationBuilder -- --task build --architecture web --path /path/to/project
```

### Alternative Approaches

Desktop and Web icons support **three different configuration methods**:

1. **Automatic Generation** - Let the build system generate all icon formats (recommended for quick setup)
2. **Manual .csproj Configuration** - Use .NET's built-in icon handling (recommended for production)
3. **Pre-generated Icons** - Provide ready-made icon files (recommended for design-heavy projects)

**📖 See the comprehensive guide:** [Desktop and Web Icon Configuration Guide](DESKTOP_WEB_ICONS.md)

This guide covers:
- Detailed setup for all three approaches
- Platform-specific requirements
- Icon format specifications
- Troubleshooting tips
- Best practices

---

## Summary: All Platform Icon Support

| Platform | Property | Automatic Generation | Manual .csproj | Pre-generated |
|----------|----------|---------------------|----------------|---------------|
| **Android** | `<AndroidIcon>` | ✅ 5 densities | N/A | ✅ Copy to mipmap |
| **iOS** | `<IOSIcon>` | ✅ 18 sizes + asset catalog | N/A | ✅ Copy to Assets.xcassets |
| **Windows** | `<DesktopIcon>` | ✅ Multi-size .ico | ✅ ApplicationIcon | ✅ Copy to output |
| **macOS** | `<DesktopIcon>` | ✅ .icns bundle | ✅ BundleResource | ✅ Copy to Resources |
| **Linux** | `<DesktopIcon>` | ✅ Multiple PNGs | ✅ .desktop file | ✅ Install to icons dir |
| **Web** | `<WebIcon>` | ✅ Favicon + PWA | ✅ wwwroot icons | ✅ Copy to wwwroot |

### Quick Reference

**All platforms with automatic generation:**
```xml
<Project>
  <PropertyGroup>
    <AndroidIcon>myicon.png</AndroidIcon>
    <IOSIcon>myicon.png</IOSIcon>
    <DesktopIcon>myicon.png</DesktopIcon>
    <WebIcon>myicon.png</WebIcon>
  </PropertyGroup>
</Project>
```

Place `myicon.png` (1024x1024, PNG format) in your `Assets/` folder - done! 🎉

## See Also

- [Desktop and Web Icon Configuration Guide](DESKTOP_WEB_ICONS.md) - Comprehensive guide with all approaches
- [Android Properties Documentation](ANDROID_PROPERTIES.md)
- [iOS Properties Documentation](IOS_PROPERTIES.md)
- [Directory.Build.props Configuration](../examples/cube/Directory.Build.props)
