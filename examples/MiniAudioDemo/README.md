# MiniAudioDemo

A cross-platform audio demo built with [Sokol.NET](../../README.md), demonstrating real-time sound-effect and music playback powered by [miniaudio](https://miniaud.io).

## Screenshot

![MiniAudio Demo](screenshot/Screenshot%202026-05-10%20at%2019.15.52.png)

## What It Demonstrates

- **miniaudio integration** — P/Invoke bindings to the [miniaudio](https://miniaud.io) C library (`ma_engine`, `ma_sound`, `ma_resource_manager`)
- **Async asset loading** — all audio files are loaded via `SFilesystem.LoadFileAsync`; audio data is owned by a `SharedBuffer` (GC-pinned) and registered with miniaudio's resource manager
- **Concurrent sound effects** — each button press spawns an independent `ma_sound` instance; multiple sounds of the same type can play simultaneously
- **Looping music** — a single persistent `ma_sound` toggles play/stop with `ma_sound_set_looping`
- **Finished-sound cleanup** — completed one-shot sounds are detected via `ma_sound_at_end` and freed each frame
- **Sokol.GUI layout** — UI built with the custom `Sokol.GUI` retained-mode framework (NanoVG rendering, `BoxLayout`, `Button`, `Label`)
- **Cross-platform** — Desktop (macOS Metal, Windows D3D11, Linux OpenGL), iOS (Metal), Android (GLES3), and WebAssembly (WebGL2)

## Audio Assets

| File | Type | Description |
|------|------|-------------|
| `Music/music.ogg` | OGG | Background music loop |
| `Sounds/BigExplosion.wav` | WAV | Large explosion effect |
| `Sounds/MachineGun.wav` | WAV | Machine gun burst |
| `Sounds/NutThrow.wav` | WAV | Projectile throw |
| `Sounds/PlayerFist.wav` | WAV | Punch swing |
| `Sounds/PlayerFistHit.wav` | WAV | Punch impact |
| `Sounds/PlayerLand.wav` | WAV | Landing thud |
| `Sounds/Powerup.wav` | WAV | Power-up pickup |
| `Sounds/SmallExplosion.wav` | WAV | Small explosion effect |

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
