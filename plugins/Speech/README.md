# sokol_speech — platform text-to-speech (Android · iOS · macOS · Windows · Linux · Web)

Speech plugin for Sokol.NET apps: the **operating system's own speech engine** synthesises the
text at runtime. Nothing is rendered ahead of time and nothing is redistributed, so no voice
licence attaches to the app — the reason it exists (a bundled clip set needs a voice actor or a
TTS whose licence permits redistributing rendered audio; a runtime engine needs neither).

| Platform | Engine | Offline | Voice availability |
|---|---|---|---|
| Android | `android.speech.tts.TextToSpeech` (the device's default engine) | yes, once an offline voice is installed | `getVoices()` filtered on `!isNetworkConnectionRequired()` and no `notInstalled` feature; the chosen voice is set explicitly so synthesis never touches the network |
| iOS | `AVSpeechSynthesizer` | yes | compact voices ship with the OS for every supported language; enhanced ones are downloads |
| macOS | `AVSpeechSynthesizer` (same shim) | yes | some languages need a one-time download in System Settings → Accessibility → Spoken Content |
| Windows | `Windows.Media.SpeechSynthesis` via C++/WinRT, played through `Windows.Media.Playback.MediaPlayer` | yes | the voices of installed language packs (Settings → Time & Language → Speech). `System.Speech` is COM/SAPI and not a safe NativeAOT dependency — hence the WinRT shim |
| Linux | speech-dispatcher's `spd-say`, run as a **process** | n/a | present only when the tool is installed; `spd-say -L` lists the voices. Nothing is linked (espeak-ng behind it is GPL) |
| Web | `window.speechSynthesis` | partly | only `localService` voices count — Chrome lists network voices that fail offline |

## Model

All calls are non-blocking. Engine callbacks arrive on platform threads and are queued
natively; the app drains them from its game loop:

```csharp
Speech.Init();                          // once at app start (Android initialises asynchronously)
Speech.OnEvent += e => { ... };         // Ready / Started / Done / Error, each with the utterance id

if (Speech.Available("he"))             // an OFFLINE voice for the language is installed NOW
    int id = Speech.Say("שלום", "he");  // interrupts the current utterance; 0 = no voice
Speech.Stop();
Speech.OpenVoiceSettings();             // the system page where voices are installed

// game loop, next to your other Poll calls:
Speech.Poll();
```

`lang` is a BCP-47 tag (`"he"`, `"pt-BR"`, `"en-US"`): a bare language matches any region, a
full tag prefers its exact region and falls back to the language. `Speech.Available(lang)` is
the contract every consumer builds on — re-check it at game start (the user can install or
remove voices at any time) and switch to a no-speech tier when it is false. On a platform
without a backend it answers `false` for every language and `Say` returns 0, so call sites
need no `#if`. `Speech.Supported` says whether a backend exists at all; `Speech.Speaking` is
the utterance id in flight (0 = idle).

## Integration (consuming app)

1. **Managed sources** — in the app `.csproj`:

   ```xml
   <Compile Include="$(SokolNetHome)/plugins/Speech/managed/*.cs">
       <Link>Plugins\Speech\%(Filename)%(Extension)</Link>
   </Compile>
   ```

   The managed layer keys on the usual platform symbols: `__ANDROID__`, `__IOS__`, `__MACOS__`,
   `__WINDOWS__`, `__LINUX__`, `WEB` (see `templates/template_app/template.csproj`). A build
   that defines none of them gets the stub (`Supported == false`).

2. **`Directory.Build.props`** — the `*Path` properties wire the native lib and mark the plugin
   ACTIVE to the builder, which then injects `platform/android/manifest/Queries.xml`
   (Android 11+ package visibility for the TTS engine — without it `TextToSpeech` cannot bind
   the engine) and compiles the Java helper:

   ```xml
   <PropertyGroup>
      <AndroidNativeLibrary_sokol_speechPath>../../plugins/Speech/libs/android</AndroidNativeLibrary_sokol_speechPath>
      <AndroidJavaSource_sokolspeechPath>../../plugins/Speech/platform/android/java</AndroidJavaSource_sokolspeechPath>
      <IOSNativeLibrary_sokol_speechPath>../../plugins/Speech/libs/ios/arm64/release</IOSNativeLibrary_sokol_speechPath>
   </PropertyGroup>
   ```

3. **macOS / Windows** — copy the native library next to the executable in the app's
   `CopyCustomContent*` targets, as for the Share plugin:

   ```xml
   <Copy SourceFiles="$(SokolNetHome)/plugins/Speech/libs/macos/$(OSArch)/release/libsokol_speech.dylib" DestinationFolder="$(OutDir)" SkipUnchangedFiles="true" />
   <Copy SourceFiles="$(SokolNetHome)/plugins/Speech/libs/windows/$(OSArch)/release/sokol_speech.dll"     DestinationFolder="$(OutDir)" SkipUnchangedFiles="true" />
   ```

4. **Web** — add the static archive to the WASM link (no `--js-library` is needed; the JS is
   inline `EM_JS`):

   ```xml
   <NativeFileReference Include="$(SokolNetHome)/plugins/Speech/libs/emscripten/x86/release/sokol_speech.a" />
   ```

5. **Linux** — nothing to link. Install `speech-dispatcher` (and a voice) for speech to be available.

No permissions, entitlements or Info.plist keys are needed on any platform.

## Audio session (iOS)

`AVSpeechSynthesizer` uses the **app's** audio session (`usesApplicationAudioSession`, the
default), so it mixes with whatever the app already plays through that session (e.g. a
MiniAudio engine) and follows the category the app configured. The plugin does not touch the
session. If the app's category is Ambient, speech obeys the ring/silent switch like the rest of
the app's audio.

