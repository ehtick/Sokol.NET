# MiniAudioDemo

A cross-platform audio demo built with [Sokol.NET](../../README.md), demonstrating real-time sound-effect and music playback powered by [miniaudio](https://miniaud.io).

## Screenshots

| Sound FX | Mixing |
|----------|--------|
| ![Sound FX tab](screenshot/Screenshot%202026-05-11%20at%2012.53.28.png) | ![Mixing tab](screenshot/Screenshot%202026-05-11%20at%2012.53.32.png) |

| Fade | Waveform |
|------|----------|
| ![Fade tab](screenshot/Screenshot%202026-05-11%20at%2012.53.34.png) | ![Waveform tab](screenshot/Screenshot%202026-05-11%20at%2012.53.55.png) |

| Spatial | EQ |
|---------|----|
| ![Spatial tab](screenshot/Screenshot%202026-05-11%20at%2013.00.06.png) | ![EQ tab](screenshot/Screenshot%202026-05-11%20at%2013.00.31.png) |

| Spectrum | Piano |
|----------|-------|
| ![Spectrum tab](screenshot/Screenshot%202026-05-11%20at%2016.42.52.png) | ![Piano tab](screenshot/Screenshot%202026-05-11%20at%2016.43.15.png) |

## What It Demonstrates

- **miniaudio integration** — P/Invoke bindings to the [miniaudio](https://miniaud.io) C library (`ma_engine`, `ma_sound`, `ma_resource_manager`)
- **Async asset loading** — all audio files are loaded via `SFilesystem.LoadFileAsync`; audio data is owned by a `SharedBuffer` (GC-pinned) and registered with miniaudio's resource manager
- **Concurrent sound effects** — each button press spawns an independent `ma_sound` instance; multiple sounds of the same type can play simultaneously
- **Looping music** — a single persistent `ma_sound` toggles play/stop with `ma_sound_set_looping`
- **Music track picker** — Sound FX, Fade, and Spatial tabs each expose a `ComboBox` to select from 6 bundled music tracks at runtime
- **Spatialized audio** — Spatial tab lets you drag a sound source on a 2D canvas; `ma_sound_set_position` / `ma_engine_listener_set_position` update the 3D position in real time with configurable attenuation model and min/max distance
- **Real-time EQ** — EQ tab chains four `ma_lpf_node` / `ma_hpf_node` / `ma_peak_eq_node` filter nodes and visualises the frequency response alongside a VU meter fed from the decoder
- **Spectrum analyzer** — Spectrum tab shows a real-time FFT bar chart with frequency-gradient colouring (orange→green→cyan), a goniometer (stereo phase/Lissajous scope), and a scrolling spectrogram; all fed from the `onProcess` engine capture ring buffer
- **Piano keyboard** — Piano tab renders an interactive multi-octave piano keyboard; clicking a key plays the corresponding note via miniaudio and highlights the key in blue; C-note labels are drawn on the keyboard
- **Waveform visualiser** — oscilloscope and peak VU meter rendered each frame from decoded PCM data
- **Finished-sound cleanup** — completed one-shot sounds are detected via `ma_sound_at_end` and freed each frame
- **Scrollable tabs on mobile** — EQ, Waveform, and Spatial tabs are wrapped in `ScrollView` so all controls are reachable on small screens (iPhone / Android)
- **Sokol.GUI layout** — UI built with the custom `Sokol.GUI` retained-mode framework (NanoVG rendering, `BoxLayout`, `Button`, `Label`, `ComboBox`, `Slider`, `ScrollView`)
- **Cross-platform** — Desktop (macOS Metal, Windows D3D11, Linux OpenGL), iOS (Metal), Android (GLES3), and WebAssembly (WebGL2)

## Audio Assets

| File | Type | Description |
|------|------|-------------|
| `Music/bombinsound-upbeat-music-kids-music-499480.ogg` | OGG | Upbeat kids music |
| `Music/openmindaudio-upbeat-background-music-clear-momentum-short-preview-497394.ogg` | OGG | Clear Momentum |
| `Music/sonican-cooking-background-music-loop-486763.ogg` | OGG | Cooking background loop |
| `Music/soulfuljamtracks-classical-background-music-483075.ogg` | OGG | Classical background |
| `Music/white_records-inception-cinematic-background-music-for-video-stories-31-second-478713.ogg` | OGG | Cinematic background |
| `Sounds/BigExplosion.wav` | WAV | Large explosion effect |
| `Sounds/MachineGun.wav` | WAV | Machine gun burst |
| `Sounds/NutThrow.wav` | WAV | Projectile throw |
| `Sounds/PlayerFist.wav` | WAV | Punch swing |
| `Sounds/PlayerFistHit.wav` | WAV | Punch impact |
| `Sounds/PlayerLand.wav` | WAV | Landing thud |
| `Sounds/Powerup.wav` | WAV | Power-up pickup |
| `Sounds/SmallExplosion.wav` | WAV | Small explosion effect |

All music files were sourced from [Pixabay](https://pixabay.com/). The Pixabay Content License applies — see [https://pixabay.com/service/license-summary/](https://pixabay.com/service/license-summary/).

## Project Files

| File | Purpose |
|------|---------|
| [Source/MiniAudioDemo-app.cs](Source/MiniAudioDemo-app.cs) | Main app: engine init, async file loading, sound/music playback, UI, frame/cleanup |
| [Source/Program.cs](Source/Program.cs) | `sapp_desc` entry point |

## Build and Run

```bash
# Desktop (macOS / Windows / Linux) — JIT mode
dotnet run --project MiniAudioDemo.csproj

# Prepare assets (required before iOS / Android / Web builds)
dotnet run --project ../../tools/SokolApplicationBuilder -- --task prepare --architecture desktop --path .

# Android APK
dotnet run --project ../../tools/SokolApplicationBuilder -- --task build --architecture android --type release --path .

# iOS
dotnet run --project ../../tools/SokolApplicationBuilder -- --task build --architecture ios --type release --path .

# WebAssembly
dotnet run --project ../../tools/SokolApplicationBuilder -- --task build --architecture web --path .
```
