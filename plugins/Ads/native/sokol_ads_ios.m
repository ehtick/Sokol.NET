/* sokol_ads_ios.m -- Google Mobile Ads implementation for Sokol.NET (iOS).
   Requires GoogleMobileAds.xcframework + UserMessagingPlatform.xcframework
   (fetched by scripts/fetch-googlemobileads-ios.sh — not vendored in the repo).
   The APPLICATION_ID comes from Info.plist's GADApplicationIdentifier, injected
   by the app builder. Interstitials present from the key window's root VC. */
#import <UIKit/UIKit.h>
#import <GoogleMobileAds/GoogleMobileAds.h>
#import <UserMessagingPlatform/UserMessagingPlatform.h>

#include "sokol_ads.h"

/* sokol_ads_queue.c */
extern void sokolads__emit(int type, int code);
extern void sokolads__consume(void);

static bool _sa_npa_only;
static GADInterstitialAd* _sa_loaded;
static id<GADFullScreenContentDelegate> _sa_delegate;

@interface SokolAdsDelegate : NSObject <GADFullScreenContentDelegate>
@end

@implementation SokolAdsDelegate
- (void)adWillPresentFullScreenContent:(id<GADFullScreenPresentingAd>)ad {
    sokolads__emit(SOKOLADS_EVENT_SHOWN, 0);
}
- (void)adDidDismissFullScreenContent:(id<GADFullScreenPresentingAd>)ad {
    sokolads__emit(SOKOLADS_EVENT_DISMISSED, 0);
}
- (void)ad:(id<GADFullScreenPresentingAd>)ad
    didFailToPresentFullScreenContentWithError:(NSError*)error {
    /* Never shown — report failure, then DISMISSED so the app's resume path
       (music/navigation) runs exactly once either way. */
    sokolads__emit(SOKOLADS_EVENT_LOAD_FAILED, (int)error.code);
    sokolads__emit(SOKOLADS_EVENT_DISMISSED, 0);
}
@end

static UIViewController* _sa_root_vc(void) {
    return [UIApplication sharedApplication].windows.firstObject.rootViewController;
}

void sokolads_init(const char* app_id, bool npa_only, bool child_directed) {
    (void)app_id;   /* iOS reads GADApplicationIdentifier from Info.plist */
    _sa_npa_only = npa_only;
    dispatch_async(dispatch_get_main_queue(), ^{
        /* Family app: never serve above-G inventory (MONETIZATION_FEATURES §1.3). */
        GADMobileAds.sharedInstance.requestConfiguration.maxAdContentRating =
            GADMaxAdContentRatingGeneral;
        if (child_directed) {
            GADMobileAds.sharedInstance.requestConfiguration.tagForChildDirectedTreatment = @YES;
        }
        [GADMobileAds.sharedInstance startWithCompletionHandler:nil];
    });
}

void sokolads_consent_gather(void) {
    dispatch_async(dispatch_get_main_queue(), ^{
        UMPRequestParameters* params = [[UMPRequestParameters alloc] init];
        [UMPConsentInformation.sharedInstance
            requestConsentInfoUpdateWithParameters:params
            completionHandler:^(NSError* _Nullable error) {
                if (error) {
                    sokolads__emit(SOKOLADS_EVENT_CONSENT_FAILED, (int)error.code);
                    return;
                }
                [UMPConsentForm loadAndPresentIfRequiredFromViewController:_sa_root_vc()
                    completionHandler:^(NSError* _Nullable formError) {
                        if (formError) sokolads__emit(SOKOLADS_EVENT_CONSENT_FAILED, (int)formError.code);
                        else           sokolads__emit(SOKOLADS_EVENT_CONSENT_READY, 0);
                    }];
            }];
    });
}

void sokolads_load_interstitial(const char* ad_unit_id) {
    if (!ad_unit_id) return;
    NSString* unit = [NSString stringWithUTF8String:ad_unit_id];
    dispatch_async(dispatch_get_main_queue(), ^{
        GADRequest* request = [GADRequest request];
        if (_sa_npa_only) {
            GADExtras* extras = [[GADExtras alloc] init];
            extras.additionalParameters = @{ @"npa" : @"1" };
            [request registerAdNetworkExtras:extras];
        }
        [GADInterstitialAd loadWithAdUnitID:unit
                                    request:request
                          completionHandler:^(GADInterstitialAd* ad, NSError* error) {
            if (error) {
                _sa_loaded = nil;
                sokolads__emit(SOKOLADS_EVENT_LOAD_FAILED, (int)error.code);
                return;
            }
            if (!_sa_delegate) _sa_delegate = [[SokolAdsDelegate alloc] init];
            ad.fullScreenContentDelegate = _sa_delegate;
            _sa_loaded = ad;
            sokolads__emit(SOKOLADS_EVENT_LOADED, 0);
        }];
    });
}

void sokolads_show_interstitial(void) {
    if (!sokolads_interstitial_ready()) return;
    sokolads__consume();
    dispatch_async(dispatch_get_main_queue(), ^{
        GADInterstitialAd* ad = _sa_loaded;
        _sa_loaded = nil;
        if (!ad) return;
        [ad presentFromRootViewController:_sa_root_vc()];
    });
}