## Layout

```
managed/   Speech.cs (public API + events + Linux spd-say backend)  SokolSpeech.cs (P/Invoke)
native/    sokol_speech.h            C ABI (event queue, poll model)
           sokol_speech_queue.c      shared lock-protected event ring (pthread / Win32 CS)
           sokol_speech_android.c    JNI bridge -> com.sokol.speech.SokolSpeech
           sokol_speech_apple.m      AVSpeechSynthesizer (iOS + macOS)
           sokol_speech_winrt.cpp    Windows.Media.SpeechSynthesis + MediaPlayer (C++/WinRT)
           sokol_speech_web.c        speechSynthesis via EM_JS (Emscripten)
platform/android/
           java/com/sokol/speech/SokolSpeech.java
           manifest/Queries.xml      <queries> for android.intent.action.TTS_SERVICE
           gradle-deps.txt           (none — the TTS API is part of the platform)
scripts/   build-android.sh  build-ios.sh  build-macos.sh  build-windows.ps1  build-web.sh  build-linux.sh
libs/      prebuilt outputs (committed): android/<abi>/release/libsokol_speech.so,
           ios/<target>/{debug,release}/sokol_speech.framework (built by CI), macos/<arch>/release/libsokol_speech.dylib
           (built by CI), windows/<arch>/release/sokol_speech.dll (built by CI), emscripten/x86/release/sokol_speech.a
```

Rebuild native libs after changing `native/`:

```bash
export ANDROID_NDK=...            # for Android
./plugins/Speech/scripts/build-android.sh
./plugins/Speech/scripts/build-ios.sh          # device + both simulators
./plugins/Speech/scripts/build-macos.sh
./plugins/Speech/scripts/build-web.sh
.\plugins\Speech\scripts\build-windows.ps1     # on Windows
```

CI: `.github/workflows/build-speech-plugin.yml` builds every backend on every push that
touches `plugins/Speech/` and commits the **Windows, macOS and iOS** libraries back to
`plugins/Speech/libs/` on `main` — never commit a local build of those (Android and Web are still
compile checks for their locally built, committed outputs). A change to any file under `native/` is not done
until every job of that run is green — the Windows backend is compiled only there.
