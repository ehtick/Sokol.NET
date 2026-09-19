/* sokol_speech_apple.m -- AVSpeechSynthesizer implementation for iOS and macOS.
   Shared by both: AVFoundation's speech API is identical on the two platforms
   (macOS 10.14+, iOS 7+). Events flow into the shared C queue via sokolspeech__emit.

   Audio session: the synthesizer uses the APP's audio session
   (usesApplicationAudioSession = YES, the default), so it mixes with whatever
   the app already plays through it (e.g. a MiniAudio engine) and follows the
   category the app configured; nothing here changes the session. */
#import <AVFoundation/AVFoundation.h>
#if TARGET_OS_IPHONE
#import <UIKit/UIKit.h>
#else
#import <AppKit/AppKit.h>
#endif
#import <objc/runtime.h>
#include "sokol_speech.h"

extern void sokolspeech__emit(int type, int id, int code);

/* The utterance id rides on the AVSpeechUtterance itself: say() interrupts the previous
   utterance, whose didCancel arrives AFTER the next one has become current, so a
   "current utterance" comparison would drop the cancelled one's DONE. */
static char _ss_id_key;
static int _ss_id_of(AVSpeechUtterance* u) {
    NSNumber* n = objc_getAssociatedObject(u, &_ss_id_key);
    return n ? n.intValue : 0;
}

@interface SokolSpeechDelegate : NSObject <AVSpeechSynthesizerDelegate>
@end

static AVSpeechSynthesizer* _syn;
static SokolSpeechDelegate* _delegate;
static int                  _nextId = 1;

/* DONE is emitted exactly ONCE per utterance, from whichever path sees the end first: the delegate,
   the isSpeaking watchdog below, or the next say()/stop() replacing it. A "done" mark on the
   utterance drops the later duplicates. */
static char _ss_done_key;
static void _ss_done(AVSpeechUtterance* u, int code) {
    int id = _ss_id_of(u);
    if (!id || objc_getAssociatedObject(u, &_ss_done_key)) return;
    objc_setAssociatedObject(u, &_ss_done_key, @YES, OBJC_ASSOCIATION_RETAIN_NONATOMIC);
    sokolspeech__emit(SOKOLSPEECH_EVENT_DONE, id, code);
}

/* ⛔ The delegate's didFinish is NOT reliable in an app (measured on macOS 26, 2026-09-19): the last
   utterance before a quiet stretch never reported it in 4 of 10 runs, so Speech.Speaking stayed set
   for good, while isSpeaking dropped on time. So while an utterance is current, a main-thread timer
   polls isSpeaking and ends it when the engine has stopped. Only between a say() and its end — the
   timer is gone the rest of the time. */
static AVSpeechUtterance* _current;   /* the utterance awaiting its DONE, nil when none */
static NSTimer*           _watch;
static BOOL               _heard;      /* isSpeaking was seen true for _current */
static void _ss_watch_stop(void) { [_watch invalidate]; _watch = nil; }
static void _ss_watch_start(void) {
    _ss_watch_stop();
    _heard = NO;
    _watch = [NSTimer timerWithTimeInterval:0.1 repeats:YES block:^(NSTimer* t) {
        if (!_current || !_syn) { _ss_watch_stop(); return; }
        if (_syn.isSpeaking) { _heard = YES; return; }
        if (!_heard) return;                               /* not started yet — wait for it */
        _ss_done(_current, 0);
        _current = nil;
        _ss_watch_stop();
    }];
    [[NSRunLoop mainRunLoop] addTimer:_watch forMode:NSRunLoopCommonModes];
}

@implementation SokolSpeechDelegate
- (void)speechSynthesizer:(AVSpeechSynthesizer*)s didStartSpeechUtterance:(AVSpeechUtterance*)u {
    int id = _ss_id_of(u); if (id) sokolspeech__emit(SOKOLSPEECH_EVENT_STARTED, id, 0);
    if (u == _current) _heard = YES;
}
- (void)speechSynthesizer:(AVSpeechSynthesizer*)s didFinishSpeechUtterance:(AVSpeechUtterance*)u {
    _ss_done(u, 0);
    if (u == _current) { _current = nil; _ss_watch_stop(); }
}
- (void)speechSynthesizer:(AVSpeechSynthesizer*)s didCancelSpeechUtterance:(AVSpeechUtterance*)u {
    _ss_done(u, 1);
    if (u == _current) { _current = nil; _ss_watch_stop(); }
}
@end

