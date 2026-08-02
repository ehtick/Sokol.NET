using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

/// <summary>
/// One-time in-app purchases for Sokol.NET apps — Google Play Billing on
/// Android, StoreKit 2 on iOS. All calls are non-blocking; results arrive as
/// <see cref="BillingEvent"/>s raised by <see cref="Billing.Poll"/>, which the
/// app calls once per frame from its game loop (the NearNet.Poll model).
///
/// On platforms without a store (desktop, web) every operation completes
/// deterministically through the same event path: queries fail, purchases
/// fail with <see cref="Billing.CodeUnavailable"/>, restore finishes empty —
/// so UI flows are testable everywhere without #if at the call sites.
///
/// <c>PurchaseOk.Proof</c> carries the store's verification material
/// (Android: purchase JSON + RSA signature; iOS: the Apple-signed JWS) for the
/// app's own offline entitlement verification. The plugin never interprets it.
/// </summary>
public enum BillingEventType
{
    ProductInfo       = 1,
    ProductFailed     = 2,
    PurchaseOk        = 3,
    PurchaseCancelled = 4,
    PurchaseFailed    = 5,
    RestoreDone       = 6,

    /// <summary>An automatic owned-purchase sync finished (store connect, not a user restore).
    /// <c>Code == 0</c> means the store ANSWERED, so the <see cref="PurchaseOk"/> events that
    /// preceded it are the complete set of what is owned — the only signal from which a
    /// consumer can infer a REVOKED purchase (the replay itself is add-only). A non-zero code
    /// means the query failed and its emptiness means nothing. Distinct from
    /// <see cref="RestoreDone"/> so a background sync never answers a "Restore purchases" tap.</summary>
    SyncDone          = 7,
}

public readonly struct BillingEvent
{
    public BillingEventType Type { get; init; }
    public int Code { get; init; }
    public string? Sku { get; init; }
    public string? Price { get; init; }
    public string? Proof { get; init; }
    public string? Signature { get; init; }
}

public static class Billing
{
    /// <summary>Error code used by the no-store stub platforms.</summary>
    public const int CodeUnavailable = -1000;

    /// <summary>True when a real store backend exists on this platform.</summary>
#if __ANDROID__ || __IOS__
    public const bool Available = true;
#else
    public const bool Available = false;
#endif

    /// <summary>Raised from within <see cref="Poll"/>, on the polling thread.</summary>
    public static event Action<BillingEvent>? OnEvent;

    static readonly Queue<BillingEvent> _stubQueue = new();

    public static void Init()
    {
#if __ANDROID__ || __IOS__
        SokolBilling.Init();
#endif
    }

    public static void QueryProduct(string sku)
    {
#if __ANDROID__ || __IOS__
        SokolBilling.QueryProduct(sku);
#else
        _stubQueue.Enqueue(new BillingEvent {
            Type = BillingEventType.ProductFailed, Code = CodeUnavailable, Sku = sku });
#endif
    }

    public static void Purchase(string sku)
    {
#if __ANDROID__ || __IOS__
        SokolBilling.Purchase(sku);
#else
        _stubQueue.Enqueue(new BillingEvent {
            Type = BillingEventType.PurchaseFailed, Code = CodeUnavailable, Sku = sku });
#endif
    }

    public static void Restore()
    {
#if __ANDROID__ || __IOS__
        SokolBilling.Restore();
#else
        // Desktop/web have no store, so the enumeration is NOT authoritative — report it as a
        // failed query. Emitting success would tell an entitlement cache that the player owns
        // nothing, and a cache that reconciles on that would wipe real purchases on any build
        // without a store.
        _stubQueue.Enqueue(new BillingEvent { Type = BillingEventType.RestoreDone, Code = CodeUnavailable });
#endif
    }

    /// <summary>Drain pending store events; call once per frame from the game loop.</summary>
    public static void Poll()
    {
        while (_stubQueue.Count > 0)
            OnEvent?.Invoke(_stubQueue.Dequeue());
#if __ANDROID__ || __IOS__
        while (SokolBilling.PollEvent(out SokolBilling.Event e))
        {
            OnEvent?.Invoke(new BillingEvent {
                Type      = (BillingEventType)e.type,
                Code      = e.code,
                Sku       = Marshal.PtrToStringUTF8(e.sku),
                Price     = Marshal.PtrToStringUTF8(e.price),
                Proof     = Marshal.PtrToStringUTF8(e.proof),
                Signature = Marshal.PtrToStringUTF8(e.signature),
            });
        }
#endif
    }
}
