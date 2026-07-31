package com.sokol.ads;

import android.app.Activity;
import android.os.Bundle;

import com.google.ads.mediation.admob.AdMobAdapter;
import com.google.android.gms.ads.AdError;
import com.google.android.gms.ads.AdRequest;
import com.google.android.gms.ads.FullScreenContentCallback;
import com.google.android.gms.ads.LoadAdError;
import com.google.android.gms.ads.MobileAds;
import com.google.android.gms.ads.RequestConfiguration;
import com.google.android.gms.ads.interstitial.InterstitialAd;
import com.google.android.gms.ads.interstitial.InterstitialAdLoadCallback;
import com.google.android.ump.ConsentInformation;
import com.google.android.ump.ConsentRequestParameters;
import com.google.android.ump.UserMessagingPlatform;

/**
 * Google Mobile Ads helper for the sokol_ads plugin. Called from native code
 * (sokol_ads_android.c); results flow back through nativeOnEvent into the
 * plugin's C event queue, drained by the app's game thread.
 *
 * Interstitials only. Max ad content rating is pinned to G (family app);
 * npa-only requests carry the "npa"="1" network extra so no personalized ads
 * are ever served regardless of consent state.
 */
public final class SokolAds {

    /** Upcall into sokol_ads_android.c — safe from any thread. */
    static native void nativeOnEvent(int type, int code);

    /* Mirror of sokolads_event_type in sokol_ads.h. */
    static final int EV_LOADED         = 1;
    static final int EV_LOAD_FAILED    = 2;
    static final int EV_SHOWN          = 3;
    static final int EV_DISMISSED      = 4;
    static final int EV_CONSENT_READY  = 5;
    static final int EV_CONSENT_FAILED = 6;

    static Activity activity;
    static boolean npaOnly;
    static InterstitialAd loaded;

    private SokolAds() {}

    public static void init(final Activity act, final boolean npa, final boolean childDirected) {
        activity = act;
        npaOnly = npa;
        act.runOnUiThread(() -> {
            RequestConfiguration.Builder cfg = MobileAds.getRequestConfiguration().toBuilder()
                .setMaxAdContentRating(RequestConfiguration.MAX_AD_CONTENT_RATING_G);
            if (childDirected) {
                cfg.setTagForChildDirectedTreatment(
                    RequestConfiguration.TAG_FOR_CHILD_DIRECTED_TREATMENT_TRUE);
            }
            MobileAds.setRequestConfiguration(cfg.build());
            MobileAds.initialize(act, status -> {});
        });
    }

    public static void gatherConsent() {
        if (activity == null) return;
        activity.runOnUiThread(() -> {
            ConsentRequestParameters params = new ConsentRequestParameters.Builder().build();
            ConsentInformation info = UserMessagingPlatform.getConsentInformation(activity);
            info.requestConsentInfoUpdate(activity, params,
                () -> UserMessagingPlatform.loadAndShowConsentFormIfRequired(activity, formError -> {
                    if (formError != null) nativeOnEvent(EV_CONSENT_FAILED, formError.getErrorCode());
                    else                   nativeOnEvent(EV_CONSENT_READY, 0);
                }),
                requestError -> nativeOnEvent(EV_CONSENT_FAILED, requestError.getErrorCode()));
        });
    }

    public static void loadInterstitial(final String unitId) {
        if (activity == null) return;
        activity.runOnUiThread(() -> {
            AdRequest.Builder req = new AdRequest.Builder();
            if (npaOnly) {
                Bundle extras = new Bundle();
                extras.putString("npa", "1");
                req.addNetworkExtrasBundle(AdMobAdapter.class, extras);
            }
            InterstitialAd.load(activity, unitId, req.build(), new InterstitialAdLoadCallback() {
                @Override public void onAdLoaded(InterstitialAd ad) {
                    loaded = ad;
                    nativeOnEvent(EV_LOADED, 0);
                }
                @Override public void onAdFailedToLoad(LoadAdError error) {
                    loaded = null;
                    nativeOnEvent(EV_LOAD_FAILED, error.getCode());
                }
            });
        });
    }

    public static void showInterstitial() {
        if (activity == null) return;
        activity.runOnUiThread(() -> {
            InterstitialAd ad = loaded;
            loaded = null;
            if (ad == null) return;
            ad.setFullScreenContentCallback(new FullScreenContentCallback() {
                @Override public void onAdShowedFullScreenContent() {
                    nativeOnEvent(EV_SHOWN, 0);
                }
                @Override public void onAdDismissedFullScreenContent() {
                    nativeOnEvent(EV_DISMISSED, 0);
                }
                @Override public void onAdFailedToShowFullScreenContent(AdError error) {
                    /* Never shown — report failure, then DISMISSED so the app's
                       resume path (music/navigation) runs exactly once either way. */
                    nativeOnEvent(EV_LOAD_FAILED, error.getCode());
                    nativeOnEvent(EV_DISMISSED, 0);
                }
            });
            ad.show(activity);
        });
    }
}
