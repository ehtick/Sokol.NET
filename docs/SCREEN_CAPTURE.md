# In-App Screen Capture (GPU readback → PNG)

Take a screenshot **from inside the app**, off the GPU, with no OS screenshot tooling involved.

Two pieces:

| Path | What it is |
|------|------------|
| `ext/sokol_capture.h` | C shim: reads a sokol-gfx render-target image back into CPU memory (`scap_*`) |
| `src/sokol/ScreenCapture.cs` | C# helper: renders one frame offscreen, reads it back, writes a PNG |

Bindings are generated into `src/sokol/generated/SCapture.cs` (prefix `scap_`, registered in
`bindgen/gen.py`) — do not hand-edit them, see `docs/C-Internal-Wrappers-Auto-Generation.md`.

## Why it exists

`sokol_gfx.h` deliberately has **no** readback API — a GPU→CPU copy forces a sync stall. But it *does*
expose the native texture handle per backend (`sg_mtl_query_image_info`, `sg_gl_query_image_info`, …),
which is enough to implement readback *outside* sokol_gfx. **No sokol fork is needed**, and the shim
lives in `ext/` (not the `ext/sokol` submodule).

OS screenshots (`screencapture`, `adb shell screencap`, `idevicescreenshot`) cover most day-to-day
spot-checks and should stay the default. Use this instead when they can't do the job:

- an **iOS 17+ device** — developer services moved behind a RemoteXPC tunnel, so lockdown-based
  `idevicescreenshot` fails with *"Could not start screenshotr service"* and `devicectl` has no
  screenshot subcommand at all;
- **headless / CI** — every OS route needs a live, visible, unlocked screen (a sleeping phone
  screencaps solid black; `screencapture -x` grabs the current macOS Space, not your window);
- **deterministic pixel diffs** — a golden-image compare catches a stray draw that a geometry/layout
  audit cannot see;
- **Web**, where there is no OS-level route at all.

## Using it

Replace the swapchain `sg_begin_pass` and the `sg_commit` in your frame callback:

```csharp
ScreenCapture.BeginFrame(passAction);   // swapchain pass, or the capture target when armed
    ... draw the frame as usual ...
sg_end_pass();
ScreenCapture.Commit();                 // sg_commit(), then readback + PNG on a capture frame
```

Then arm a capture from anywhere (a debug key, a test harness command, a CI script):

```csharp
ScreenCapture.Request(Path.Combine(prefDir, "screenshots", "catalog.png"));
// Pending == true until the next frame completes; then LastPath / LastError say what happened.
```

| Member | Meaning |
|---|---|
| `Supported` | the active backend implements readback |
| `Request(path)` | arm the next frame; false if unsupported or a capture is already armed |
| `Pending` | armed and not yet taken |
| `LastPath` / `LastError` | outcome of the most recent capture |
| `Cancel()` | disarm a request whose frame never rendered |
| `Shutdown()` | free the target and buffers |

## How it works, and what that costs

The capture frame is rendered into an offscreen target laid out to **exactly match the swapchain**
(colour format, depth format, sample count) — the app's pipelines were created against the swapchain,
so anything else fails sokol validation. With MSAA the frame renders into a multisampled attachment
that resolves into the single-sample image that is read back.

- **The captured frame is not presented.** The swapchain gets a plain clear, so a capture costs one
  black frame on screen. Captures are rare (harness/CI driven), so there is deliberately no
  blit-back pass and therefore no extra shader to compile per backend.
- **Readback happens after `sg_commit()`.** Metal only orders work by commit order; reading earlier
  would race the render and capture garbage.
- **The target is freed after each capture.** MSAA colour + depth at full phone resolution is ~100 MB
  of GPU memory — too much to hold between screenshots. sokol recycles the pool slots, so a long
  sweep does not leak them.
- Output is always tightly-packed **RGBA8, top-down**, whatever the backend: GL render targets are
  stored bottom-up and Metal swapchain-format targets are BGRA8, and the shim normalises both.
- The PNG is written by `ScreenCapture` in managed code (8-bit RGB, filter 0, in-box zlib), so an
  encoding change needs no native rebuild. Alpha is dropped — screenshots are opaque.

## Backend support

| Backend | Status |
|---|---|
| Metal (macOS) | supported |
| Metal (iOS) | supported |
| GLES3 (Android) | supported |
| GLES3 / WebGL2 (Emscripten) | shares the GL path; compiles, not yet exercised |
| GLCORE (Linux) | shares the GL path; compiles, not yet exercised |
| D3D11 (Windows) | **not implemented** — `scap_supported()` returns false with a clear error |

D3D11 readback is a staging texture + `CopyResource` + `Map`; it was left out rather than shipped
unverified, because a compile error in that branch would break the whole Windows `sokol` library.

⚠ The shim is C code compiled into the `sokol` native library, so **`libs/<platform>/` must be
rebuilt for every platform you claim support for** (`docs/BUILD_SYSTEM.md`) — bindings without a
rebuilt library give you `EntryPointNotFoundException`.

## Getting the file off a device

`ScreenCapture` writes wherever you point it; app-private storage is the portable choice
(`sfs_get_pref_path`, see `docs/SOKOL_FILESYSTEM.md`).

```bash
# Android (debuggable build): copy out of the private dir, then pull
adb -s <serial> shell run-as <pkg> cp <prefdir>/screenshots/shot.png /sdcard/shot.png
adb -s <serial> pull /sdcard/shot.png .

# iOS (development-signed app)
ios-deploy --bundle_id <bundle-id> --download=/Library/Application\ Support/... --to .
```

Shrink before viewing — a retina/phone capture is large: `sips -Z 900 shot.png --out small.png`.