/* Best installed voice for a BCP-47 tag: exact tag first, then same language
   (any region), preferring enhanced/premium quality. All AVSpeechSynthesisVoices
   are on-device. */
static AVSpeechSynthesisVoice* _ss_voice(const char* lang)
{
    if (!lang || !lang[0]) return nil;
    NSString* want = [[NSString stringWithUTF8String:lang] stringByReplacingOccurrencesOfString:@"_" withString:@"-"];
    NSString* wantLang = [[want componentsSeparatedByString:@"-"] firstObject].lowercaseString;
    BOOL wantRegion = [want containsString:@"-"];
    AVSpeechSynthesisVoice* best = nil;
    NSInteger bestScore = -1;
    for (AVSpeechSynthesisVoice* v in [AVSpeechSynthesisVoice speechVoices]) {
        NSString* vl = v.language;
        NSString* vLang = [[vl componentsSeparatedByString:@"-"] firstObject].lowercaseString;
        if (![vLang isEqualToString:wantLang]) continue;
        NSInteger score = 0;
        if (wantRegion && [vl caseInsensitiveCompare:want] == NSOrderedSame) score += 100;
        if (@available(iOS 9.0, macOS 10.14, *)) score += (NSInteger)v.quality;   /* default 1, enhanced 2, premium 3 */
        if (score > bestScore) { bestScore = score; best = v; }
    }
    return best;
}

void sokolspeech_init(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (_syn) return;
        _delegate = [SokolSpeechDelegate new];
        _syn = [AVSpeechSynthesizer new];
        _syn.delegate = _delegate;
        sokolspeech__emit(SOKOLSPEECH_EVENT_READY, 0, 0);
    });
}

bool sokolspeech_available(const char* lang)
{
    return _ss_voice(lang) != nil;
}

int sokolspeech_say(const char* text, const char* lang)
{
    if (!text) return 0;
    AVSpeechSynthesisVoice* voice = _ss_voice(lang);
    if (!voice) return 0;
    int id = _nextId++;
    NSString* nsText = [NSString stringWithUTF8String:text];
    dispatch_async(dispatch_get_main_queue(), ^{
        if (!_syn) { sokolspeech__emit(SOKOLSPEECH_EVENT_ERROR, id, -1); return; }
        if (_syn.isSpeaking) [_syn stopSpeakingAtBoundary:AVSpeechBoundaryImmediate];
        if (_current) _ss_done(_current, 1);               /* interrupted — its own callback may never come */
        AVSpeechUtterance* u = [AVSpeechUtterance speechUtteranceWithString:nsText ?: @""];
        u.voice = voice;
        objc_setAssociatedObject(u, &_ss_id_key, @(id), OBJC_ASSOCIATION_RETAIN_NONATOMIC);
        _current = u;
        _ss_watch_start();
        [_syn speakUtterance:u];
    });
    return id;
}

void sokolspeech_stop(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (_syn && _syn.isSpeaking) [_syn stopSpeakingAtBoundary:AVSpeechBoundaryImmediate];
        if (_current) { _ss_done(_current, 1); _current = nil; }
        _ss_watch_stop();
    });
}

void sokolspeech_open_voice_settings(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
#if TARGET_OS_IPHONE
        NSURL* url = [NSURL URLWithString:UIApplicationOpenSettingsURLString];
        if (url) [[UIApplication sharedApplication] openURL:url options:@{} completionHandler:nil];
#else
        /* Accessibility > Spoken Content, where system voices are downloaded. */
        NSURL* url = [NSURL URLWithString:@"x-apple.systempreferences:com.apple.preference.universalaccess?SpeakableItems"];
        if (url) [[NSWorkspace sharedWorkspace] openURL:url];
#endif
    });
}

void sokolspeech_shutdown(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (_syn && _syn.isSpeaking) [_syn stopSpeakingAtBoundary:AVSpeechBoundaryImmediate];
        _ss_watch_stop();
        _current = nil;
        _syn = nil;
        _delegate = nil;
    });
}
