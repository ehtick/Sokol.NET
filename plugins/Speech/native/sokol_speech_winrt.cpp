/* sokol_speech_winrt.cpp -- Windows backend (C++/WinRT,
   Windows.Media.SpeechSynthesis + Windows.Media.Playback).

   System.Speech (.NET) is COM/SAPI-based and not a safe NativeAOT dependency, so
   speech is synthesised through the WinRT API instead: SpeechSynthesizer renders
   the utterance to a stream and a MediaPlayer plays it. Both are usable from an
   unpackaged desktop process. Voices are the installed language packs
   (Settings > Time & Language > Speech), all on-device.

   Compiled/verified by CI (Windows job); no local Windows toolchain on the
   development Mac. */
#include "sokol_speech.h"

#if defined(_WIN32)

#include <windows.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Media.Core.h>
#include <winrt/Windows.Media.Playback.h>
#include <winrt/Windows.Media.SpeechSynthesis.h>
#include <winrt/Windows.Storage.Streams.h>

#include <atomic>
#include <mutex>
#include <string>

extern "C" void sokolspeech__emit(int type, int id, int code);

using namespace winrt;
using namespace winrt::Windows::Foundation;
using namespace winrt::Windows::Media::Core;
using namespace winrt::Windows::Media::Playback;
using namespace winrt::Windows::Media::SpeechSynthesis;
using namespace winrt::Windows::Storage::Streams;

namespace {

std::mutex        g_lock;
SpeechSynthesizer g_syn{ nullptr };
MediaPlayer       g_player{ nullptr };
std::atomic<int>  g_nextId{ 1 };
std::atomic<int>  g_currentId{ 0 };

std::wstring ToWide(const char* utf8) {
    if (!utf8 || !utf8[0]) return std::wstring();
    int n = MultiByteToWideChar(CP_UTF8, 0, utf8, -1, nullptr, 0);
    std::wstring w(n > 0 ? n - 1 : 0, L'\0');
    if (n > 0) MultiByteToWideChar(CP_UTF8, 0, utf8, -1, w.data(), n);
    return w;
}

std::wstring Lower(std::wstring s) {
    for (auto& c : s) c = (wchar_t)towlower(c);
    return s;
}

/* Best installed voice for a BCP-47 tag: exact tag first, then same language. */
VoiceInformation PickVoice(const char* lang) {
    if (!lang || !lang[0]) return nullptr;
    std::wstring want = Lower(ToWide(lang));
    for (auto& c : want) if (c == L'_') c = L'-';
    std::wstring wantLang = want.substr(0, want.find(L'-'));
    bool wantRegion = want.find(L'-') != std::wstring::npos;
    VoiceInformation best{ nullptr };
    int bestScore = -1;
    for (auto const& v : SpeechSynthesizer::AllVoices()) {
        std::wstring vl = Lower(std::wstring(v.Language()));
        if (vl.substr(0, vl.find(L'-')) != wantLang) continue;
        int score = (wantRegion && vl == want) ? 100 : 0;
        if (score > bestScore) { bestScore = score; best = v; }
    }
    return best;
}

fire_and_forget Speak(int id, SpeechSynthesizer syn, MediaPlayer player, hstring text) {
    try {
        SpeechSynthesisStream stream = co_await syn.SynthesizeTextToStreamAsync(text);
        {
            std::lock_guard<std::mutex> guard(g_lock);
            if (g_currentId.load() != id) co_return;     /* superseded by a later say/stop */
            player.Source(MediaSource::CreateFromStream(stream, stream.ContentType()));
            player.Play();
        }
        sokolspeech__emit(SOKOLSPEECH_EVENT_STARTED, id, 0);
    } catch (...) {
        sokolspeech__emit(SOKOLSPEECH_EVENT_ERROR, id, -1);
    }
}

} // namespace

extern "C" {

void sokolspeech_init(void)
{
    std::lock_guard<std::mutex> guard(g_lock);
    if (g_syn) return;
    try { winrt::init_apartment(winrt::apartment_type::multi_threaded); } catch (...) {}
    try {
        g_syn = SpeechSynthesizer();
        g_player = MediaPlayer();
        g_player.MediaEnded([](MediaPlayer const&, IInspectable const&) {
            int id = g_currentId.exchange(0);
            if (id) sokolspeech__emit(SOKOLSPEECH_EVENT_DONE, id, 0);
        });
        g_player.MediaFailed([](MediaPlayer const&, MediaPlayerFailedEventArgs const&) {
            int id = g_currentId.exchange(0);
            if (id) sokolspeech__emit(SOKOLSPEECH_EVENT_ERROR, id, -2);
        });
        sokolspeech__emit(SOKOLSPEECH_EVENT_READY, 0, 0);
    } catch (...) {
        g_syn = nullptr;
        g_player = nullptr;
        sokolspeech__emit(SOKOLSPEECH_EVENT_READY, 0, -1);
    }
}

bool sokolspeech_available(const char* lang)
{
    try { return PickVoice(lang) != nullptr; } catch (...) { return false; }
}

int sokolspeech_say(const char* text, const char* lang)
{
    if (!text) return 0;
    VoiceInformation voice{ nullptr };
    try { voice = PickVoice(lang); } catch (...) {}
    if (!voice) return 0;
    std::lock_guard<std::mutex> guard(g_lock);
    if (!g_syn || !g_player) return 0;
    int id = g_nextId++;
    int previous = g_currentId.exchange(id);
    try {
        if (previous) { g_player.Pause(); sokolspeech__emit(SOKOLSPEECH_EVENT_DONE, previous, 1); }
        g_syn.Voice(voice);
        Speak(id, g_syn, g_player, hstring(ToWide(text)));
    } catch (...) {
        g_currentId.store(0);
        return 0;
    }
    return id;
}

void sokolspeech_stop(void)
{
    std::lock_guard<std::mutex> guard(g_lock);
    int id = g_currentId.exchange(0);
    if (g_player) { try { g_player.Pause(); } catch (...) {} }
    if (id) sokolspeech__emit(SOKOLSPEECH_EVENT_DONE, id, 1);
}

void sokolspeech_open_voice_settings(void)
{
    /* Settings > Time & Language > Speech (ms-settings URI). */
    ShellExecuteW(nullptr, L"open", L"ms-settings:speech", nullptr, nullptr, SW_SHOWNORMAL);
}

void sokolspeech_shutdown(void)
{
    std::lock_guard<std::mutex> guard(g_lock);
    g_currentId.store(0);
    if (g_player) { try { g_player.Pause(); } catch (...) {} }
    g_player = nullptr;
    g_syn = nullptr;
}

} // extern "C"

#endif /* _WIN32 */
