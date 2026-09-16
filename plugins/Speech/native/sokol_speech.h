/* sokol_speech.h -- platform text-to-speech for Sokol.NET apps.
   Android: android.speech.tts.TextToSpeech (via a Java helper class).
   iOS/macOS: AVSpeechSynthesizer (Objective-C shim).
   Windows: Windows.Media.SpeechSynthesis (C++/WinRT shim).
   Web: window.speechSynthesis (Emscripten JS interop).
   Linux: no native library -- the managed layer probes speech-dispatcher (spd-say) at runtime.

   Speech is synthesised by the OS engine at RUNTIME; nothing is rendered ahead of
   time or redistributed, so no voice licence attaches to the app.

   All calls are non-blocking; results surface as queued events drained by
   sokolspeech_poll_event() -- call it from the game thread once per frame (the
   NearNet.Poll model). The public functions may be called from any thread, but
   the intended model is a single game thread; engine callbacks arrive on
   platform threads and are queued internally.
*/
#pragma once
#ifndef SOKOL_SPEECH_H
#define SOKOL_SPEECH_H

#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef enum sokolspeech_event_type {
    SOKOLSPEECH_EVENT_NONE    = 0,
    SOKOLSPEECH_EVENT_READY   = 1, /* engine initialised; code 0 = usable, else engine error */
    SOKOLSPEECH_EVENT_STARTED = 2, /* utterance `id` began playing                          */
    SOKOLSPEECH_EVENT_DONE    = 3, /* utterance `id` finished; code 0 = spoken, 1 = cancelled */
    SOKOLSPEECH_EVENT_ERROR   = 4, /* utterance `id` failed; code = engine error             */
} sokolspeech_event_type;

typedef struct sokolspeech_event {
    int type;   /* sokolspeech_event_type                       */
    int id;     /* utterance id from sokolspeech_say, else 0    */
    int code;   /* see the event type                           */
} sokolspeech_event;

/* Create the engine. Android initialises asynchronously: nothing is available
   until READY arrives (typically well under a second). Idempotent. */
void sokolspeech_init(void);

/* True when an OFFLINE voice for `lang` is installed RIGHT NOW. `lang` is a
   BCP-47 tag ("he", "pt-BR", "en-US"); a bare language matches any region, a
   full tag prefers its exact region and falls back to the language. Re-check at
   game start -- the user can install or remove voices at any time. */
bool sokolspeech_available(const char* lang);

/* Speak `text` with the best offline voice for `lang`, interrupting whatever is
   playing. Returns the utterance id (> 0) that STARTED/DONE/ERROR will carry,
   or 0 when no offline voice exists (nothing is queued in that case). */
int sokolspeech_say(const char* text, const char* lang);

/* Stop the current utterance (DONE with code 1 follows where the engine reports it). */
void sokolspeech_stop(void);

/* Open the system page where voices are installed (Android: the TTS data
   installer / TTS settings; iOS + macOS: the app's Settings page, from where
   Accessibility > Spoken Content is reachable). No-op on Windows and web. */
void sokolspeech_open_voice_settings(void);

/* Pop one queued event. Returns false when the queue is empty. */
bool sokolspeech_poll_event(sokolspeech_event* out);

/* Release the engine. Safe to call without init. */
void sokolspeech_shutdown(void);

#ifdef __cplusplus
}
#endif
#endif /* SOKOL_SPEECH_H */
