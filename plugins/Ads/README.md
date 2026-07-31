# sokol_ads — AdMob interstitials (Android + iOS)

Google Mobile Ads plugin for Sokol.NET apps: **Google Mobile Ads SDK 23 + UMP** on
Android, **GoogleMobileAds.xcframework 13** on iOS. Interstitials only; the max ad
content rating is pinned to **G** in both shims, and `npaOnly` requests carry the
non-personalized-ads flag on every request. Desktop and web have no ad SDK — the
managed layer stubs every call deterministically (`AdsPlugin.Available` is `false`).

## Model

All calls are non-blocking; SDK callbacks are queued natively and drained from the
game loop:

```csharp
AdsPlugin.Init(npaOnly: true, childDirected: false);   // once at app start
AdsPlugin.OnEvent += e => { ... };    // Loaded / LoadFailed / Shown / Dismissed / Consent*
AdsPlugin.GatherConsent();            // UMP flow — at the FIRST ad-eligible moment, not app start
AdsPlugin.LoadInterstitial(AdsPlugin.TestInterstitialUnit);   // preload
if (AdsPlugin.InterstitialReady) AdsPlugin.ShowInterstitial(); // -> Shown, then Dismissed

// game loop, next to your other Poll calls:
AdsPlugin.Poll();
```

`Dismissed` is the important event — resume music and navigation on it. A failed
show still emits `Dismissed` after `LoadFailed`, so the resume path runs exactly once
either way.

## Ids: application id vs unit ids

- **APPLICATION_ID is platform configuration, not an API input.**
  - Android: the plugin's manifest fragment injects
    `<meta-data com.google.android.gms.ads.APPLICATION_ID>` with the builder token
    `$(AdMobAppId_Android|<Google TEST app id>)` — set `AdMobAppId_Android` in the
    app's `Directory.Build.props` for production; unset, the Google TEST app id is
    used and production inventory can never be served by accident.
  - iOS: `IOSInfoPlistKey_GADApplicationIdentifier` in `Directory.Build.props`
    (plus an `IOSInfoPlistRawFragment_SKAdNetworkItems` array).
- **Ad unit ids are runtime parameters** of `LoadInterstitial`.
  `AdsPlugin.TestInterstitialUnit` is Google's published test unit for the platform —
  use it in every non-store build (clicking real ads in dev gets the account banned).

## Integration (consuming app)

```xml
<!-- Directory.Build.props -->
<PropertyGroup>
   <AndroidNativeLibrary_sokol_adsPath>../../plugins/Ads/libs/android</AndroidNativeLibrary_sokol_adsPath>
   <AndroidJavaSource_sokoladsPath>../../plugins/Ads/platform/android/java</AndroidJavaSource_sokoladsPath>
   <AdMobAppId_Android></AdMobAppId_Android>   <!-- empty = Google test app id -->
   <IOSNativeLibrary_sokol_adsPath>../../plugins/Ads/libs/ios/arm64/release</IOSNativeLibrary_sokol_adsPath>
   <IOSNativeLibrary_GoogleMobileAdsPath>../../plugins/Ads/vendor/ios/device</IOSNativeLibrary_GoogleMobileAdsPath>
   <IOSInfoPlistKey_GADApplicationIdentifier>ca-app-pub-3940256099942544~1458002511</IOSInfoPlistKey_GADApplicationIdentifier>
</PropertyGroup>
<ItemGroup>
   <Compile Include="$(SokolNetHome)/plugins/Ads/managed/*.cs" />
</ItemGroup>
```

The `AndroidNativeLibrary_*` path marks the plugin ACTIVE: the builder then
auto-injects `platform/android/gradle-deps.txt` (play-services-ads + UMP) and the
manifest fragment. `AndroidJavaSource_*` compiles `com.sokol.ads.SokolAds` into the
APK.

**iOS SDK is fetched, not vendored** (Google's SDK; `vendor/` is gitignored):

```bash
./plugins/Ads/scripts/fetch-googlemobileads-ios.sh   # downloads + extracts device slices
```

`vendor/ios/device/` then holds `GoogleMobileAds.framework` +
`UserMessagingPlatform.framework` for the builder's framework embedding (one
`IOSNativeLibrary_*Path` covers both — the builder globs `*.framework`).

## Layout

```
managed/   AdsPlugin.cs (public API + events)  SokolAds.cs (P/Invoke)
native/    sokol_ads.h            C ABI (poll model, int-only events)
           sokol_ads_queue.c      shared lock-protected event ring + ready flag
           sokol_ads_android.c    JNI bridge -> com.sokol.ads.SokolAds
           sokol_ads_ios.m        GoogleMobileAds + UMP (ObjC, ARC)
platform/android/
           gradle-deps.txt        play-services-ads + user-messaging-platform
           manifest/Providers.xml APPLICATION_ID meta-data ($(Prop|default) token)
           java/com/sokol/ads/SokolAds.java
scripts/   build-android.sh  build-ios.sh  fetch-googlemobileads-ios.sh
libs/      prebuilt outputs (committed): android/<abi>/release/libsokol_ads.so,
           ios/arm64/{debug,release}/sokol_ads.framework
vendor/    (gitignored) fetched Google SDK xcframeworks + device slices
```
