using System;
using System.Collections.Generic;

/// <summary>
/// Google AdMob interstitials for Sokol.NET apps — Android + iOS. All calls are
/// non-blocking; results arrive as <see cref="AdsEvent"/>s raised by
/// <see cref="AdsPlugin.Poll"/>, which the app calls once per frame from its
/// game loop (the NearNet.Poll model).
///
/// On platforms without an ad SDK (desktop, web) every operation completes
/// deterministically through the same event path: loads fail with
/// <see cref="AdsPlugin.CodeUnavailable"/>, consent reports ready — so policy
/// layers are testable everywhere without #if at the call sites.
///
/// The AdMob APPLICATION_ID is injected into the platform artifacts by the app
/// builder (manifest meta-data / Info.plist), not passed here. Ad UNIT ids are
/// runtime parameters; Google's published TEST units
/// (<see cref="TestInterstitialUnit"/>) work in any build without store setup.
/// </summary>
public enum AdsEventType
{
    Loaded        = 1,
    LoadFailed    = 2,
    Shown         = 3,
    Dismissed     = 4,
    ConsentReady  = 5,
    ConsentFailed = 6,
}

public readonly struct AdsEvent
{
    public AdsEventType Type { get; init; }
    public int Code { get; init; }
}

public static class AdsPlugin
{
    /// <summary>Error code used by the no-SDK stub platforms.</summary>
    public const int CodeUnavailable = -1000;

    /// <summary>Google's published test interstitial unit for this platform —
    /// safe (and mandatory) for every non-store build; clicking real ads in
    /// dev gets the AdMob account banned.</summary>
#if __IOS__
    public const string TestInterstitialUnit = "ca-app-pub-3940256099942544/4411468910";
#else
    public const string TestInterstitialUnit = "ca-app-pub-3940256099942544/1033173712";
#endif

    /// <summary>True when a real ad SDK exists on this platform AND its native library
    /// loaded. An app may ship without the native plugin (e.g. iOS while the static-Swift
    /// GoogleMobileAds link is unresolved) — the first failing P/Invoke flips this false and
    /// every later call stubs out instead of crashing.</summary>
#if __ANDROID__ || __IOS__
    public static bool Available => _nativeOk;
    static bool _nativeOk = true;
#else
    public const bool Available = false;
#endif

    /// <summary>Raised from within <see cref="Poll"/>, on the polling thread.</summary>
    public static event Action<AdsEvent>? OnEvent;

    static readonly Queue<AdsEvent> _stubQueue = new();

#if __ANDROID__ || __IOS__
    // The native library binds lazily at the first P/Invoke; a missing plugin framework must
    // degrade to the stub path, never crash the app.
    static bool Native(Action call)
    {
        if (!_nativeOk) return false;
        try { call(); return true; }
        catch (DllNotFoundException)        { _nativeOk = false; }
        catch (EntryPointNotFoundException) { _nativeOk = false; }
        return false;
    }
#endif

    public static void Init(bool npaOnly, bool childDirected)
    {
#if __ANDROID__ || __IOS__
        Native(() => SokolAds.Init(null, npaOnly, childDirected));
#endif
    }

    /// <summary>UMP consent flow — call at the first ad-eligible moment, not app start.</summary>
    public static void GatherConsent()
    {
#if __ANDROID__ || __IOS__
        if (Native(SokolAds.ConsentGather)) return;
#endif
        _stubQueue.Enqueue(new AdsEvent { Type = AdsEventType.ConsentReady });
    }

    public static void LoadInterstitial(string adUnitId)
    {
#if __ANDROID__ || __IOS__
        if (Native(() => SokolAds.LoadInterstitial(adUnitId))) return;
#endif
        _stubQueue.Enqueue(new AdsEvent { Type = AdsEventType.LoadFailed, Code = CodeUnavailable });
    }

    public static bool InterstitialReady
    {
        get
        {
#if __ANDROID__ || __IOS__
            bool ready = false;
            Native(() => ready = SokolAds.InterstitialReady());
            return ready;
#else
            return false;
#endif
        }
    }

    /// <summary>Show the preloaded interstitial (consumes it) — Shown then Dismissed follow.</summary>
    public static void ShowInterstitial()
    {
#if __ANDROID__ || __IOS__
        Native(SokolAds.ShowInterstitial);
#endif
    }

    /// <summary>Drain pending ad events; call once per frame from the game loop.</summary>
    public static void Poll()
    {
        while (_stubQueue.Count > 0)
            OnEvent?.Invoke(_stubQueue.Dequeue());
#if __ANDROID__ || __IOS__
        if (!_nativeOk) return;
        Native(() =>
        {
            while (SokolAds.PollEvent(out SokolAds.Event e))
                OnEvent?.Invoke(new AdsEvent { Type = (AdsEventType)e.type, Code = e.code });
        });
#endif
    }
}
