/* sokol_speech_web.c -- window.speechSynthesis backend (Emscripten).
   The JS lives inline (EM_JS), so the app's link needs no extra --js-library;
   the C symbols below are what the managed P/Invokes bind to.

   Browsers list REMOTE voices too (Chrome's "Google ..." voices have
   localService = false and fail offline); only localService voices count as
   available. Chrome populates getVoices() asynchronously, so init() primes the
   list and listens for voiceschanged. */
#include "sokol_speech.h"

#if defined(__EMSCRIPTEN__)
#include <emscripten.h>

extern void sokolspeech__emit(int type, int id, int code);

/* Called from the JS utterance callbacks. Kept alive so the linker exports it. */
EMSCRIPTEN_KEEPALIVE
void sokolspeech_web_on_event(int type, int id, int code) { sokolspeech__emit(type, id, code); }

EM_JS(void, sokolspeech_js_init, (void), {
    if (!('speechSynthesis' in window)) { _sokolspeech_web_on_event(1, 0, -1); return; }
    window.speechSynthesis.getVoices();
    if (window.speechSynthesis.onvoiceschanged !== undefined)
        window.speechSynthesis.onvoiceschanged = function() { window.speechSynthesis.getVoices(); };
    _sokolspeech_web_on_event(1, 0, 0);
});

/* Shared voice picker: exact tag first, then same language, local voices only. */
EM_JS(int, sokolspeech_js_pick, (const char* lang), {
    if (!('speechSynthesis' in window)) return -1;
    var want = UTF8ToString(lang).replace('_', '-').toLowerCase();
    var wantLang = want.split('-')[0];
    var wantRegion = want.indexOf('-') >= 0;
    var voices = window.speechSynthesis.getVoices();
    var best = -1, bestScore = -1;
    for (var i = 0; i < voices.length; i++) {
        var v = voices[i];
        if (!v.localService) continue;
        var vl = (v.lang || "").replace('_', '-').toLowerCase();
        if (vl.split('-')[0] !== wantLang) continue;
        var score = (wantRegion && vl === want) ? 100 : 0;
        if (v.default) score += 1;
        if (score > bestScore) { bestScore = score; best = i; }
    }
    return best;
});

EM_JS(void, sokolspeech_js_say, (int id, int voiceIndex, const char* text), {
    var voices = window.speechSynthesis.getVoices();
    var u = new SpeechSynthesisUtterance(UTF8ToString(text));
    if (voiceIndex >= 0 && voiceIndex < voices.length) { u.voice = voices[voiceIndex]; u.lang = voices[voiceIndex].lang; }
    u.onstart = function() { _sokolspeech_web_on_event(2, id, 0); };
    u.onend   = function() { _sokolspeech_web_on_event(3, id, 0); };
    u.onerror = function(e) { _sokolspeech_web_on_event((e && e.error === 'interrupted' || e && e.error === 'canceled') ? 3 : 4, id, 1); };
    window.speechSynthesis.cancel();
    window.speechSynthesis.speak(u);
});

EM_JS(void, sokolspeech_js_stop, (void), {
    if ('speechSynthesis' in window) window.speechSynthesis.cancel();
});

static int _ss_next_id = 1;

void sokolspeech_init(void)               { sokolspeech_js_init(); }
bool sokolspeech_available(const char* lang) { return lang && sokolspeech_js_pick(lang) >= 0; }
int  sokolspeech_say(const char* text, const char* lang)
{
    if (!text || !lang) return 0;
    int v = sokolspeech_js_pick(lang);
    if (v < 0) return 0;
    int id = _ss_next_id++;
    sokolspeech_js_say(id, v, text);
    return id;
}
void sokolspeech_stop(void)                { sokolspeech_js_stop(); }
void sokolspeech_open_voice_settings(void) { }
void sokolspeech_shutdown(void)            { sokolspeech_js_stop(); }

#endif /* __EMSCRIPTEN__ */
