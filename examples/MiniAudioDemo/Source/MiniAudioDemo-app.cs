using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Sokol;
using Sokol.GUI;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SLog;
using static Sokol.NanoVG;
using static Sokol.STM;
using static MiniAudioNS.MiniAudio;
using static TinySoundFont.TSF;
using static TinySoundFont.TML;

public static unsafe class MiniaudiodemoApp
{

    // ── Audio file catalogue ──────────────────────────────────────────────────
    struct AudioFile { public string Path; public string Label; public bool IsMusic; }
    static readonly AudioFile[] _audioFiles =
    {
        new() { Path = "Music/bombinsound-upbeat-music-kids-music-499480.ogg",                                         Label = "Upbeat Kids",     IsMusic = true  },
        new() { Path = "Music/openmindaudio-upbeat-background-music-clear-momentum-short-preview-497394.ogg",          Label = "Clear Momentum",  IsMusic = true  },
        new() { Path = "Music/sonican-cooking-background-music-loop-486763.ogg",                                       Label = "Cooking Loop",    IsMusic = true  },
        new() { Path = "Music/soulfuljamtracks-classical-background-music-483075.ogg",                                 Label = "Classical",       IsMusic = true  },
        new() { Path = "Music/white_records-inception-cinematic-background-music-for-video-stories-31-second-478713.ogg", Label = "Cinematic",    IsMusic = true  },
        new() { Path = "Sounds/BigExplosion.wav",    Label = "Big Explosion",   IsMusic = false },
        new() { Path = "Sounds/MachineGun.wav",      Label = "Machine Gun",     IsMusic = false },
        new() { Path = "Sounds/NutThrow.wav",        Label = "Nut Throw",       IsMusic = false },
        new() { Path = "Sounds/PlayerFist.wav",      Label = "Player Fist",     IsMusic = false },
        new() { Path = "Sounds/PlayerFistHit.wav",   Label = "Fist Hit",        IsMusic = false },
        new() { Path = "Sounds/PlayerLand.wav",      Label = "Player Land",     IsMusic = false },
        new() { Path = "Sounds/Powerup.wav",         Label = "Powerup",         IsMusic = false },
        new() { Path = "Sounds/SmallExplosion.wav",  Label = "Small Explosion", IsMusic = false },
    };

    // ── Loaded file data (pinned via SharedBuffer; registered with resource manager) ─
    static readonly Dictionary<string, SharedBuffer> _loaded = new();

    // ── One entry per concurrently playing one-shot sound ─────────────────────
    struct ActiveSound { public ma_sound* Sound; public bool CheckIsPlaying; }
    static readonly List<ActiveSound> _active = new();

    // ── Music – single persistent looping instance ────────────────────────────
    static ma_sound* _musicSound;

    // ── miniaudio engine ──────────────────────────────────────────────────────
    static ma_engine* _engine;
    static bool       _engineReady;

    // ── GUI state ─────────────────────────────────────────────────────────────
    static sg_pass_action _passAction;
    static IntPtr         _vg = IntPtr.Zero;
    static Screen?        _screen;
    static Label?         _statusLabel;
    static Label?         _activeCountLabel;
    static Button?        _musicButton;

    // ── Additional tab sounds (freed in Cleanup) ──────────────────────────────
    static ma_sound* _mixSound;
    static ma_sound* _fadeSound;

    // ── Engine tab — live stats labels updated each frame ─────────────────────
    static Label? _engActiveLabel;
    static Label? _engUptimeLabel;

    // ── Waveform Generator tab ─────────────────────────────────────────────────
    static ma_waveform*        _waveformDS;
    static ma_sound*           _waveformSound;
    static OscilloscopeWidget? _oscWidget;
    static ma_waveform*        _vuWaveform;   // shadow waveform for VU meter PCM reads
    static VuMeterWidget?      _vuWidget;

    // ── Real Piano tab ────────────────────────────────────────────────────────
    static PianoKeyboardWidget? _realPianoWidget;
    static readonly unsafe ma_sound*[] _pianoVoices = new ma_sound*[24];
    static readonly string[] _pianoNoteFiles =
    {
        "Piano/cc.mp3",  "Piano/ccs.mp3", "Piano/dd.mp3",  "Piano/dds.mp3",
        "Piano/ee.mp3",  "Piano/ff.mp3",  "Piano/ffs.mp3", "Piano/gg.mp3",
        "Piano/ggs.mp3", "Piano/aa.mp3",  "Piano/aas.mp3", "Piano/bb.mp3",
        "Piano/c1.mp3",  "Piano/c1s.mp3", "Piano/d1.mp3",  "Piano/d1s.mp3",
        "Piano/e1.mp3",  "Piano/f1.mp3",  "Piano/f1s.mp3", "Piano/g1.mp3",
        "Piano/g1s.mp3", "Piano/a1.mp3",  "Piano/a1s.mp3", "Piano/b1.mp3",
    };

    // ── SoundFont tab ─────────────────────────────────────────────────────────
    static IntPtr               _tsf            = IntPtr.Zero;
    static tml_message*         _tmlFirst       = null;
    static tml_message*         _tmlCurrent     = null;
    static double               _tmlMsec        = 0.0;
    static volatile bool        _tmlPlaying     = false;
    static double               _tmlTotalMs     = 0.0;
    static int                  _tsfSampleRate  = 44100;
    static SharedBuffer?        _sf2Buf;
    static SharedBuffer?        _midBuf;
    static Label?               _tsfVoiceLabel;
    static Label?               _tsfTimeLabel;
    static Button?              _tsfPlayBtn;
    static ComboBox?            _tsfPresetCombo;
    static PianoKeyboardWidget? _tsfPianoWidget;
    static ma_waveform*         _tsfKeepAliveWaveform;
    static ma_sound*            _tsfKeepAliveSound;

    // ── Spectrum Analyzer tab ──────────────────────────────────────────────────
    static ma_sound*           _specSound;
    // Lock-free ring buffer filled by the engine's onProcess callback.
    // Audio thread is the sole writer; main thread reads the most-recent SpecFrames.
    const  int    CaptureRingFrames = 8192;          // power of 2
    const  int    CaptureRingSize   = CaptureRingFrames * 2;  // stereo floats
    static float* _captureRing;
    static volatile int _captureWritePos;
    static SpectrumWidget?     _specWidget;
    static GoniometerWidget?   _gonioWidget;
    static SpectrogramWidget?  _spectroWidget;

    // ── Spatialization tab ────────────────────────────────────────────────────
    static ma_sound*                    _spatSound;
    static SpatializationCanvasWidget?  _spatCanvas;

    // ── Equalizer tab ─────────────────────────────────────────────────────────
    static ma_sound*        _eqSound;
    static ma_decoder*      _eqDecoder;     // monitoring decoder for VU meter (not in audio graph)
    static VuMeterWidget?   _eqVuWidget;
    static EqBarsWidget?    _eqBarsWidget;
    static ma_loshelf_node* _eqLoShelf;
    static ma_peak_node*    _eqLowMid;
    static ma_peak_node*    _eqMid;
    static ma_peak_node*    _eqHiMid;
    static ma_hishelf_node* _eqHiShelf;
    static bool             _eqNodesReady;
    static float            _eqPan;          // -1..+1, mirrors balance slider

    [UnmanagedCallersOnly]
    static void Init()
    {
        sg_setup(new sg_desc
        {
            environment = sglue_environment(),
            logger = { func = &slog_func },
        });

        stm_setup();
        SFilesystem.Initialize();

        _engine = (ma_engine*)NativeMemory.AllocZeroed((nuint)sizeof(ma_engine));
        _captureRing = (float*)NativeMemory.AllocZeroed((nuint)(CaptureRingSize * sizeof(float)));
        _captureWritePos = 0;
        var engCfg = ma_engine_config_init();
        engCfg.onProcess = &OnEngineProcess;
        _engineReady = ma_engine_init(in engCfg, _engine) == ma_result.MA_SUCCESS;

        _passAction = default;
        _passAction.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        _passAction.colors[0].clear_value = new sg_color { r = 0.12f, g = 0.12f, b = 0.15f, a = 1f };
        _passAction.depth.load_action = sg_load_action.SG_LOADACTION_CLEAR;
        _passAction.depth.clear_value = 1.0f;
        _passAction.stencil.load_action = sg_load_action.SG_LOADACTION_CLEAR;
        _passAction.stencil.clear_value = 0;

        _vg = nvgCreateSokol(NVG_ANTIALIAS | NVG_STENCIL_STROKES);
        _screen = Screen.Initialize(_vg);
        FontRegistry.Instance.RegisterAsync(_vg, "sans", "fonts/Roboto-Regular.ttf");
        FontRegistry.Instance.RegisterAsync(_vg, "bold", "fonts/Roboto-Bold.ttf");
        FontRegistry.Instance.RegisterFallbackAsync(_vg, "symbols", "fonts/NotoSansSymbols2-Regular.ttf", ["sans", "bold"]);

        foreach (var af in _audioFiles)
            LoadAudioFileAsync(af.Path);

        foreach (var p in _pianoNoteFiles)
            LoadAudioFileAsync(p);

        SFilesystem.LoadFileAsync("SoundFont/florestan-subset.sf2", (_, bytes, status) =>
        {
            if (status != SFileLoadStatus.Success || bytes == null) return;
            _sf2Buf = SharedBuffer.Create((uint)bytes.Length);
            bytes.CopyTo(_sf2Buf.Buffer, 0);
            _tsf = tsf_load_memory((void*)_sf2Buf.GetBufferPointer(), bytes.Length);
            if (_tsf != IntPtr.Zero && _engineReady)
            {
                _tsfSampleRate = (int)ma_engine_get_sample_rate(in *_engine);
                tsf_set_output(_tsf, TinySoundFont.TSF.TSFOutputMode.TSF_STEREO_INTERLEAVED, _tsfSampleRate, 0f);
                tsf_set_max_voices(_tsf, 64);
            }
            if (_tsfPresetCombo != null && _tsf != IntPtr.Zero)
            {
                int count = tsf_get_presetcount(_tsf);
                var names = new string[count];
                for (int i = 0; i < count; i++)
                    names[i] = $"{i}: {tsf_get_presetname(_tsf, i)}";
                _tsfPresetCombo.SetItems(names);
                _tsfPresetCombo.SelectedIndex = 0;
            }
        });

        SFilesystem.LoadFileAsync("SoundFont/venture.mid", (_, bytes, status) =>
        {
            if (status != SFileLoadStatus.Success || bytes == null) return;
            _midBuf = SharedBuffer.Create((uint)bytes.Length);
            bytes.CopyTo(_midBuf.Buffer, 0);
            _tmlFirst = tml_load_memory((void*)_midBuf.GetBufferPointer(), bytes.Length);
            if (_tmlFirst != null)
            {
                int ch = 0, prog = 0, notes = 0;
                uint tFirst = 0, tLen = 0;
                tml_get_info(_tmlFirst, ref ch, ref prog, ref notes, ref tFirst, ref tLen);
                _tmlTotalMs = tLen;
            }
        });

        BuildUI();
    }

    private static void StartTSFLoopingWave()
    {
        if(_tsfKeepAliveSound != null) return;
        // Silent looping waveform — keeps OnEngineProcess firing with real frame
        // counts even when no ma_sound is playing, so TSF piano renders audio.
        if (_engineReady)
        {
            uint sr = ma_engine_get_sample_rate(in *_engine);
            _tsfKeepAliveWaveform = (ma_waveform*)NativeMemory.AllocZeroed((nuint)sizeof(ma_waveform));
            var kaCfg = ma_waveform_config_init(ma_format.ma_format_f32, 2, sr,
                                                ma_waveform_type.ma_waveform_type_sine, 0.0, 440.0);
            ma_waveform_init(in kaCfg, _tsfKeepAliveWaveform);
            _tsfKeepAliveSound = (ma_sound*)NativeMemory.AllocZeroed((nuint)sizeof(ma_sound));
            uint kaFlags = (uint)ma_sound_flags.MA_SOUND_FLAG_NO_SPATIALIZATION;
            if (ma_sound_init_from_data_source(_engine, (IntPtr)_tsfKeepAliveWaveform, kaFlags, null, _tsfKeepAliveSound)
                == ma_result.MA_SUCCESS)
            {
                ma_sound_set_looping(_tsfKeepAliveSound, 1);
                ma_sound_start(_tsfKeepAliveSound);
            }
        }
    }

    private static void StopTSFLoopingWave()
    {
        if (_tsfKeepAliveSound != null)
        {
            ma_sound_stop(_tsfKeepAliveSound);
            ma_sound_uninit(_tsfKeepAliveSound);
            NativeMemory.Free(_tsfKeepAliveSound);
            _tsfKeepAliveSound = null;
        }
        if (_tsfKeepAliveWaveform != null)
        {
            ma_waveform_uninit(_tsfKeepAliveWaveform);
            NativeMemory.Free(_tsfKeepAliveWaveform);
            _tsfKeepAliveWaveform = null;
        }
    }

    // ── File loading ──────────────────────────────────────────────────────────

    static void LoadAudioFileAsync(string path)
    {
        SFilesystem.LoadFileAsync(path, (filePath, bytes, status) =>
        {
            if (status != SFileLoadStatus.Success || bytes == null) return;

            var buf = SharedBuffer.Create((uint)bytes.Length);
            bytes.CopyTo(buf.Buffer, 0);
            _loaded[filePath] = buf;

            if (_engineReady)
            {
                var rm  = ma_engine_get_resource_manager(_engine);
                var ptr = (void*)buf.GetBufferPointer();
                ma_resource_manager_register_encoded_data(rm, filePath, ptr, (nuint)buf.Size);
            }
        });
    }

    // ── Sound-effect playback (each press = independent concurrent instance) ──

    static void PlaySound(string path)
    {
        if (!_engineReady || !_loaded.ContainsKey(path))
        {
            SetStatus("Still loading — try again in a moment");
            return;
        }

        var sound = (ma_sound*)NativeMemory.AllocZeroed((nuint)sizeof(ma_sound));
        uint flags = (uint)(ma_sound_flags.MA_SOUND_FLAG_DECODE |
                            ma_sound_flags.MA_SOUND_FLAG_NO_SPATIALIZATION);
        var result = ma_sound_init_from_file(_engine, path, flags, null, null, sound);
        if (result != ma_result.MA_SUCCESS)
        {
            SetStatus($"Sound init failed: {result}");
            NativeMemory.Free(sound);
            return;
        }

        ma_sound_start(sound);
        _active.Add(new ActiveSound { Sound = sound });
        SetStatus($"Playing: {System.IO.Path.GetFileName(path)}");
    }

    // ── Music playback (single looping instance, toggle play / stop) ──────────

    static void ToggleMusic(string path)
    {
        if (!_engineReady) return;

        if (_musicSound != null)
        {
            ma_sound_stop(_musicSound);
            ma_sound_uninit(_musicSound);
            NativeMemory.Free(_musicSound);
            _musicSound = null;
            UpdateMusicButton(false);
            SetStatus("Music stopped");
            return;
        }

        if (!_loaded.ContainsKey(path))
        {
            SetStatus("Still loading — try again in a moment");
            return;
        }

        _musicSound = (ma_sound*)NativeMemory.AllocZeroed((nuint)sizeof(ma_sound));
        uint flags = (uint)(ma_sound_flags.MA_SOUND_FLAG_DECODE |
                            ma_sound_flags.MA_SOUND_FLAG_NO_SPATIALIZATION);
        var result = ma_sound_init_from_file(_engine, path, flags, null, null, _musicSound);
        if (result != ma_result.MA_SUCCESS)
        {
            SetStatus($"Music init failed: {result}");
            NativeMemory.Free(_musicSound);
            _musicSound = null;
            return;
        }

        ma_sound_set_looping(_musicSound, 1);
        ma_sound_start(_musicSound);
        UpdateMusicButton(true);
        SetStatus("♪ Now playing: music (looping)");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void UpdateMusicButton(bool playing)
    {
        if (_musicButton != null)
            _musicButton.Text = playing ? "■  Stop" : "▶  Play";
    }

    static void SetStatus(string msg)
    {
        if (_statusLabel != null) _statusLabel.Text = msg;
    }

    // ── Frame ─────────────────────────────────────────────────────────────────

    [UnmanagedCallersOnly]
    static void Frame()
    {
        SFilesystem.Update();

        if (_tsf != IntPtr.Zero && tsf_active_voice_count(_tsf) == 0 && !_tmlPlaying)
        {
            StopTSFLoopingWave();
        }


        // Drain finished one-shot sounds
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var s = _active[i];
            bool done = s.CheckIsPlaying
                ? ma_sound_is_playing(in *s.Sound) == 0
                : ma_sound_at_end(in *s.Sound) != 0;
            if (done)
            {
                ma_sound_uninit(s.Sound);
                NativeMemory.Free(s.Sound);
                _active.RemoveAt(i);
            }
        }

        if (_activeCountLabel != null)
            _activeCountLabel.Text = $"Active: {_active.Count}";

        if (_engActiveLabel != null)
            _engActiveLabel.Text = $"Active sounds: {_active.Count}";

        if (_engUptimeLabel != null && _engineReady)
            _engUptimeLabel.Text = $"Uptime: {ma_engine_get_time_in_milliseconds(in *_engine) / 1000.0:F1} s";

        // Sample shadow waveform for VU meter
        if (_vuWidget != null && _vuWaveform != null)
        {
            const int Frames = 512;
            Span<float> buf = stackalloc float[Frames * 2];
            fixed (float* p = buf)
            {
                ulong read = 0;
                ma_waveform_read_pcm_frames(_vuWaveform, p, Frames, ref read);
                float peakL = 0f, peakR = 0f, sumSqL = 0f, sumSqR = 0f;
                for (int i = 0; i < (int)read; i++)
                {
                    float l = MathF.Abs(p[i * 2]);
                    float r = MathF.Abs(p[i * 2 + 1]);
                    if (l > peakL) peakL = l;
                    if (r > peakR) peakR = r;
                    sumSqL += l * l;
                    sumSqR += r * r;
                }
                float inv = read > 0 ? 1f / read : 0f;
                float rmsL = MathF.Sqrt(sumSqL * inv);
                float rmsR = MathF.Sqrt(sumSqR * inv);
                _vuWidget.UpdateLevels(peakL, rmsL, peakR, rmsR, (float)sapp_frame_duration());
            }
        }

        // Sample EQ monitoring decoder for VU meter
        if (_eqVuWidget != null && _eqDecoder != null && _eqSound != null)
        {
            ulong cursor = 0;
            ma_sound_get_cursor_in_pcm_frames(in *_eqSound, ref cursor);
            ma_decoder_seek_to_pcm_frame(_eqDecoder, cursor);
            const int EqFrames = 512;
            Span<float> eqBuf = stackalloc float[EqFrames * 2];
            fixed (float* p = eqBuf)
            {
                ulong read = 0;
                ma_decoder_read_pcm_frames(_eqDecoder, p, EqFrames, ref read);
                float peakL = 0f, peakR = 0f, sumSqL = 0f, sumSqR = 0f;
                for (int i = 0; i < (int)read; i++)
                {
                    float l = MathF.Abs(p[i * 2]);
                    float r = MathF.Abs(p[i * 2 + 1]);
                    if (l > peakL) peakL = l;
                    if (r > peakR) peakR = r;
                    sumSqL += l * l;
                    sumSqR += r * r;
                }
                float inv = read > 0 ? 1f / read : 0f;
                float rmsL = MathF.Sqrt(sumSqL * inv);
                float rmsR = MathF.Sqrt(sumSqR * inv);
                // Scale by pan so the VU reflects what's audible on each channel
                float lFactor = _eqPan >= 0f ? 1f - _eqPan : 1f;
                float rFactor = _eqPan <= 0f ? 1f + _eqPan : 1f;
                _eqVuWidget.UpdateLevels(peakL * lFactor, rmsL * lFactor, peakR * rFactor, rmsR * rFactor, (float)sapp_frame_duration());
            }
        }

        // Spectrum Analyzer tab — read most-recent PCM from engine capture ring buffer.
        // _captureRing is filled by OnEngineProcess (audio thread) with frames as they
        // exit the mixer, so no cursor estimation or hardware-latency guessing is needed.
        if ((_specWidget != null || _gonioWidget != null || _spectroWidget != null) && _captureRing != null && _specSound != null)
        {
            const int SpecFrames = 2048;
            int wp   = _captureWritePos;            // snapshot the volatile write head
            int mask = CaptureRingSize - 1;
            // Step back SpecFrames stereo pairs to get the most recently committed audio.
            int rp   = (wp - SpecFrames * 2 + CaptureRingSize) & mask;
            Span<float> mono = stackalloc float[SpecFrames];
            Span<float> smpL = stackalloc float[SpecFrames];
            Span<float> smpR = stackalloc float[SpecFrames];
            for (int i = 0; i < SpecFrames; i++)
            {
                smpL[i] = _captureRing[(rp + i * 2)     & mask];
                smpR[i] = _captureRing[(rp + i * 2 + 1) & mask];
                mono[i] = (smpL[i] + smpR[i]) * 0.5f;
            }
            _specWidget?.PushSamples(mono);
            _gonioWidget?.PushSamples(smpL, smpR);
            _spectroWidget?.PushSamples(mono);
        }

        if (_tsfVoiceLabel != null && _tsf != IntPtr.Zero)
            _tsfVoiceLabel.Text = $"voices:{tsf_active_voice_count(_tsf)}";

        if (_tsfTimeLabel != null && _tmlTotalMs > 0)
        {
            int eMs   = (int)_tmlMsec;
            int totMs = (int)_tmlTotalMs;
            _tsfTimeLabel.Text = $"{eMs / 60000}:{(eMs / 1000) % 60:D2} / {totMs / 60000}:{(totMs / 1000) % 60:D2}";
        }

        if (_tsfPlayBtn != null)
            _tsfPlayBtn.Text = _tmlPlaying ? "⏸  Pause" : "▶  Play";

        float winW = sapp_widthf()  ;
        float winH = sapp_heightf() ;

        _screen!.Update(winW, winH, 1.0f);

        sg_begin_pass(new sg_pass { action = _passAction, swapchain = sglue_swapchain() });
        _screen.Draw(winW, winH, 1.0f);
        sg_end_pass();
        sg_commit();
    }

    [UnmanagedCallersOnly]
    static void Event(sapp_event* e) => _screen?.DispatchEvent(e);


    // Called by the miniaudio engine on the audio thread after mixing, just before
    // frames are sent to hardware.  Writes interleaved stereo f32 into _captureRing
    // so Frame() can read the most-recent samples without any cursor estimation.
    [UnmanagedCallersOnly]
    static void OnEngineProcess(void* pUserData, float* pFramesOut, ulong frameCount)
    {
        float* ring = _captureRing;
        if (ring == null) return;
        int count = (int)((long)frameCount * 2);   // stereo pairs → individual floats
        int wp    = _captureWritePos;
        int mask  = CaptureRingSize - 1;
        for (int i = 0; i < count; i++)
            ring[(wp + i) & mask] = pFramesOut[i];
        _captureWritePos = (wp + count) & mask;

        if (_tsf == IntPtr.Zero) return;
        IntPtr tsf = _tsf;

        if (_tmlPlaying)
        {
            // Process MIDI in 64-sample blocks (TSF_RENDER_EFFECTSAMPLEBLOCK) for accurate timing.
            // Mirrors the AudioCallback in example3.c.
            const int blockSize = 64;
            double sampleMs = 1000.0 / _tsfSampleRate;
            float* stream   = pFramesOut;
            int remaining   = (int)frameCount;
            while (remaining > 0)
            {
                int block = remaining < blockSize ? remaining : blockSize;
                _tmlMsec += block * sampleMs;
                tml_message* msg = _tmlCurrent;
                while (msg != null && _tmlMsec >= msg->time)
                {
                    switch (msg->type)
                    {
                        case 0xC0: // TML_PROGRAM_CHANGE
                            tsf_channel_set_presetnumber(tsf, msg->channel, msg->data.program, msg->channel == 9 ? 1 : 0);
                            break;
                        case 0x90: // TML_NOTE_ON
                            tsf_channel_note_on(tsf, msg->channel, msg->data.key, msg->data.vel / 127.0f);
                            break;
                        case 0x80: // TML_NOTE_OFF
                            tsf_channel_note_off(tsf, msg->channel, msg->data.key);
                            break;
                        case 0xE0: // TML_PITCH_BEND
                            tsf_channel_set_pitchwheel(tsf, msg->channel, msg->data.pitch_bend);
                            break;
                        case 0xB0: // TML_CONTROL_CHANGE
                            tsf_channel_midi_control(tsf, msg->channel, msg->data.control, msg->data.value);
                            break;
                    }
                    msg = msg->next;
                }
                _tmlCurrent = msg;
                tsf_render_float(tsf, ref *stream, block, 1); // flag_mixing=1: mix into existing output
                stream    += block * 2; // stereo
                remaining -= block;
            }
            if (_tmlCurrent == null)
                _tmlPlaying = false; // reached end of MIDI file
        }
        else
        {
            // Not playing MIDI — still render TSF voices (interactive piano, etc.)
            tsf_render_float(tsf, ref *pFramesOut, (int)frameCount, 1);
        }
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        if (_waveformSound != null)
        {
            ma_sound_stop(_waveformSound);
            ma_sound_uninit(_waveformSound);
            NativeMemory.Free(_waveformSound);
            _waveformSound = null;
        }
        if (_waveformDS != null)
        {
            ma_waveform_uninit(_waveformDS);
            NativeMemory.Free(_waveformDS);
            _waveformDS = null;
        }
        if (_vuWaveform != null)
        {
            ma_waveform_uninit(_vuWaveform);
            NativeMemory.Free(_vuWaveform);
            _vuWaveform = null;
        }

        if (_spatSound != null)
        {
            ma_sound_stop(_spatSound);
            ma_sound_uninit(_spatSound);
            NativeMemory.Free(_spatSound);
            _spatSound = null;
        }

        if (_eqSound != null)
        {
            ma_sound_stop(_eqSound);
            ma_sound_uninit(_eqSound);
            NativeMemory.Free(_eqSound);
            _eqSound = null;
        }
        if (_eqDecoder != null)
        {
            ma_decoder_uninit(_eqDecoder);
            NativeMemory.Free(_eqDecoder);
            _eqDecoder = null;
        }
        if (_specSound != null)
        {
            ma_sound_stop(_specSound);
            ma_sound_uninit(_specSound);
            NativeMemory.Free(_specSound);
            _specSound = null;
        }
        if (_captureRing != null)
        {
            NativeMemory.Free(_captureRing);
            _captureRing = null;
        }
        if (_eqNodesReady)
        {
            ref ma_allocation_callbacks na = ref Unsafe.NullRef<ma_allocation_callbacks>();
            ma_loshelf_node_uninit(_eqLoShelf, in na);
            ma_peak_node_uninit(_eqLowMid,     in na);
            ma_peak_node_uninit(_eqMid,        in na);
            ma_peak_node_uninit(_eqHiMid,      in na);
            ma_hishelf_node_uninit(_eqHiShelf, in na);
            NativeMemory.Free(_eqLoShelf);  _eqLoShelf  = null;
            NativeMemory.Free(_eqLowMid);   _eqLowMid   = null;
            NativeMemory.Free(_eqMid);      _eqMid      = null;
            NativeMemory.Free(_eqHiMid);    _eqHiMid    = null;
            NativeMemory.Free(_eqHiShelf);  _eqHiShelf  = null;
            _eqNodesReady = false;
        }

        if (_mixSound != null)
        {
            ma_sound_stop(_mixSound);
            ma_sound_uninit(_mixSound);
            NativeMemory.Free(_mixSound);
        }

        if (_fadeSound != null)
        {
            ma_sound_stop(_fadeSound);
            ma_sound_uninit(_fadeSound);
            NativeMemory.Free(_fadeSound);
        }

        if (_musicSound != null)
        {
            ma_sound_stop(_musicSound);
            ma_sound_uninit(_musicSound);
            NativeMemory.Free(_musicSound);
        }

        if (_tsf != IntPtr.Zero)
        {
            tsf_note_off_all(_tsf);
            tsf_close(_tsf);
            _tsf = IntPtr.Zero;
        }
        if (_tmlFirst != null)
        {
            tml_free(_tmlFirst);
            _tmlFirst = null;
        }
        if (_sf2Buf != null) { SharedBuffer.Dispose(_sf2Buf); _sf2Buf = null; }
        if (_midBuf != null) { SharedBuffer.Dispose(_midBuf); _midBuf = null; }

        foreach (var s in _active)
        {
            ma_sound_uninit(s.Sound);
            NativeMemory.Free(s.Sound);
        }
        _active.Clear();

        for (int i = 0; i < _pianoVoices.Length; i++)
        {
            if (_pianoVoices[i] != null)
            {
                ma_sound_stop(_pianoVoices[i]);
                ma_sound_uninit(_pianoVoices[i]);
                NativeMemory.Free(_pianoVoices[i]);
                _pianoVoices[i] = null;
            }
        }

        if (_tsfKeepAliveSound != null)
        {
            ma_sound_stop(_tsfKeepAliveSound);
            ma_sound_uninit(_tsfKeepAliveSound);
            NativeMemory.Free(_tsfKeepAliveSound);
            _tsfKeepAliveSound = null;
        }
        if (_tsfKeepAliveWaveform != null)
        {
            ma_waveform_uninit(_tsfKeepAliveWaveform);
            NativeMemory.Free(_tsfKeepAliveWaveform);
            _tsfKeepAliveWaveform = null;
        }

        if (_engineReady)
        {
            var rm = ma_engine_get_resource_manager(_engine);
            foreach (var kv in _loaded)
                ma_resource_manager_unregister_data(rm, kv.Key);
            ma_engine_uninit(_engine);
        }
        NativeMemory.Free(_engine);

        foreach (var kv in _loaded)
            SharedBuffer.Dispose(kv.Value);
        _loaded.Clear();

        Screen.Shutdown();
        if (_vg != IntPtr.Zero) nvgDeleteSokol(_vg);
        SFilesystem.Shutdown();
        sg_shutdown();

        if (Debugger.IsAttached) Environment.Exit(0);
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    static void BuildUI()
    {
        var tabs = new TabView();
        _screen!.AddChild(tabs);

        tabs.AddTab("Sound FX", BuildSoundFxTab());
        tabs.AddTab("Mixing",   BuildMixingTab());
        tabs.AddTab("Fade",     BuildFadeTab());
        tabs.AddTab("Waveform", BuildWaveformTab());
        tabs.AddTab("Spatial",  BuildSpatializationTab());
        tabs.AddTab("EQ",       BuildEqTab());
        tabs.AddTab("Spectrum", BuildSpectrumTab());
        tabs.AddTab("Piano",     BuildPianoTab());
        tabs.AddTab("SoundFont", BuildSoundFontTab());
        tabs.AddTab("Engine",    BuildEngineTab());
    }

    // ── Tab: Sound FX ─────────────────────────────────────────────────────────
    static Widget BuildSoundFxTab()
    {
        var root = new Panel
        {
            Layout  = new BoxLayout(Orientation.Vertical, Alignment.Stretch, 12),
            Padding = new Thickness(20),
        };

        // Status row
        var infoRow = new Panel
        {
            Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Center, 16),
            FixedSize = new Vector2(0, 22),
        };
        _statusLabel      = new Label { Text = "Press a button to play audio", ForeColor = UIColor.FromHex("#AAAAAA") };
        _activeCountLabel = new Label { Text = "Active: 0",                    ForeColor = UIColor.FromHex("#66AAFF") };
        infoRow.AddChild(_statusLabel);
        infoRow.AddChild(_activeCountLabel);
        root.AddChild(infoRow);
        root.AddChild(new Separator());

        // ── Music ──────────────────────────────────────────────────────────────
        root.AddChild(new Label { Text = "Music", FontSize = 16 });

        var musicFiles  = _audioFiles.Where(af => af.IsMusic).ToArray();
        var musicCombo  = new ComboBox { FixedSize = new Vector2(180, 32) };
        musicCombo.SetItems(musicFiles.Select(af => af.Label).ToArray());
        musicCombo.SelectedIndex = 0;

        var musicRow = new Panel
        {
            Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Center, 12),
            FixedSize = new Vector2(0, 40),
        };
        _musicButton = new Button("▶  Play") { CornerRadius = 6 };
        _musicButton.Clicked += () => ToggleMusic(musicFiles[musicCombo.SelectedIndex].Path);
        musicRow.AddChild(_musicButton);
        musicRow.AddChild(musicCombo);
        musicRow.AddChild(new Label { Text = "(loops while playing)", ForeColor = UIColor.FromHex("#AAAAAA") });
        root.AddChild(musicRow);

        root.AddChild(new Separator());

        // ── Sound effects ──────────────────────────────────────────────────────
        root.AddChild(new Label { Text = "Sound Effects  —  click to play; click repeatedly for simultaneous instances", FontSize = 16 });

        var soundsGrid = new Panel
        {
            Layout  = new GridLayout(columns: 4, hSpacing: 8, vSpacing: 8),
            Padding = new Thickness(4),
        };

        foreach (var af in _audioFiles)
        {
            if (af.IsMusic) continue;
            var btn  = new Button(af.Label) { CornerRadius = 6 };
            var path = af.Path;
            btn.Clicked += () => PlaySound(path);
            soundsGrid.AddChild(btn);
        }
        root.AddChild(soundsGrid);

        return root;
    }

    // ── Tab: Mixing ───────────────────────────────────────────────────────────
    static Widget BuildMixingTab()
    {
        var root = new Panel
        {
            Layout  = new BoxLayout(Orientation.Vertical, Alignment.Start, 14),
            Padding = new Thickness(20),
        };

        root.AddChild(new Label { Text = "Live Sound Mixer", FontSize = 20 });
        root.AddChild(new Label { Text = "Pick a sound and adjust volume, pitch, and pan in real time while it loops.",
                                  ForeColor = UIColor.FromHex("#AAAAAA") });
        root.AddChild(new Separator());

        // Build parallel lists for the combo (paths + display labels)
        var sfxPaths  = new List<string>();
        var sfxLabels = new List<string>();
        foreach (var af in _audioFiles)
            if (!af.IsMusic) { sfxPaths.Add(af.Path); sfxLabels.Add(af.Label); }

        // ── Sound picker + Play/Stop ──────────────────────────────────────────
        var pickerRow = new Panel
        {
            Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Center, 12),
            FixedSize = new Vector2(0, 36),
        };
        var combo   = new ComboBox { FixedSize = new Vector2(200, 32) };
        combo.SetItems(sfxLabels.ToArray());
        combo.SelectedIndex = 0;
        var playBtn = new Button("▶  Play") { CornerRadius = 6, FixedSize = new Vector2(100, 32) };
        pickerRow.AddChild(new Label { Text = "Sound:", FixedSize = new Vector2(55, 32) });
        pickerRow.AddChild(combo);
        pickerRow.AddChild(playBtn);
        root.AddChild(pickerRow);

        root.AddChild(new Separator());

        // ── Sliders ───────────────────────────────────────────────────────────
        float mixVol = 1f, mixPitch = 1f, mixPan = 0f;

        Panel MakeSliderRow(string name, float min, float max, float initial, Label valueLbl, Action<float> onChange)
        {
            var row = new Panel
            {
                Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Center, 12),
                FixedSize = new Vector2(0, 32),
            };
            var sl = new Slider { Min = min, Max = max, Value = initial, FixedSize = new Vector2(280, 24) };
            sl.ValueChanged += onChange;
            row.AddChild(new Label { Text = name, FixedSize = new Vector2(60, 26) });
            row.AddChild(sl);
            row.AddChild(valueLbl);
            return row;
        }

        var volLbl   = new Label { Text = "1.00", FixedSize = new Vector2(45, 26) };
        var pitchLbl = new Label { Text = "1.00", FixedSize = new Vector2(45, 26) };
        var panLbl   = new Label { Text = "0.00", FixedSize = new Vector2(45, 26) };

        root.AddChild(MakeSliderRow("Volume", 0f, 2f, 1f, volLbl, v =>
        {
            mixVol = v; volLbl.Text = $"{v:F2}";
            if (_mixSound != null) ma_sound_set_volume(_mixSound, v);
        }));
        root.AddChild(MakeSliderRow("Pitch", 0.25f, 4f, 1f, pitchLbl, v =>
        {
            mixPitch = v; pitchLbl.Text = $"{v:F2}";
            if (_mixSound != null) ma_sound_set_pitch(_mixSound, v);
        }));
        root.AddChild(MakeSliderRow("Pan", -1f, 1f, 0f, panLbl, v =>
        {
            mixPan = v; panLbl.Text = $"{v:F2}";
            if (_mixSound != null) ma_sound_set_pan(_mixSound, v);
        }));

        // ── Play/Stop helpers ─────────────────────────────────────────────────
        void StartMixSound(string path)
        {
            if (!_engineReady || !_loaded.ContainsKey(path)) return;
            _mixSound = (ma_sound*)NativeMemory.AllocZeroed((nuint)sizeof(ma_sound));
            uint flags = (uint)(ma_sound_flags.MA_SOUND_FLAG_DECODE | ma_sound_flags.MA_SOUND_FLAG_NO_SPATIALIZATION);
            if (ma_sound_init_from_file(_engine, path, flags, null, null, _mixSound) != ma_result.MA_SUCCESS)
            {
                NativeMemory.Free(_mixSound); _mixSound = null; return;
            }
            ma_sound_set_volume(_mixSound, mixVol);
            ma_sound_set_pitch(_mixSound, mixPitch);
            ma_sound_set_pan(_mixSound, mixPan);
            ma_sound_set_looping(_mixSound, 1);
            ma_sound_start(_mixSound);
            playBtn.Text = "■  Stop";
        }

        void StopMixSound()
        {
            if (_mixSound == null) return;
            ma_sound_stop(_mixSound);
            ma_sound_uninit(_mixSound);
            NativeMemory.Free(_mixSound);
            _mixSound = null;
            playBtn.Text = "▶  Play";
        }

        playBtn.Clicked += () =>
        {
            if (_mixSound != null) { StopMixSound(); return; }
            int idx = combo.SelectedIndex < 0 ? 0 : combo.SelectedIndex;
            if (!_loaded.ContainsKey(sfxPaths[idx])) { SetStatus("Still loading — try again"); return; }
            StartMixSound(sfxPaths[idx]);
        };

        combo.SelectionChanged += (i, _) =>
        {
            if (_mixSound == null) return;
            StopMixSound();
            int idx = i < 0 ? 0 : i;
            if (_loaded.ContainsKey(sfxPaths[idx])) StartMixSound(sfxPaths[idx]);
        };

        return new ScrollView { Content = root, CanScrollVertical = true };
    }

    // ── Tab: Fade ─────────────────────────────────────────────────────────────
    static Widget BuildFadeTab()
    {
        var root = new Panel
        {
            Layout  = new BoxLayout(Orientation.Vertical, Alignment.Start, 14),
            Padding = new Thickness(20),
        };

        root.AddChild(new Label { Text = "Fade Demo", FontSize = 20 });
        root.AddChild(new Label { Text = "Smooth linear volume transitions via ma_sound_set_fade_in_milliseconds on the music track.",
                                  ForeColor = UIColor.FromHex("#AAAAAA") });
        root.AddChild(new Separator());

        // Music picker
        var fadeMusicFiles = _audioFiles.Where(af => af.IsMusic).ToArray();
        var fadeMusicCombo = new ComboBox { FixedSize = new Vector2(180, 32) };
        fadeMusicCombo.SetItems(fadeMusicFiles.Select(af => af.Label).ToArray());
        fadeMusicCombo.SelectedIndex = 0;
        var fadePickerRow = new Panel
        {
            Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Center, 12),
            FixedSize = new Vector2(0, 36),
        };
        fadePickerRow.AddChild(new Label { Text = "Track:", FixedSize = new Vector2(50, 32) });
        fadePickerRow.AddChild(fadeMusicCombo);
        root.AddChild(fadePickerRow);

        root.AddChild(new Separator());

        // Duration slider
        float fadeMs = 2000f;
        var durationLbl = new Label { Text = "2000 ms", FixedSize = new Vector2(80, 26) };
        var durRow = new Panel
        {
            Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Center, 12),
            FixedSize = new Vector2(0, 32),
        };
        var durSlider = new Slider { Min = 200f, Max = 8000f, Value = fadeMs, FixedSize = new Vector2(280, 24) };
        durSlider.ValueChanged += v => { fadeMs = v; durationLbl.Text = $"{(int)v} ms"; };
        durRow.AddChild(new Label { Text = "Duration:", FixedSize = new Vector2(70, 26) });
        durRow.AddChild(durSlider);
        durRow.AddChild(durationLbl);
        root.AddChild(durRow);

        root.AddChild(new Separator());

        // Buttons
        var btnRow = new Panel
        {
            Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Center, 12),
            FixedSize = new Vector2(0, 40),
        };
        var fadeInBtn  = new Button("▶  Fade In")  { CornerRadius = 6, FixedSize = new Vector2(130, 36) };
        var fadeOutBtn = new Button("▼  Fade Out") { CornerRadius = 6, FixedSize = new Vector2(130, 36) };
        var stopBtn    = new Button("■  Stop")     { CornerRadius = 6, FixedSize = new Vector2(100, 36) };
        var statusLbl  = new Label { Text = "Stopped", ForeColor = UIColor.FromHex("#AAAAAA") };

        fadeInBtn.Clicked += () =>
        {
            if (!_engineReady) return;
            if (_fadeSound != null)
            {
                // Already running: fade up from current volume
                float curVol = ma_sound_get_volume(in *_fadeSound);
                ma_sound_set_fade_in_milliseconds(_fadeSound, curVol, 1f, (ulong)fadeMs);
                statusLbl.Text = $"Fading in ({(int)fadeMs} ms)\u2026";
                return;
            }
            var fadePath = fadeMusicFiles[fadeMusicCombo.SelectedIndex].Path;
            if (!_loaded.ContainsKey(fadePath)) { statusLbl.Text = "Still loading — try again"; return; }
            _fadeSound = (ma_sound*)NativeMemory.AllocZeroed((nuint)sizeof(ma_sound));
            uint flags = (uint)(ma_sound_flags.MA_SOUND_FLAG_DECODE | ma_sound_flags.MA_SOUND_FLAG_NO_SPATIALIZATION);
            if (ma_sound_init_from_file(_engine, fadePath, flags, null, null, _fadeSound) != ma_result.MA_SUCCESS)
            {
                NativeMemory.Free(_fadeSound); _fadeSound = null;
                statusLbl.Text = "Init failed"; return;
            }
            ma_sound_set_looping(_fadeSound, 1);
            ma_sound_set_fade_in_milliseconds(_fadeSound, 0f, 1f, (ulong)fadeMs);
            ma_sound_start(_fadeSound);
            statusLbl.Text = $"Fading in ({(int)fadeMs} ms)\u2026";
        };

        fadeOutBtn.Clicked += () =>
        {
            if (_fadeSound == null) { statusLbl.Text = "Nothing playing"; return; }
            float curVol = ma_sound_get_volume(in *_fadeSound);
            ma_sound_set_fade_in_milliseconds(_fadeSound, curVol, 0f, (ulong)fadeMs);
            statusLbl.Text = $"Fading out ({(int)fadeMs} ms)\u2026";
        };

        stopBtn.Clicked += () =>
        {
            if (_fadeSound == null) return;
            ma_sound_stop(_fadeSound);
            ma_sound_uninit(_fadeSound);
            NativeMemory.Free(_fadeSound);
            _fadeSound = null;
            statusLbl.Text = "Stopped";
        };

        btnRow.AddChild(fadeInBtn);
        btnRow.AddChild(fadeOutBtn);
        btnRow.AddChild(stopBtn);
        root.AddChild(btnRow);
        root.AddChild(statusLbl);

        return new ScrollView { Content = root, CanScrollVertical = true };
    }

    // ── Tab: Piano ────────────────────────────────────────────────────────────
    static Widget BuildPianoTab()
    {
        _realPianoWidget = new PianoKeyboardWidget { Expand = true };
        _realPianoWidget.NoteOnSemi  += PianoNoteOn;
        _realPianoWidget.NoteOffSemi += PianoNoteOff;
        return _realPianoWidget;
    }

    static unsafe void PianoNoteOn(int semi)
    {
        if (semi < 0 || semi >= _pianoNoteFiles.Length) return;

        // Stop any previous voice for this note
        if (_pianoVoices[semi] != null)
        {
            ma_sound_stop(_pianoVoices[semi]);
            ma_sound_uninit(_pianoVoices[semi]);
            NativeMemory.Free(_pianoVoices[semi]);
            _pianoVoices[semi] = null;
        }

        string path = _pianoNoteFiles[semi];
        if (!_engineReady || !_loaded.ContainsKey(path)) return;

        var snd   = (ma_sound*)NativeMemory.AllocZeroed((nuint)sizeof(ma_sound));
        uint flags = (uint)(ma_sound_flags.MA_SOUND_FLAG_DECODE | ma_sound_flags.MA_SOUND_FLAG_NO_SPATIALIZATION);
        if (ma_sound_init_from_file(_engine, path, flags, null, null, snd) == ma_result.MA_SUCCESS)
        {
            ma_sound_start(snd);
            _pianoVoices[semi] = snd;
        }
        else
        {
            NativeMemory.Free(snd);
        }
    }

    static unsafe void PianoNoteOff(int semi)
    {
        if (semi < 0 || semi >= _pianoVoices.Length) return;
        if (_pianoVoices[semi] == null) return;

        // Fade out and schedule stop in 500 ms (simulates piano damper)
        ulong now = ma_engine_get_time_in_milliseconds(in *_engine);
        ma_sound_set_stop_time_with_fade_in_milliseconds(_pianoVoices[semi], now + 500, 500);

        // Transfer to _active for cleanup when the sound stops
        _active.Add(new ActiveSound { Sound = _pianoVoices[semi], CheckIsPlaying = true });
        _pianoVoices[semi] = null;
    }

    // ── Tab: SoundFont ────────────────────────────────────────────────────────
    static Widget BuildSoundFontTab()
    {
        var root = new Panel
        {
            Layout  = new BoxLayout(Orientation.Vertical, Alignment.Start, 14),
            Padding = new Thickness(20),
        };

        root.AddChild(new Label { Text = "SoundFont Synthesizer", FontSize = 20 });
        root.AddChild(new Label
        {
            Text      = "SF2 synthesis via TinySoundFont · MIDI playback (venture.mid) · Interactive piano",
            ForeColor = UIColor.FromHex("#AAAAAA"),
        });
        root.AddChild(new Separator());

        // ── MIDI Player ───────────────────────────────────────────────────────
        root.AddChild(new Label { Text = "MIDI Playback — venture.mid", FontSize = 16 });

        var midiRow = new Panel
        {
            Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Center, 12),
            FixedSize = new Vector2(0, 36),
        };

        _tsfPlayBtn    = new Button("▶  Play") { CornerRadius = 6, FixedSize = new Vector2(110, 32) };
        var stopBtn    = new Button("■  Stop")  { CornerRadius = 6, FixedSize = new Vector2(100, 32) };
        _tsfTimeLabel  = new Label { Text = "—",        ForeColor = UIColor.FromHex("#66AAFF"), FixedSize = new Vector2(110, 26) };
        _tsfVoiceLabel = new Label { Text = "Voices: 0", ForeColor = UIColor.FromHex("#AAAAAA") };

        _tsfPlayBtn.Clicked += () =>
        {
            if (_tsf == IntPtr.Zero || _tmlFirst == null) return;
            if (_tmlPlaying)
            {
                _tmlPlaying = false;
            }
            else
            {
                if (_tmlCurrent == null)
                {
                    _tmlCurrent = _tmlFirst;
                    _tmlMsec    = 0;
                }
                if (tsf_active_voice_count(_tsf) == 0)
                    StartTSFLoopingWave();
                _tmlPlaying = true;
            }
        };

        stopBtn.Clicked += () =>
        {
            _tmlPlaying = false;
            _tmlCurrent = null;
            _tmlMsec    = 0;
            if (_tsf != IntPtr.Zero) tsf_note_off_all(_tsf);
        };

        midiRow.AddChild(_tsfPlayBtn);
        midiRow.AddChild(stopBtn);
        midiRow.AddChild(_tsfTimeLabel);
        midiRow.AddChild(_tsfVoiceLabel);
        root.AddChild(midiRow);

        root.AddChild(new Separator());

        // ── Interactive Piano ─────────────────────────────────────────────────
        root.AddChild(new Label { Text = "Interactive Piano", FontSize = 16 });

        var presetRow = new Panel
        {
            Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Center, 12),
            FixedSize = new Vector2(0, 36),
        };

        _tsfPresetCombo = new ComboBox { FixedSize = new Vector2(300, 32) };
        _tsfPresetCombo.SetItems(new[] { "Loading SF2…" });
        _tsfPresetCombo.SelectedIndex = 0;

        presetRow.AddChild(new Label { Text = "Preset:", FixedSize = new Vector2(55, 32) });
        presetRow.AddChild(_tsfPresetCombo);
        root.AddChild(presetRow);

        _tsfPianoWidget = new PianoKeyboardWidget { Expand = true };
        _tsfPianoWidget.NoteOnSemi += semi =>
        {
            if (_tsf != IntPtr.Zero)
            {
                if (tsf_active_voice_count(_tsf) == 0)
                {
                    StartTSFLoopingWave();
                }
                tsf_note_on(_tsf, _tsfPresetCombo?.SelectedIndex ?? 0, 60 + semi, 1f);
            }
        };
        _tsfPianoWidget.NoteOffSemi += semi =>
        {
            if (_tsf != IntPtr.Zero)
            {
                tsf_note_off(_tsf, _tsfPresetCombo?.SelectedIndex ?? 0, 60 + semi);
            }
        };
        root.AddChild(_tsfPianoWidget);

        return new ScrollView { Content = root, CanScrollVertical = true };
    }

    // ── Tab: Engine ───────────────────────────────────────────────────────────
    static Widget BuildEngineTab()
    {
        var root = new Panel
        {
            Layout  = new BoxLayout(Orientation.Vertical, Alignment.Start, 14),
            Padding = new Thickness(20),
        };

        root.AddChild(new Label { Text = "Engine Controls", FontSize = 20 });
        root.AddChild(new Label { Text = "Global controls affecting all sounds routed through the ma_engine.",
                                  ForeColor = UIColor.FromHex("#AAAAAA") });
        root.AddChild(new Separator());

        Panel MakeSliderRow(string name, float nameW, Slider sl, Label valueLbl)
        {
            var row = new Panel
            {
                Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Center, 12),
                FixedSize = new Vector2(0, 32),
            };
            row.AddChild(new Label { Text = name, FixedSize = new Vector2(nameW, 26) });
            row.AddChild(sl);
            row.AddChild(valueLbl);
            return row;
        }

        // ── Master Volume (linear 0–2×) ──────────────────────────────────────
        float initVol      = _engineReady ? ma_engine_get_volume(_engine) : 1f;
        var masterVolLbl   = new Label { Text = FormatLinearVol(initVol), FixedSize = new Vector2(150, 26) };
        var masterSlider   = new Slider { Min = 0f, Max = 2f, Value = initVol, FixedSize = new Vector2(280, 24) };
        masterSlider.ValueChanged += v =>
        {
            if (_engineReady) ma_engine_set_volume(_engine, v);
            masterVolLbl.Text = FormatLinearVol(v);
        };
        root.AddChild(MakeSliderRow("Master Vol:", 90, masterSlider, masterVolLbl));

        // ── Gain dB (-60 to +6 dB) ──────────────────────────────────────────
        float initGain   = _engineReady ? ma_engine_get_gain_db(_engine) : 0f;
        var gainLbl      = new Label { Text = $"{initGain:+0.0;-0.0;0.0} dB", FixedSize = new Vector2(90, 26) };
        var gainSlider   = new Slider { Min = -60f, Max = 6f, Value = initGain, FixedSize = new Vector2(280, 24) };
        gainSlider.ValueChanged += v =>
        {
            if (_engineReady) ma_engine_set_gain_db(_engine, v);
            gainLbl.Text = $"{v:+0.0;-0.0;0.0} dB";
        };
        root.AddChild(MakeSliderRow("Gain (dB):", 90, gainSlider, gainLbl));

        root.AddChild(new Separator());

        // ── Live Stats ────────────────────────────────────────────────────────
        root.AddChild(new Label { Text = "Live Stats", FontSize = 16 });

        _engActiveLabel = new Label { Text = "Active sounds: 0",     ForeColor = UIColor.FromHex("#66AAFF") };
        _engUptimeLabel = new Label { Text = "Uptime: 0.0 s",        ForeColor = UIColor.FromHex("#66AAFF") };
        root.AddChild(_engActiveLabel);
        root.AddChild(_engUptimeLabel);

        if (_engineReady)
        {
            uint sr  = ma_engine_get_sample_rate(in *_engine);
            uint ch  = ma_engine_get_channels(in *_engine);
            uint lst = ma_engine_get_listener_count(in *_engine);
            root.AddChild(new Label { Text = $"Sample rate: {sr} Hz",       ForeColor = UIColor.FromHex("#AAAAAA") });
            root.AddChild(new Label { Text = $"Channels: {ch}",              ForeColor = UIColor.FromHex("#AAAAAA") });
            root.AddChild(new Label { Text = $"Listeners: {lst}",            ForeColor = UIColor.FromHex("#AAAAAA") });
        }

        root.AddChild(new Label
        {
            Text      = _engineReady ? "\u2713 Engine ready" : "\u2717 Engine init failed",
            ForeColor = _engineReady ? UIColor.FromHex("#66DD88") : UIColor.FromHex("#FF6666"),
        });

        return new ScrollView { Content = root, CanScrollVertical = true };

        static string FormatLinearVol(float v)
        {
            float dB = v > 0f ? 20f * MathF.Log10(v) : float.NegativeInfinity;
            return float.IsNegativeInfinity(dB)
                ? $"{v:F2}  (\u2212\u221e dB)"
                : $"{v:F2}  ({dB:+0.0;-0.0} dB)";
        }
    }

    // ── Tab: Waveform Generator ───────────────────────────────────────────────
    static Widget BuildWaveformTab()
    {
        var root = new Panel
        {
            Layout  = new BoxLayout(Orientation.Vertical, Alignment.Start, 14),
            Padding = new Thickness(20),
        };

        root.AddChild(new Label { Text = "Waveform Generator", FontSize = 20 });
        root.AddChild(new Label { Text = "Generates a pure tone via ma_waveform and plays it through the engine.",
                                  ForeColor = UIColor.FromHex("#AAAAAA") });
        root.AddChild(new Separator());

        // Waveform type combo
        var typeRow = new Panel
        {
            Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Center, 12),
            FixedSize = new Vector2(0, 36),
        };
        var typeCombo = new ComboBox { FixedSize = new Vector2(160, 32) };
        typeCombo.SetItems(new[] { "Sine", "Square", "Triangle", "Sawtooth" });
        typeCombo.SelectedIndex = 0;
        typeRow.AddChild(new Label { Text = "Type:", FixedSize = new Vector2(50, 32) });
        typeRow.AddChild(typeCombo);
        root.AddChild(typeRow);

        // Frequency slider
        float waveFreq = 440f;
        var freqLbl = new Label { Text = "440 Hz", FixedSize = new Vector2(70, 26) };
        var freqRow = new Panel
        {
            Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Center, 12),
            FixedSize = new Vector2(0, 32),
        };
        var freqSlider = new Slider { Min = 20f, Max = 2000f, Value = waveFreq, FixedSize = new Vector2(280, 24) };
        freqRow.AddChild(new Label { Text = "Frequency:", FixedSize = new Vector2(80, 26) });
        freqRow.AddChild(freqSlider);
        freqRow.AddChild(freqLbl);
        root.AddChild(freqRow);

        // Amplitude slider
        float waveAmp = 0.5f;
        var ampLbl = new Label { Text = "0.50", FixedSize = new Vector2(50, 26) };
        var ampRow = new Panel
        {
            Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Center, 12),
            FixedSize = new Vector2(0, 32),
        };
        var ampSlider = new Slider { Min = 0f, Max = 1f, Value = waveAmp, FixedSize = new Vector2(280, 24) };
        ampRow.AddChild(new Label { Text = "Amplitude:", FixedSize = new Vector2(80, 26) });
        ampRow.AddChild(ampSlider);
        ampRow.AddChild(ampLbl);
        root.AddChild(ampRow);

        root.AddChild(new Separator());

        // Play / Stop button
        var playBtn = new Button("▶  Play") { CornerRadius = 6, FixedSize = new Vector2(120, 36) };
        root.AddChild(playBtn);

        root.AddChild(new Separator());

        // Side-by-side visualizers: oscilloscope (expands to fill width) + VU meter (fixed 90px wide)
        var vizRow = new Panel
        {
            Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Stretch, 8),
            FixedSize = new Vector2(0, 150),
        };

        _oscWidget = new OscilloscopeWidget { Expand = true };
        vizRow.AddChild(_oscWidget);

        var waveDbCheck = new CheckBox { Label = "dB", FixedSize = new Vector2(90, 22), IsChecked = true };
        _vuWidget = new VuMeterWidget { Expand = true, DbScale = true };
        waveDbCheck.CheckedChanged += v => _vuWidget!.DbScale = v;
        var waveVuCol = new Panel
        {
            Layout    = new BoxLayout(Orientation.Vertical, Alignment.Start, 0),
            FixedSize = new Vector2(90, 0),
        };
        waveVuCol.AddChild(waveDbCheck);
        waveVuCol.AddChild(_vuWidget);
        vizRow.AddChild(waveVuCol);

        root.AddChild(vizRow);

        // Helpers
        ma_waveform_type CurrentWaveType() => typeCombo.SelectedIndex switch
        {
            1 => ma_waveform_type.ma_waveform_type_square,
            2 => ma_waveform_type.ma_waveform_type_triangle,
            3 => ma_waveform_type.ma_waveform_type_sawtooth,
            _ => ma_waveform_type.ma_waveform_type_sine,
        };

        void StartWaveform()
        {
            if (!_engineReady) return;
            uint sampleRate = ma_engine_get_sample_rate(in *_engine);
            var waveType    = CurrentWaveType();

            _waveformDS = (ma_waveform*)NativeMemory.AllocZeroed((nuint)sizeof(ma_waveform));
            var cfg = ma_waveform_config_init(ma_format.ma_format_f32, 2, sampleRate,
                                              waveType, waveAmp, waveFreq);
            if (ma_waveform_init(in cfg, _waveformDS) != ma_result.MA_SUCCESS)
            {
                NativeMemory.Free(_waveformDS); _waveformDS = null; return;
            }

            _waveformSound = (ma_sound*)NativeMemory.AllocZeroed((nuint)sizeof(ma_sound));
            uint flags = (uint)ma_sound_flags.MA_SOUND_FLAG_NO_SPATIALIZATION;
            if (ma_sound_init_from_data_source(_engine, (IntPtr)_waveformDS, flags, null, _waveformSound)
                != ma_result.MA_SUCCESS)
            {
                ma_waveform_uninit(_waveformDS);
                NativeMemory.Free(_waveformDS); _waveformDS = null;
                NativeMemory.Free(_waveformSound); _waveformSound = null;
                return;
            }
            ma_sound_set_looping(_waveformSound, 1);
            ma_sound_start(_waveformSound);
            playBtn.Text = "■  Stop";
            if (_oscWidget != null) _oscWidget.IsPlaying = true;

            // Init shadow waveform for VU meter PCM reads
            _vuWaveform = (ma_waveform*)NativeMemory.AllocZeroed((nuint)sizeof(ma_waveform));
            var vuCfg = ma_waveform_config_init(ma_format.ma_format_f32, 2, sampleRate,
                                                waveType, waveAmp, waveFreq);
            if (ma_waveform_init(in vuCfg, _vuWaveform) != ma_result.MA_SUCCESS)
            {
                NativeMemory.Free(_vuWaveform); _vuWaveform = null;
            }
        }

        void StopWaveform()
        {
            if (_waveformSound != null)
            {
                ma_sound_stop(_waveformSound);
                ma_sound_uninit(_waveformSound);
                NativeMemory.Free(_waveformSound);
                _waveformSound = null;
            }
            if (_waveformDS != null)
            {
                ma_waveform_uninit(_waveformDS);
                NativeMemory.Free(_waveformDS);
                _waveformDS = null;
            }
            if (_vuWaveform != null)
            {
                ma_waveform_uninit(_vuWaveform);
                NativeMemory.Free(_vuWaveform);
                _vuWaveform = null;
            }
            playBtn.Text = "▶  Play";
            if (_oscWidget != null) _oscWidget.IsPlaying = false;
            _vuWidget?.Reset();
        }

        playBtn.Clicked += () =>
        {
            if (_waveformSound != null) StopWaveform(); else StartWaveform();
        };

        // Slider / combo change handlers — update waveform in real time if playing
        freqSlider.ValueChanged += v =>
        {
            waveFreq = v;
            freqLbl.Text = $"{(int)v} Hz";
            if (_oscWidget != null) _oscWidget.Freq = v;
            if (_waveformDS != null) ma_waveform_set_frequency(_waveformDS, v);
            if (_vuWaveform != null) ma_waveform_set_frequency(_vuWaveform, v);
        };
        ampSlider.ValueChanged += v =>
        {
            waveAmp = v;
            ampLbl.Text = $"{v:F2}";
            if (_oscWidget != null) _oscWidget.Amp = v;
            if (_waveformDS != null) ma_waveform_set_amplitude(_waveformDS, v);
            if (_vuWaveform != null) ma_waveform_set_amplitude(_vuWaveform, v);
        };
        typeCombo.SelectionChanged += (_, _) =>
        {
            var wt = CurrentWaveType();
            if (_oscWidget != null) _oscWidget.WaveType = wt;
            if (_waveformDS != null) ma_waveform_set_type(_waveformDS, wt);
            if (_vuWaveform != null) ma_waveform_set_type(_vuWaveform, wt);
        };

        return new ScrollView { CanScrollHorizontal = false, CanScrollVertical = true, Expand = true, Content = root };
    }

    // ── Tab: Spatialization ───────────────────────────────────────────────────
    static Widget BuildSpatializationTab()
    {
        var root = new Panel
        {
            Layout  = new BoxLayout(Orientation.Vertical, Alignment.Start, 14),
            Padding = new Thickness(20),
        };

        root.AddChild(new Label { Text = "Spatialization", FontSize = 20 });
        root.AddChild(new Label { Text = "Play a looping sound, then drag the orange dot. Sound fades from full volume inside the green ring (Min Dist) to silent at the blue ring (Max Dist).",
                                  ForeColor = UIColor.FromHex("#AAAAAA") });
        root.AddChild(new Separator());

        // Sound picker (all files — SFX and music)
        var sfxPaths  = new List<string>();
        var sfxLabels = new List<string>();
        foreach (var af in _audioFiles)
            { sfxPaths.Add(af.Path); sfxLabels.Add(af.Label); }

        var pickerRow = new Panel
        {
            Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Center, 12),
            FixedSize = new Vector2(0, 36),
        };
        var combo   = new ComboBox { FixedSize = new Vector2(200, 32) };
        combo.SetItems(sfxLabels.ToArray());
        combo.SelectedIndex = 0;
        var playBtn = new Button("▶  Play") { CornerRadius = 6, FixedSize = new Vector2(100, 32) };
        pickerRow.AddChild(new Label { Text = "Sound:", FixedSize = new Vector2(55, 32) });
        pickerRow.AddChild(combo);
        pickerRow.AddChild(playBtn);
        root.AddChild(pickerRow);

        // Attenuation model combo
        var attenRow = new Panel
        {
            Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Center, 12),
            FixedSize = new Vector2(0, 36),
        };
        var attenCombo = new ComboBox { FixedSize = new Vector2(160, 32) };
        attenCombo.SetItems(new[] { "None", "Inverse", "Linear", "Exponential" });
        attenCombo.SelectedIndex = 2; // Linear default
        attenRow.AddChild(new Label { Text = "Attenuation:", FixedSize = new Vector2(95, 32) });
        attenRow.AddChild(attenCombo);
        root.AddChild(attenRow);

        // Min/Max distance sliders
        float spatMinDist = 1f, spatMaxDist = 10f;

        Panel MakeSliderRow(string name, float min, float max, float initial, Label valueLbl, Action<float> onChange)
        {
            var row = new Panel
            {
                Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Center, 12),
                FixedSize = new Vector2(0, 32),
            };
            var sl = new Slider { Min = min, Max = max, Value = initial, FixedSize = new Vector2(240, 24) };
            sl.ValueChanged += onChange;
            row.AddChild(new Label { Text = name, FixedSize = new Vector2(90, 26) });
            row.AddChild(sl);
            row.AddChild(valueLbl);
            return row;
        }

        var minDistLbl = new Label { Text = "1.0", FixedSize = new Vector2(50, 26) };
        var maxDistLbl = new Label { Text = "10.0", FixedSize = new Vector2(50, 26) };

        root.AddChild(MakeSliderRow("Min Dist:", 0.1f, 10f, spatMinDist, minDistLbl, v =>
        {
            spatMinDist = v; minDistLbl.Text = $"{v:F1}";
            if (_spatSound != null) ma_sound_set_min_distance(_spatSound, v);
            if (_spatCanvas != null) _spatCanvas.MinDist = v;
        }));
        root.AddChild(MakeSliderRow("Max Dist:", 1f, 30f, spatMaxDist, maxDistLbl, v =>
        {
            spatMaxDist = v; maxDistLbl.Text = $"{v:F1}";
            if (_spatSound != null) ma_sound_set_max_distance(_spatSound, v);
            if (_spatCanvas != null) _spatCanvas.MaxDist = v;
        }));

        root.AddChild(new Separator());

        // Canvas widget
        _spatCanvas = new SpatializationCanvasWidget
        {
            FixedSize = new Vector2(0, 200),
            MaxDist   = spatMaxDist,
            MinDist   = spatMinDist,
        };
        _spatCanvas.PositionChanged += (x, z) =>
        {
            if (_spatSound != null) ma_sound_set_position(_spatSound, x, 0f, z);
        };
        root.AddChild(_spatCanvas);

        // Play / Stop helpers
        ma_attenuation_model CurrentAttenModel() => attenCombo.SelectedIndex switch
        {
            1 => ma_attenuation_model.ma_attenuation_model_inverse,
            2 => ma_attenuation_model.ma_attenuation_model_linear,
            3 => ma_attenuation_model.ma_attenuation_model_exponential,
            _ => ma_attenuation_model.ma_attenuation_model_none,
        };

        void StartSpatSound(string path)
        {
            if (!_engineReady || !_loaded.ContainsKey(path)) return;
            _spatSound = (ma_sound*)NativeMemory.AllocZeroed((nuint)sizeof(ma_sound));
            // NO MA_SOUND_FLAG_NO_SPATIALIZATION — we want spatialization!
            uint flags = (uint)ma_sound_flags.MA_SOUND_FLAG_DECODE;
            if (ma_sound_init_from_file(_engine, path, flags, null, null, _spatSound)
                != ma_result.MA_SUCCESS)
            {
                NativeMemory.Free(_spatSound); _spatSound = null; return;
            }
            ma_sound_set_attenuation_model(_spatSound, CurrentAttenModel());
            ma_sound_set_min_distance(_spatSound, spatMinDist);
            ma_sound_set_max_distance(_spatSound, spatMaxDist);
            if (_spatCanvas != null)
                ma_sound_set_position(_spatSound, _spatCanvas.SourceX, 0f, _spatCanvas.SourceZ);

            // Set up listener
            ma_engine_listener_set_position(_engine, 0, 0f, 0f, 0f);
            ma_engine_listener_set_direction(_engine, 0, 0f, 0f, -1f);
            ma_engine_listener_set_world_up(_engine, 0, 0f, 1f, 0f);

            ma_sound_set_looping(_spatSound, 1);
            ma_sound_start(_spatSound);
            playBtn.Text = "■  Stop";
        }

        void StopSpatSound()
        {
            if (_spatSound == null) return;
            ma_sound_stop(_spatSound);
            ma_sound_uninit(_spatSound);
            NativeMemory.Free(_spatSound);
            _spatSound = null;
            playBtn.Text = "▶  Play";
        }

        playBtn.Clicked += () =>
        {
            if (_spatSound != null) { StopSpatSound(); return; }
            int idx = combo.SelectedIndex < 0 ? 0 : combo.SelectedIndex;
            StartSpatSound(sfxPaths[idx]);
        };

        combo.SelectionChanged += (i, _) =>
        {
            if (_spatSound == null) return;
            StopSpatSound();
            StartSpatSound(sfxPaths[i < 0 ? 0 : i]);
        };

        attenCombo.SelectionChanged += (_, _) =>
        {
            if (_spatSound != null) ma_sound_set_attenuation_model(_spatSound, CurrentAttenModel());
        };

        return new ScrollView { CanScrollHorizontal = false, CanScrollVertical = true, Expand = true, Content = root };
    }

    // ── EQ helpers ────────────────────────────────────────────────────────────

    static void EqInitNodes()
    {
        if (_eqNodesReady || !_engineReady) return;

        var ng       = ma_engine_get_node_graph(_engine);
        var endpoint = ma_engine_get_endpoint(_engine);
        uint ch      = ma_engine_get_channels(in *_engine);
        uint sr      = ma_engine_get_sample_rate(in *_engine);
        // Pass null for allocation callbacks so miniaudio uses its default allocators.
        // 'new ma_allocation_callbacks()' is a non-null pointer with null fn ptrs, which
        // causes miniaudio to use null function pointers for alloc → init failure.
        ref ma_allocation_callbacks nullAlloc = ref Unsafe.NullRef<ma_allocation_callbacks>();

        _eqLoShelf = (ma_loshelf_node*)NativeMemory.AllocZeroed((nuint)sizeof(ma_loshelf_node));
        _eqLowMid  = (ma_peak_node*)   NativeMemory.AllocZeroed((nuint)sizeof(ma_peak_node));
        _eqMid     = (ma_peak_node*)   NativeMemory.AllocZeroed((nuint)sizeof(ma_peak_node));
        _eqHiMid   = (ma_peak_node*)   NativeMemory.AllocZeroed((nuint)sizeof(ma_peak_node));
        _eqHiShelf = (ma_hishelf_node*)NativeMemory.AllocZeroed((nuint)sizeof(ma_hishelf_node));

        var loShelfCfg = ma_loshelf_node_config_init(ch, sr, 0.0, 1.0,    80.0);
        ma_loshelf_node_init(ng, in loShelfCfg, in nullAlloc, _eqLoShelf);

        var lowMidCfg = ma_peak_node_config_init(ch, sr, 0.0, 0.71,  300.0);
        ma_peak_node_init(ng, in lowMidCfg, in nullAlloc, _eqLowMid);

        var midCfg = ma_peak_node_config_init(ch, sr, 0.0, 0.71, 1000.0);
        ma_peak_node_init(ng, in midCfg, in nullAlloc, _eqMid);

        var hiMidCfg = ma_peak_node_config_init(ch, sr, 0.0, 0.71, 4000.0);
        ma_peak_node_init(ng, in hiMidCfg, in nullAlloc, _eqHiMid);

        var hiShelfCfg = ma_hishelf_node_config_init(ch, sr, 0.0, 1.0, 12000.0);
        ma_hishelf_node_init(ng, in hiShelfCfg, in nullAlloc, _eqHiShelf);

        // Wire chain: loShelf → lowMid → mid → hiMid → hiShelf → endpoint
        ma_node_attach_output_bus((IntPtr)_eqLoShelf, 0, (IntPtr)_eqLowMid,  0);
        ma_node_attach_output_bus((IntPtr)_eqLowMid,  0, (IntPtr)_eqMid,     0);
        ma_node_attach_output_bus((IntPtr)_eqMid,     0, (IntPtr)_eqHiMid,   0);
        ma_node_attach_output_bus((IntPtr)_eqHiMid,   0, (IntPtr)_eqHiShelf, 0);
        ma_node_attach_output_bus((IntPtr)_eqHiShelf, 0, endpoint,            0);

        _eqNodesReady = true;
    }

    static void EqApplyBand(int index, float gainDB)
    {
        if (!_eqNodesReady || !_engineReady) return;

        uint ch = ma_engine_get_channels(in *_engine);
        uint sr = ma_engine_get_sample_rate(in *_engine);

        switch (index)
        {
            case 0:
            {
                var cfg = ma_loshelf2_config_init(ma_format.ma_format_f32, ch, sr, gainDB, 1.0, 80.0);
                ma_loshelf_node_reinit(in cfg, _eqLoShelf);
                break;
            }
            case 1:
            {
                var cfg = ma_peak2_config_init(ma_format.ma_format_f32, ch, sr, gainDB, 0.71, 300.0);
                ma_peak_node_reinit(in cfg, _eqLowMid);
                break;
            }
            case 2:
            {
                var cfg = ma_peak2_config_init(ma_format.ma_format_f32, ch, sr, gainDB, 0.71, 1000.0);
                ma_peak_node_reinit(in cfg, _eqMid);
                break;
            }
            case 3:
            {
                var cfg = ma_peak2_config_init(ma_format.ma_format_f32, ch, sr, gainDB, 0.71, 4000.0);
                ma_peak_node_reinit(in cfg, _eqHiMid);
                break;
            }
            case 4:
            {
                var cfg = ma_hishelf2_config_init(ma_format.ma_format_f32, ch, sr, gainDB, 1.0, 12000.0);
                ma_hishelf_node_reinit(in cfg, _eqHiShelf);
                break;
            }
        }
    }

    // ── Tab: Equalizer ────────────────────────────────────────────────────────
    static Widget BuildEqTab()
    {
        // Outer: controls on the left, VU meter on the right
        var outer = new Panel
        {
            Layout = new BoxLayout(Orientation.Horizontal, Alignment.Stretch, 0),
            Expand = true,
        };

        var controls = new Panel
        {
            Layout  = new BoxLayout(Orientation.Vertical, Alignment.Start, 14),
            Padding = new Thickness(20),
        };

        var scrollView = new ScrollView
        {
            CanScrollHorizontal = false,
            CanScrollVertical   = true,
            Expand              = true,
            Content             = controls,
        };

        _eqVuWidget = new VuMeterWidget { Expand = true, DbScale = true };
        var eqDbCheck = new CheckBox { Label = "dB", FixedSize = new Vector2(90, 22), IsChecked = true };
        eqDbCheck.CheckedChanged += v => _eqVuWidget!.DbScale = v;
        var eqVuCol = new Panel
        {
            Layout    = new BoxLayout(Orientation.Vertical, Alignment.Start, 0),
            FixedSize = new Vector2(90, 0),
        };
        eqVuCol.AddChild(eqDbCheck);
        eqVuCol.AddChild(_eqVuWidget);
        outer.AddChild(scrollView);
        outer.AddChild(eqVuCol);

        controls.AddChild(new Label { Text = "5-Band Equalizer", FontSize = 20 });
        controls.AddChild(new Label
        {
            Text      = "Filter chain: Lo Shelf → Low-Mid → Mid → Hi-Mid → Hi Shelf → endpoint",
            ForeColor = UIColor.FromHex("#AAAAAA"),
        });
        controls.AddChild(new Separator());

        // Source picker + play button
        var fileNames = new string[_audioFiles.Length];
        for (int i = 0; i < _audioFiles.Length; i++) fileNames[i] = _audioFiles[i].Label;
        var fileCombo = new ComboBox { FixedSize = new Vector2(200, 32) };
        fileCombo.SetItems(fileNames);
        fileCombo.SelectedIndex = 0;

        var playBtn = new Button { Text = "▶  Play", FixedSize = new Vector2(100, 32) };

        var ctrlRow = new Panel
        {
            Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Center, 12),
            FixedSize = new Vector2(0, 36),
        };
        ctrlRow.AddChild(new Label { Text = "Source:", FixedSize = new Vector2(56, 32) });
        ctrlRow.AddChild(fileCombo);
        ctrlRow.AddChild(playBtn);
        controls.AddChild(ctrlRow);

        controls.AddChild(new Separator());

        // EQ bars visualizer
        _eqBarsWidget = new EqBarsWidget { FixedSize = new Vector2(0, 110) };
        controls.AddChild(_eqBarsWidget);

        controls.AddChild(new Separator());

        // Band definitions: (display name, frequency label)
        (string name, string freq)[] bands =
        {
            ("Lo Shelf",  " 80 Hz"),
            ("Low-Mid",   "300 Hz"),
            ("Mid",       "1 kHz"),
            ("Hi-Mid",    "4 kHz"),
            ("Hi Shelf",  "12 kHz"),
        };

        var eqSliders   = new Slider[5];
        var gainLabels  = new Label[5];

        Panel MakeBandRow(int idx)
        {
            var row = new Panel
            {
                Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Center, 10),
                FixedSize = new Vector2(0, 34),
            };
            row.AddChild(new Label { Text = bands[idx].name, FixedSize = new Vector2(72, 28) });
            row.AddChild(new Label { Text = bands[idx].freq, FixedSize = new Vector2(52, 28),
                                     ForeColor = UIColor.FromHex("#AAAAAA") });

            var sl = new Slider { Min = -15f, Max = 15f, Value = 0f };
            eqSliders[idx] = sl;
            gainLabels[idx] = new Label { Text = " 0.0 dB", FixedSize = new Vector2(72, 28) };

            sl.ValueChanged += v =>
            {
                gainLabels[idx].Text = $"{v:+0.0;-0.0; 0.0} dB";
                EqApplyBand(idx, v);
                _eqBarsWidget?.SetGain(idx, v);
            };

            row.AddChild(sl);
            row.AddChild(gainLabels[idx]);
            return row;
        }

        controls.AddChild(new Label { Text = "Gain (−15 to +15 dB)", FontSize = 14,
                                      ForeColor = UIColor.FromHex("#CCCCCC") });
        for (int i = 0; i < 5; i++)
            controls.AddChild(MakeBandRow(i));

        controls.AddChild(new Separator());

        // L/R Balance control
        controls.AddChild(new Label { Text = "L/R Balance", FontSize = 14,
                                      ForeColor = UIColor.FromHex("#CCCCCC") });

        var balLabel  = new Label { Text = "Center", FixedSize = new Vector2(72, 28) };
        var balSlider = new Slider { Min = -1f, Max = 1f, Value = 0f };
        balSlider.ValueChanged += v =>
        {
            if (_eqSound != null) ma_sound_set_pan(_eqSound, v);
            _eqPan = v;
            balLabel.Text = v < -0.05f ? $"L {(-v * 100f):F0}%"
                          : v >  0.05f ? $"R {( v * 100f):F0}%"
                          : "Center";
        };

        var balRow = new Panel
        {
            Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Center, 10),
            FixedSize = new Vector2(0, 34),
        };
        balRow.AddChild(new Label { Text = "◀ L", FixedSize = new Vector2(28, 28),
                                    ForeColor = UIColor.FromHex("#AAAAAA") });
        balRow.AddChild(balSlider);
        balRow.AddChild(new Label { Text = "R ▶", FixedSize = new Vector2(28, 28),
                                    ForeColor = UIColor.FromHex("#AAAAAA") });
        balRow.AddChild(balLabel);
        controls.AddChild(balRow);

        controls.AddChild(new Separator());

        var resetBtn = new Button { Text = "Reset EQ", FixedSize = new Vector2(100, 32) };
        resetBtn.Clicked += () =>
        {
            for (int i = 0; i < 5; i++)
                eqSliders[i].Value = 0f;   // fires ValueChanged which calls EqApplyBand
            balSlider.Value = 0f;          // also reset balance
            _eqPan = 0f;
            _eqBarsWidget?.Reset();
        };
        controls.AddChild(resetBtn);

        // Play/Stop logic
        playBtn.Clicked += () =>
        {
            if (_eqSound != null)
            {
                ma_sound_stop(_eqSound);
                ma_sound_uninit(_eqSound);
                NativeMemory.Free(_eqSound);
                _eqSound = null;
                if (_eqDecoder != null)
                {
                    ma_decoder_uninit(_eqDecoder);
                    NativeMemory.Free(_eqDecoder);
                    _eqDecoder = null;
                }
                _eqVuWidget?.Reset();
                playBtn.Text = "▶  Play";
                return;
            }

            if (!_engineReady) return;
            var af = _audioFiles[fileCombo.SelectedIndex];
            if (!_loaded.ContainsKey(af.Path)) return;

            EqInitNodes();
            if (!_eqNodesReady) return;

            _eqSound = (ma_sound*)NativeMemory.AllocZeroed((nuint)sizeof(ma_sound));
            uint flags = (uint)(ma_sound_flags.MA_SOUND_FLAG_DECODE |
                                ma_sound_flags.MA_SOUND_FLAG_NO_SPATIALIZATION);
            var result = ma_sound_init_from_file(_engine, af.Path, flags, null, null, _eqSound);
            if (result != ma_result.MA_SUCCESS)
            {
                NativeMemory.Free(_eqSound);
                _eqSound = null;
                return;
            }

            ma_sound_set_looping(_eqSound, 1);
            // Apply current balance before starting
            if (MathF.Abs(balSlider.Value) > 0.001f)
                ma_sound_set_pan(_eqSound, balSlider.Value);
            // Route through EQ chain (sound has no default attachment so this is the only path)
            ma_node_attach_output_bus((IntPtr)_eqSound, 0, (IntPtr)_eqLoShelf, 0);
            ma_sound_start(_eqSound);

            // Init monitoring decoder for VU meter (reads same file, not connected to audio graph)
            // Use ma_decoder_init_memory (not init_file) so it works on iOS/Android where
            // the C file system is not available — data is already pinned in _loaded.
            var decCfg = ma_decoder_config_init(ma_format.ma_format_f32, 2, 0);
            _eqDecoder = (ma_decoder*)NativeMemory.AllocZeroed((nuint)sizeof(ma_decoder));
            var loadedBuf = _loaded[af.Path];
            if (ma_decoder_init_memory(
                    (void*)loadedBuf.GetBufferPointer(), (nuint)loadedBuf.Size, in decCfg, _eqDecoder)
                != ma_result.MA_SUCCESS)
            {
                NativeMemory.Free(_eqDecoder);
                _eqDecoder = null;
            }

            playBtn.Text = "■  Stop";
        };

        return outer;
    }

    // ── Tab: Spectrum Analyzer ────────────────────────────────────────────────
    static Widget BuildSpectrumTab()
    {
        var outer = new Panel
        {
            Layout = new BoxLayout(Orientation.Vertical, Alignment.Stretch, 0),
            Expand = true,
        };

        // ── Header + controls row ─────────────────────────────────────────────
        var header = new Panel
        {
            Layout  = new BoxLayout(Orientation.Horizontal, Alignment.Center, 12),
            Padding = new Thickness(12, 8, 12, 8),
        };

        var fileNames = new string[_audioFiles.Length];
        for (int i = 0; i < _audioFiles.Length; i++) fileNames[i] = _audioFiles[i].Label;
        var fileCombo = new ComboBox { FixedSize = new Vector2(200, 32) };
        fileCombo.SetItems(fileNames);
        fileCombo.SelectedIndex = 0;

        var playBtn  = new Button("▶  Play") { CornerRadius = 6, FixedSize = new Vector2(100, 32) };
        var statusLbl = new Label { Text = "Stopped", ForeColor = UIColor.FromHex("#AAAAAA") };

        header.AddChild(new Label { Text = "Spectrum Analyzer", FontSize = 16 });
        header.AddChild(fileCombo);
        header.AddChild(playBtn);
        header.AddChild(statusLbl);
        outer.AddChild(header);
        outer.AddChild(new Separator());

        // ── Main visual area: spectrum + goniometer side by side ──────────────
        var vizRow = new Panel
        {
            Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Stretch, 4),
            Padding   = new Thickness(8, 4, 8, 4),
            FixedSize = new Vector2(0, 220),
        };

        _specWidget = new SpectrumWidget { Expand = true };
        vizRow.AddChild(_specWidget);

        _gonioWidget = new GoniometerWidget { FixedSize = new Vector2(200, 0) };
        vizRow.AddChild(_gonioWidget);

        outer.AddChild(vizRow);
        outer.AddChild(new Separator());

        // ── Spectrogram waterfall ─────────────────────────────────────────────
        _spectroWidget = new SpectrogramWidget { FixedSize = new Vector2(0, 160), Expand = true };
        var spectroRow = new Panel
        {
            Layout  = new BoxLayout(Orientation.Vertical, Alignment.Stretch, 0),
            Padding = new Thickness(8, 4, 8, 4),
        };
        spectroRow.AddChild(new Label { Text = "Spectrogram (scroll right = time →)", FontSize = 12,
                                         ForeColor = UIColor.FromHex("#667788") });
        spectroRow.AddChild(_spectroWidget);
        outer.AddChild(spectroRow);

        // ── Play / Stop logic ─────────────────────────────────────────────────
        playBtn.Clicked += () =>
        {
            if (_specSound != null)
            {
                ma_sound_stop(_specSound);
                ma_sound_uninit(_specSound);
                NativeMemory.Free(_specSound);
                _specSound = null;
                _specWidget?.Reset();
                _gonioWidget?.Reset();
                _spectroWidget?.Reset();
                playBtn.Text = "▶  Play";
                statusLbl.Text = "Stopped";
                return;
            }

            if (!_engineReady) return;
            var af = _audioFiles[fileCombo.SelectedIndex];
            if (!_loaded.ContainsKey(af.Path)) { statusLbl.Text = "Still loading…"; return; }

            _specSound = (ma_sound*)NativeMemory.AllocZeroed((nuint)sizeof(ma_sound));
            uint flags = (uint)(ma_sound_flags.MA_SOUND_FLAG_DECODE |
                                ma_sound_flags.MA_SOUND_FLAG_NO_SPATIALIZATION);
            if (ma_sound_init_from_file(_engine, af.Path, flags, null, null, _specSound)
                != ma_result.MA_SUCCESS)
            {
                NativeMemory.Free(_specSound); _specSound = null;
                statusLbl.Text = "Init failed"; return;
            }
            ma_sound_set_looping(_specSound, 1);
            ma_sound_start(_specSound);

            playBtn.Text   = "■  Stop";
            statusLbl.Text = $"▶ {af.Label}";
        };

        fileCombo.SelectionChanged += (_, _) =>
        {
            if (_specSound == null) return;
            // Stop and restart with new file — reuse the stop path then start path
            ma_sound_stop(_specSound);
            ma_sound_uninit(_specSound);
            NativeMemory.Free(_specSound); _specSound = null;
            _specWidget?.Reset(); _gonioWidget?.Reset(); _spectroWidget?.Reset();
            playBtn.Text = "▶  Play"; statusLbl.Text = "Stopped";
        };

        return outer;
    }

    public static SApp.sapp_desc sokol_main()
    {
        return new SApp.sapp_desc
        {
            init_cb      = &Init,
            frame_cb     = &Frame,
            event_cb     = &Event,
            cleanup_cb   = &Cleanup,
            width        = 960,
            height       = 540,
            sample_count = 4,
            window_title = "MiniAudio Demo",
            icon         = { sokol_default = true },
            logger       = { func = &slog_func },
        };
    }
}

// ── Oscilloscope Widget ────────────────────────────────────────────────────────

sealed class OscilloscopeWidget : Widget
{
    public ma_waveform_type WaveType = ma_waveform_type.ma_waveform_type_sine;
    public float            Freq     = 440f;
    public float            Amp      = 0.5f;
    public bool             IsPlaying;

    public override Vector2 PreferredSize(Renderer renderer) =>
        FixedSize ?? new Vector2(400, 160);

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        float w = Bounds.Width;
        float h = Bounds.Height;
        var   b = new Rect(0, 0, w, h);

        renderer.FillRoundedRect(b, 6f, UIColor.FromHex("#111122"));
        renderer.StrokeRoundedRect(b, 6f, 1.5f, UIColor.FromHex("#334466"));

        renderer.Save();
        renderer.ClipRect(b);

        float cy = h * 0.5f;

        // Center line
        renderer.SetStrokeColor(UIColor.FromHex("#223344"));
        renderer.SetStrokeWidth(1f);
        renderer.DrawLine(new Vector2(4f, cy), new Vector2(w - 4f, cy), 1f);

        if (!IsPlaying)
        {
            renderer.SetFillColor(UIColor.FromHex("#556677"));
            renderer.SetFont("sans");
            renderer.SetFontSize(14f);
            renderer.SetTextAlign(TextHAlign.Center);
            renderer.DrawText(w * 0.5f, cy, "Press Play to start");
            renderer.Restore();
            return;
        }

        int n    = (int)(w - 8f);
        if (n < 2) { renderer.Restore(); return; }

        // Fixed display window = 3 cycles at 440 Hz reference (~6.8ms).
        // As Freq changes the number of cycles drawn changes, making the
        // visual update in real time.
        float displayTime = 3f / 440f;
        float usableH = (h - 16f) * 0.5f;

        renderer.BeginPath();
        for (int i = 0; i < n; i++)
        {
            float t   = (float)i / n * displayTime;
            float raw = 2f * MathF.PI * Freq * t;

            float sample = WaveType switch
            {
                MiniAudioNS.MiniAudio.ma_waveform_type.ma_waveform_type_square =>
                    MathF.Sin(raw) >= 0f ? 1f : -1f,
                MiniAudioNS.MiniAudio.ma_waveform_type.ma_waveform_type_triangle =>
                    2f / MathF.PI * MathF.Asin(MathF.Sin(raw)),
                MiniAudioNS.MiniAudio.ma_waveform_type.ma_waveform_type_sawtooth =>
                    2f * (Freq * t - MathF.Floor(Freq * t + 0.5f)),
                _ => // sine
                    MathF.Sin(raw),
            };
            float px = 4f + i;
            float py = cy - sample * Amp * usableH;
            if (i == 0) renderer.MoveTo(px, py); else renderer.LineTo(px, py);
        }
        renderer.SetStrokeColor(new UIColor(0.2f, 0.85f, 1f, 1f));
        renderer.SetStrokeWidth(2f);
        renderer.Stroke();

        renderer.Restore();
    }
}

// ── VU Meter Widget ────────────────────────────────────────────────────────────

sealed class VuMeterWidget : Widget
{
    float _smoothRmsL, _smoothRmsR;
    float _holdL, _holdR;
    float _holdTimerL, _holdTimerR;

    const float HoldTime  = 1.5f;   // seconds before peak hold starts falling
    const float HoldDecay = 1.2f;   // fall speed in linear units per second

    public bool DbScale { get; set; } = false;

    public void UpdateLevels(float peakL, float rmsL, float peakR, float rmsR, float dt)
    {
        // Fast attack, exponential decay (~300ms time constant)
        float decay = MathF.Pow(0.01f, dt);
        _smoothRmsL = MathF.Max(rmsL, _smoothRmsL * decay);
        _smoothRmsR = MathF.Max(rmsR, _smoothRmsR * decay);

        // Peak hold
        if (peakL >= _holdL) { _holdL = peakL; _holdTimerL = HoldTime; }
        else { _holdTimerL -= dt; if (_holdTimerL < 0f) _holdL = MathF.Max(0f, _holdL - HoldDecay * dt); }

        if (peakR >= _holdR) { _holdR = peakR; _holdTimerR = HoldTime; }
        else { _holdTimerR -= dt; if (_holdTimerR < 0f) _holdR = MathF.Max(0f, _holdR - HoldDecay * dt); }
    }

    public void Reset()
    {
        _smoothRmsL = _smoothRmsR = 0f;
        _holdL = _holdR = 0f;
        _holdTimerL = _holdTimerR = 0f;
    }

    public override Vector2 PreferredSize(Renderer renderer) =>
        FixedSize ?? new Vector2(90, 160);

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        float w = Bounds.Width;
        float h = Bounds.Height;
        var   b = new Rect(0, 0, w, h);

        renderer.FillRoundedRect(b, 6f, UIColor.FromHex("#111122"));
        renderer.StrokeRoundedRect(b, 6f, 1.5f, UIColor.FromHex("#334466"));

        renderer.Save();
        renderer.ClipRect(b);

        const float LabelH = 18f;
        const float Pad    = 6f;
        const float Gap    = 4f;

        float barAreaW = w - Pad * 2f - Gap;
        float barW     = barAreaW * 0.5f;
        float barH     = h - Pad - LabelH;

        if (barW < 2f || barH < 10f) { renderer.Restore(); return; }

        float lx = Pad;
        float rx = Pad + barW + Gap;
        float ty = Pad;

        DrawBar(renderer, lx, ty, barW, barH, _smoothRmsL, _holdL, DbScale);
        DrawBar(renderer, rx, ty, barW, barH, _smoothRmsR, _holdR, DbScale);

        // Channel labels
        renderer.SetFillColor(UIColor.FromHex("#889AAA"));
        renderer.SetFont("sans");
        renderer.SetFontSize(11f);
        renderer.SetTextAlign(NVGalign.NVG_ALIGN_CENTER | NVGalign.NVG_ALIGN_BOTTOM);
        renderer.DrawText(lx + barW * 0.5f, h - 2f, "L");
        renderer.DrawText(rx + barW * 0.5f, h - 2f, "R");

        renderer.Restore();
    }

    static void DrawBar(Renderer renderer, float x, float y, float bw, float bh,
                        float rms, float hold, bool dbScale)
    {
        // Background
        renderer.FillRect(new Rect(x, y, bw, bh), UIColor.FromHex("#1A1A2E"));

        // Map a linear amplitude (0–1) to a bar fill fraction (0–1)
        static float ToFrac(float v, bool db) => db
            ? MathF.Max(0f, (MathF.Max(v > 0f ? 20f * MathF.Log10(v) : -60f, -60f) + 60f) / 60f)
            : MathF.Min(v, 1f);

        // dB tick marks at 0, -6, -12, -18, -24 dBFS
        ReadOnlySpan<float> dbTicks = stackalloc float[] { 0f, -6f, -12f, -18f, -24f };
        foreach (float db in dbTicks)
        {
            float frac = dbScale
                ? (db + 60f) / 60f
                : db >= 0f ? 1f : MathF.Pow(10f, db / 20f);
            float ty = y + bh - frac * bh;
            if (ty >= y && ty <= y + bh)
                renderer.FillRect(new Rect(x, ty, bw, 1f), UIColor.FromHex("#2A3A4A"));
        }

        // RMS fill bar with green→yellow→red gradient (bottom to top)
        float fillH = ToFrac(rms, dbScale) * bh;
        if (fillH >= 1f)
        {
            float barY = y + bh - fillH;
            // Gradient bottom (green) → top (red)
            var paint = renderer.LinearGradient(x, y + bh, x, y,
                new UIColor(0.15f, 0.85f, 0.20f, 1f),
                new UIColor(0.95f, 0.20f, 0.15f, 1f));
            renderer.FillRectWithPaint(new Rect(x, barY, bw, fillH), paint);
        }

        // Peak hold line (white)
        if (hold > 0.005f)
        {
            float holdY = y + bh - ToFrac(hold, dbScale) * bh;
            renderer.SetStrokeColor(UIColor.FromHex("#FFFFFFCC"));
            renderer.SetStrokeWidth(2f);
            renderer.DrawLine(new Vector2(x, holdY), new Vector2(x + bw, holdY), 2f);
        }
    }
}

// ── EQ Bars Widget ────────────────────────────────────────────────────────────

sealed class EqBarsWidget : Widget
{
    static readonly string[] FreqLabels = { "80", "300", "1k", "4k", "12k" };
    const float MaxDB = 15f;

    readonly float[] _gains = new float[5];

    public void SetGain(int band, float db) { if ((uint)band < 5) _gains[band] = db; }
    public void Reset() { for (int i = 0; i < 5; i++) _gains[i] = 0f; }

    public override Vector2 PreferredSize(Renderer renderer) =>
        FixedSize ?? new Vector2(300, 110);

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        float w = Bounds.Width;
        float h = Bounds.Height;
        var   b = new Rect(0, 0, w, h);

        renderer.FillRoundedRect(b, 6f, UIColor.FromHex("#0D0D1A"));
        renderer.StrokeRoundedRect(b, 6f, 1f, UIColor.FromHex("#334466"));

        renderer.Save();
        renderer.ClipRect(b);

        const float Pad    = 8f;
        const float LabelH = 14f;
        const float Gap    = 4f;

        float barAreaH = h - Pad * 2f - LabelH;
        float barAreaW = w - Pad * 2f;
        const int N    = 5;
        float barW     = (barAreaW - Gap * (N - 1)) / N;

        if (barW < 2f || barAreaH < 10f) { renderer.Restore(); return; }

        float centerY = Pad + barAreaH * 0.5f;

        // Grid lines
        renderer.SetStrokeWidth(1f);
        renderer.SetStrokeColor(UIColor.FromHex("#1C2A3A"));
        renderer.DrawLine(new Vector2(Pad, Pad), new Vector2(w - Pad, Pad), 1f);
        renderer.DrawLine(new Vector2(Pad, Pad + barAreaH), new Vector2(w - Pad, Pad + barAreaH), 1f);
        // 0dB center line (brighter)
        renderer.SetStrokeColor(UIColor.FromHex("#2A4A6A"));
        renderer.DrawLine(new Vector2(Pad, centerY), new Vector2(w - Pad, centerY), 1f);

        for (int i = 0; i < N; i++)
        {
            float gain = MathF.Max(-MaxDB, MathF.Min(MaxDB, _gains[i]));
            float norm  = gain / MaxDB; // -1..1
            float bx    = Pad + i * (barW + Gap);

            if (MathF.Abs(norm) < 0.015f)
            {
                // Flat — thin tick at center
                renderer.FillRect(new Rect(bx, centerY - 1f, barW, 2f), UIColor.FromHex("#2A4A6A"));
            }
            else if (norm > 0f)
            {
                // Boost: grow upward from center line
                float fillH = norm * barAreaH * 0.5f;
                float barY  = centerY - fillH;
                var paint = renderer.LinearGradient(bx, centerY, bx, Pad,
                    new UIColor(0.05f, 0.55f, 1.0f, 0.85f),
                    new UIColor(0.30f, 0.85f, 1.0f, 1.0f));
                renderer.FillRoundedRectWithPaint(new Rect(bx, barY, barW, fillH), 3f, paint);
            }
            else
            {
                // Cut: grow downward from center line
                float fillH = -norm * barAreaH * 0.5f;
                float barY  = centerY;
                var paint = renderer.LinearGradient(bx, centerY, bx, Pad + barAreaH,
                    new UIColor(1.0f, 0.55f, 0.05f, 0.85f),
                    new UIColor(1.0f, 0.15f, 0.0f,  1.0f));
                renderer.FillRoundedRectWithPaint(new Rect(bx, barY, barW, fillH), 3f, paint);
            }

            // Frequency label
            renderer.SetFillColor(UIColor.FromHex("#6688AA"));
            renderer.SetFont("sans");
            renderer.SetFontSize(10f);
            renderer.SetTextAlign(NVGalign.NVG_ALIGN_CENTER | NVGalign.NVG_ALIGN_BOTTOM);
            renderer.DrawText(bx + barW * 0.5f, h - 1f, FreqLabels[i]);
        }

        renderer.Restore();
    }
}

// ── Spatialization Canvas Widget ───────────────────────────────────────────────

sealed class SpatializationCanvasWidget : Widget
{
    public float SourceX  { get; set; }
    public float SourceZ  { get; set; }
    public float MaxDist  { get; set; } = 10f;
    public float MinDist  { get; set; } = 1f;

    public event Action<float, float>? PositionChanged;

    // The canvas always represents this world diameter, matching the MaxDist slider's maximum.
    // Both rings scale with their slider values — bigger value → bigger ring.
    private const float ViewRadius = 30f;

    private bool _dragging;

    public override Vector2 PreferredSize(Renderer renderer) =>
        FixedSize ?? new Vector2(400, 260);

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        float w  = Bounds.Width;
        float h  = Bounds.Height;
        var   b  = new Rect(0, 0, w, h);
        float cx = w * 0.5f;
        float cy = h * 0.5f;

        float pixelRadius = MathF.Min(w, h) * 0.5f - 12f;
        float scale       = pixelRadius / ViewRadius; // fixed — does not change with MaxDist

        renderer.FillRoundedRect(b, 6f, UIColor.FromHex("#111122"));
        renderer.StrokeRoundedRect(b, 6f, 1.5f, UIColor.FromHex("#334466"));

        renderer.Save();
        renderer.ClipRect(b);

        // Cross-hair axes
        renderer.DrawCrossHair(cx, cy, pixelRadius, 1f, new UIColor(0.2f, 0.3f, 0.4f, 0.3f));

        // Max distance ring (blue) — grows when MaxDist slider increases
        float maxPR = MathF.Min(MaxDist * scale, pixelRadius);
        renderer.StrokeCircle(cx, cy, maxPR, 2f, new UIColor(0.3f, 0.5f, 1f, 0.8f));

        // Min distance ring (green) — grows when MinDist slider increases
        float minPR = MathF.Min(MinDist * scale, pixelRadius);
        renderer.StrokeCircle(cx, cy, minPR, 1.5f, new UIColor(0.3f, 0.8f, 0.3f, 0.7f));

        // Listener (cyan filled circle)
        renderer.FillCircle(cx, cy, 8f, new UIColor(0.2f, 0.9f, 1f, 1f));

        // Sound source (orange filled circle)
        float sx = cx + SourceX * scale;
        float sz = cy + SourceZ * scale;
        renderer.FillCircle(sx, sz, 10f, new UIColor(1f, 0.55f, 0.1f, 1f));
        renderer.StrokeCircle(sx, sz, 10f, 2f, new UIColor(1f, 0.8f, 0.5f, 1f));

        // Distance line from listener to source
        renderer.DrawLine(cx, cy, sx, sz, 1f, new UIColor(0.7f, 0.5f, 0.2f, 0.5f));

        renderer.Restore();

        // Distance label
        float dist3d = MathF.Sqrt(SourceX * SourceX + SourceZ * SourceZ);
        renderer.SetFillColor(UIColor.FromHex("#AABBCC"));
        renderer.SetFont("sans");
        renderer.SetFontSize(12f);
        renderer.SetTextAlign(TextHAlign.Center);
        renderer.DrawText(w * 0.5f, h - 8f, $"Dist: {dist3d:F1}  |  Green = Min Dist (full vol)  |  Blue = Max Dist (silent)");
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button == MouseButton.Left)
        {
            _dragging = true;
            UpdateFromMouse(e.LocalPosition);
            return true;
        }
        return false;
    }

    public override bool OnMouseMove(MouseEvent e)
    {
        if (_dragging) { UpdateFromMouse(e.LocalPosition); return true; }
        return false;
    }

    public override bool OnMouseUp(MouseEvent e)
    {
        if (e.Button == MouseButton.Left) { _dragging = false; return true; }
        return false;
    }

    private void UpdateFromMouse(Vector2 localPos)
    {
        float cx          = Bounds.Width  * 0.5f;
        float cy          = Bounds.Height * 0.5f;
        float pixelRadius = MathF.Min(Bounds.Width, Bounds.Height) * 0.5f - 12f;
        float scale       = pixelRadius / ViewRadius;
        if (scale <= 0f) return;

        float x = (localPos.X - cx) / scale;
        float z = (localPos.Y - cy) / scale;

        // Clamp to MaxDist circle
        float len = MathF.Sqrt(x * x + z * z);
        if (len > MaxDist) { x = x / len * MaxDist; z = z / len * MaxDist; }

        SourceX = x;
        SourceZ = z;
        PositionChanged?.Invoke(x, z);
    }
}

// ── Spectrum Widget ────────────────────────────────────────────────────────────
// Real-time frequency bar display — runs a DFT on incoming PCM samples.

sealed class SpectrumWidget : Widget
{
    // 1024-bin magnitude buffer (smoothed)
    const int FftSize  = 2048;
    const int BinCount = FftSize / 2;

    readonly float[] _magSmooth = new float[BinCount];
    readonly float[] _fftReal   = new float[FftSize];
    readonly float[] _fftImag   = new float[FftSize];
    readonly float[] _window    = new float[FftSize]; // Hann window

    private bool _hasData;

    public SpectrumWidget()
    {
        // Pre-compute Hann window
        for (int i = 0; i < FftSize; i++)
            _window[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (FftSize - 1)));
    }

    public void PushSamples(ReadOnlySpan<float> mono)
    {
        int n = Math.Min(mono.Length, FftSize);
        // Fill window — zero-pad if fewer samples than FftSize
        for (int i = 0; i < FftSize; i++)
        {
            float s = i < n ? mono[i] : 0f;
            _fftReal[i] = s * _window[i];
            _fftImag[i] = 0f;
        }
        DftMagnitudes(_fftReal, _fftImag, _magSmooth, smoothFactor: 0.7f);
        _hasData = true;
    }

    public void Reset()
    {
        Array.Clear(_magSmooth, 0, BinCount);
        _hasData = false;
    }

    // Simple Cooley–Tukey in-place FFT (power-of-2)
    internal static void Fft(float[] re, float[] im)
    {
        int n = re.Length;
        // Bit-reversal permutation
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            float ang = -2f * MathF.PI / len;
            float wRe = MathF.Cos(ang), wIm = MathF.Sin(ang);
            for (int i = 0; i < n; i += len)
            {
                float curRe = 1f, curIm = 0f;
                for (int j = 0; j < len / 2; j++)
                {
                    float uRe = re[i + j],               uIm = im[i + j];
                    float vRe = re[i + j + len / 2],     vIm = im[i + j + len / 2];
                    float tvRe = vRe * curRe - vIm * curIm;
                    float tvIm = vRe * curIm + vIm * curRe;
                    re[i + j]           = uRe + tvRe;  im[i + j]           = uIm + tvIm;
                    re[i + j + len / 2] = uRe - tvRe;  im[i + j + len / 2] = uIm - tvIm;
                    float newCurRe = curRe * wRe - curIm * wIm;
                    curIm = curRe * wIm + curIm * wRe;
                    curRe = newCurRe;
                }
            }
        }
    }

    // Run FFT and blend magnitudes into dest with exponential smoothing
    static void DftMagnitudes(float[] re, float[] im, float[] dest, float smoothFactor)
    {
        Fft(re, im);
        int half = re.Length / 2;
        float scale = 2f / re.Length;
        for (int i = 0; i < half; i++)
        {
            float mag = MathF.Sqrt(re[i] * re[i] + im[i] * im[i]) * scale;
            dest[i] = dest[i] * smoothFactor + mag * (1f - smoothFactor);
        }
    }

    public override Vector2 PreferredSize(Renderer renderer) => FixedSize ?? new Vector2(500, 180);

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;
        float w = Bounds.Width, h = Bounds.Height;
        var b = new Rect(0, 0, w, h);
        renderer.FillRoundedRect(b, 6f, UIColor.FromHex("#090912"));
        renderer.StrokeRoundedRect(b, 6f, 1f, UIColor.FromHex("#334466"));

        renderer.Save();
        renderer.ClipRect(b);

        const float Pad     = 8f;
        const float LabelH  = 14f;
        float barAreaH = h - Pad * 2f - LabelH;
        float barAreaW = w - Pad * 2f;

        if (!_hasData || barAreaH < 10f) { renderer.Restore(); return; }

        // Frequency axis: map bar index → log frequency
        // Display range 20 Hz – 20 kHz (assume 44100 Hz sample rate → BinCount = 1024 bins)
        const float SampleRate = 44100f;
        const float FreqMin    = 20f;
        const float FreqMax    = 20000f;
        float logMin = MathF.Log(FreqMin);
        float logMax = MathF.Log(FreqMax);

        const int   BarCount = 128;
        float barW   = barAreaW / BarCount - 1f;
        if (barW < 1f) barW = 1f;
        float step   = barAreaW / BarCount;

        for (int b2 = 0; b2 < BarCount; b2++)
        {
            // Map bar → frequency → bin
            float t     = (float)b2 / BarCount;
            float freq  = MathF.Exp(logMin + t * (logMax - logMin));
            int   bin   = (int)(freq / SampleRate * FftSize);
            bin = Math.Clamp(bin, 0, BinCount - 1);

            float mag = _magSmooth[bin];
            // Convert to dBFS (floor at -80 dB)
            float dB  = mag > 0f ? 20f * MathF.Log10(mag) : -80f;
            float frac = MathF.Max(0f, (dB + 80f) / 80f);

            float barX = Pad + b2 * step;
            float barH2 = frac * barAreaH;
            float barY  = Pad + barAreaH - barH2;

            // Color by frequency band
            UIColor col;
            if (t < 0.25f)      col = new UIColor(1.0f, 0.45f + t, 0.1f, 0.9f); // bass: warm orange
            else if (t < 0.65f) col = new UIColor(0.15f, 0.85f, 0.25f, 0.9f);   // mid: green
            else                col = new UIColor(0.2f, 0.85f, 1.0f, 0.9f);      // treble: cyan

            if (barH2 >= 1f)
                renderer.FillRect(new Rect(barX, barY, barW, barH2), col);
        }

        // dB guide lines
        foreach (float db in new[] { -20f, -40f, -60f })
        {
            float frac2 = (db + 80f) / 80f;
            float lineY = Pad + barAreaH * (1f - frac2);
            renderer.DrawLine(Pad, lineY, w - Pad, lineY, 1f, new UIColor(0.25f, 0.35f, 0.45f, 0.5f));
            renderer.SetFillColor(UIColor.FromHex("#556677"));
            renderer.SetFont("sans"); renderer.SetFontSize(10f);
            renderer.SetTextAlign(NVGalign.NVG_ALIGN_LEFT | NVGalign.NVG_ALIGN_BOTTOM);
            renderer.DrawText(Pad + 2f, lineY - 1f, $"{(int)db} dB");
        }

        // Freq axis labels
        renderer.SetFillColor(UIColor.FromHex("#556677"));
        renderer.SetFont("sans"); renderer.SetFontSize(10f);
        renderer.SetTextAlign(NVGalign.NVG_ALIGN_CENTER | NVGalign.NVG_ALIGN_BOTTOM);
        foreach ((float f, string lbl) in new[] { (100f, "100"), (500f, "500"), (1000f, "1k"), (5000f, "5k"), (10000f, "10k"), (18000f, "18k") })
        {
            float t2 = (MathF.Log(f) - logMin) / (logMax - logMin);
            renderer.DrawText(Pad + t2 * barAreaW, h - 1f, lbl);
        }

        renderer.Restore();
    }
}

// ── Goniometer Widget ──────────────────────────────────────────────────────────
// Lissajous (L vs R) plot showing stereo width and correlation.

sealed class GoniometerWidget : Widget
{
    const int TrailLen = 1024;
    readonly float[] _mid  = new float[TrailLen]; // L+R (x axis, 45° rotated)
    readonly float[] _side = new float[TrailLen]; // L-R (y axis, 45° rotated)
    int   _head;
    int   _count;

    public void PushSamples(ReadOnlySpan<float> L, ReadOnlySpan<float> R)
    {
        int n = Math.Min(L.Length, R.Length);
        for (int i = 0; i < n; i++)
        {
            _mid[_head]  = (L[i] + R[i]) * 0.707f;  // M = (L+R)/√2
            _side[_head] = (L[i] - R[i]) * 0.707f;  // S = (L-R)/√2
            _head = (_head + 1) % TrailLen;
            if (_count < TrailLen) _count++;
        }
    }

    public void Reset() { _head = 0; _count = 0; }

    public override Vector2 PreferredSize(Renderer renderer) => FixedSize ?? new Vector2(180, 180);

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;
        float w = Bounds.Width, h = Bounds.Height;
        renderer.FillRoundedRect(new Rect(0, 0, w, h), 6f, UIColor.FromHex("#090912"));
        renderer.StrokeRoundedRect(new Rect(0, 0, w, h), 6f, 1f, UIColor.FromHex("#334466"));

        renderer.Save();
        renderer.ClipRect(new Rect(0, 0, w, h));

        float cx = w * 0.5f, cy = h * 0.5f;
        float scale = MathF.Min(w, h) * 0.45f;

        // Axis lines
        renderer.DrawCrossHair(cx, cy, scale, 1f, new UIColor(0.2f, 0.3f, 0.4f, 0.5f));

        // L/R axis labels
        renderer.SetFillColor(UIColor.FromHex("#445566"));
        renderer.SetFont("sans"); renderer.SetFontSize(10f);
        renderer.SetTextAlign(NVGalign.NVG_ALIGN_CENTER | NVGalign.NVG_ALIGN_MIDDLE);
        renderer.DrawText(cx - scale + 8f, cy - scale * 0.75f, "L");
        renderer.DrawText(cx + scale - 8f, cy - scale * 0.75f, "R");
        renderer.DrawText(cx, cy - scale + 6f, "M");

        if (_count < 2) { renderer.Restore(); return; }

        // Draw trail in alpha bands to keep vertex count low.
        // 1024 individual circles each tesselate into ~48 vertices → overflows sokol_nvg's
        // fixed vertex buffer. Batching into 8 groups: 8 nvgFill calls total instead of 1024.
        const int Bands = 8;
        const float DotHalf = 1.5f;
        for (int band = 0; band < Bands; band++)
        {
            int start = _count * band / Bands;
            int end   = _count * (band + 1) / Bands;
            float alpha = (band + 0.5f) / Bands * 0.85f;
            renderer.BeginPath();
            for (int i = start; i < end; i++)
            {
                int idx = (_head - _count + i + TrailLen) % TrailLen;
                float px = cx + _mid[idx]  * scale;
                float py = cy - _side[idx] * scale;
                renderer.AddRect(px - DotHalf, py - DotHalf, DotHalf * 2f, DotHalf * 2f);
            }
            renderer.SetFillColor(new UIColor(0.1f, 0.85f, 1f, alpha));
            renderer.Fill();
        }

        // Label
        renderer.SetFillColor(UIColor.FromHex("#445566"));
        renderer.SetFont("sans"); renderer.SetFontSize(10f);
        renderer.SetTextAlign(NVGalign.NVG_ALIGN_CENTER | NVGalign.NVG_ALIGN_BOTTOM);
        renderer.DrawText(cx, h - 1f, "Goniometer");

        renderer.Restore();
    }
}

// ── Spectrogram Widget ─────────────────────────────────────────────────────────
// Scrolling waterfall: X = time (newest on right), Y = frequency (low at bottom).

sealed class SpectrogramWidget : Widget
{
    const int FftSize  = 1024;
    const int BinCount = FftSize / 2;

    // Pixel buffer: Width × BinCount RGBA
    const int ImgW = 256;
    const int ImgH = BinCount; // 512 rows = 512 freq bins (displayed cropped to widget height)

    readonly byte[]  _pixels  = new byte[ImgW * ImgH * 4];
    readonly float[] _fftReal = new float[FftSize];
    readonly float[] _fftImag = new float[FftSize];
    readonly float[] _window  = new float[FftSize];
    readonly float[] _sampleBuf = new float[FftSize];
    int  _sampleFill;
    int  _imgHandle   = -1;  // NVG image handle (-1 = not created yet)
    bool _dirty;

    public SpectrogramWidget()
    {
        for (int i = 0; i < FftSize; i++)
            _window[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (FftSize - 1)));
    }

    public void PushSamples(ReadOnlySpan<float> mono)
    {
        foreach (float s in mono)
        {
            _sampleBuf[_sampleFill++] = s;
            if (_sampleFill >= FftSize)
            {
                ProcessColumn();
                _sampleFill = 0;
            }
        }
    }

    void ProcessColumn()
    {
        for (int i = 0; i < FftSize; i++)
        {
            _fftReal[i] = _sampleBuf[i] * _window[i];
            _fftImag[i] = 0f;
        }
        SpectrumWidget.Fft(_fftReal, _fftImag); // reuse static method via class name

        // Shift existing columns left by 1 pixel
        for (int y = 0; y < ImgH; y++)
        {
            int dst = y * ImgW * 4;
            int src = dst + 4;
            Buffer.BlockCopy(_pixels, src, _pixels, dst, (ImgW - 1) * 4);
        }

        // Write new column on the right
        float scale = 2f / FftSize;
        for (int bin = 0; bin < BinCount; bin++)
        {
            float mag = MathF.Sqrt(_fftReal[bin] * _fftReal[bin] + _fftImag[bin] * _fftImag[bin]) * scale;
            float dB  = mag > 0f ? 20f * MathF.Log10(mag) : -80f;
            float t   = MathF.Max(0f, (dB + 80f) / 80f); // 0 = silent, 1 = loud

            // Color map: black → blue → green → yellow → white
            byte r, g, byt;
            if (t < 0.25f)      { float u = t / 0.25f; r = 0; g = 0; byt = (byte)(u * 200); }
            else if (t < 0.5f)  { float u = (t - 0.25f) / 0.25f; r = 0; g = (byte)(u * 200); byt = (byte)(200 - u * 200); }
            else if (t < 0.75f) { float u = (t - 0.5f) / 0.25f; r = (byte)(u * 220); g = 200; byt = 0; }
            else                { float u = (t - 0.75f) / 0.25f; r = (byte)(220 + u * 35); g = (byte)(200 + u * 55); byt = (byte)(u * 255); }

            // bin 0 = low freq → bottom of image (flip Y)
            int y = ImgH - 1 - bin;
            if (y < 0 || y >= ImgH) continue;
            int idx = (y * ImgW + (ImgW - 1)) * 4;
            _pixels[idx]     = r;
            _pixels[idx + 1] = g;
            _pixels[idx + 2] = byt;
            _pixels[idx + 3] = 255;
        }
        _dirty = true;
    }

    public void Reset()
    {
        Array.Clear(_pixels, 0, _pixels.Length);
        _sampleFill = 0;
        _dirty = true;
    }

    public override Vector2 PreferredSize(Renderer renderer) => FixedSize ?? new Vector2(512, 160);

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;
        float w = Bounds.Width, h = Bounds.Height;
        renderer.FillRoundedRect(new Rect(0, 0, w, h), 4f, UIColor.FromHex("#090912"));

        if (_imgHandle < 0)
        {
            // CreateImageRGBA with null pixels creates a stream_update (mutable) image.
            _imgHandle = renderer.CreateImageRGBA(ImgW, ImgH, 0);
            // Populate the image immediately with current (cleared) pixels
            if (_imgHandle >= 0)
                renderer.UpdateImage(_imgHandle, in _pixels[0]);
        }
        else if (_dirty)
        {
            renderer.UpdateImage(_imgHandle, in _pixels[0]);
            _dirty = false;
        }

        if (_imgHandle >= 0)
            renderer.DrawImage(_imgHandle, new Rect(0, 0, w, h));
    }
}

// ── Piano Keyboard Widget ──────────────────────────────────────────────────────
// Full-screen 2-octave keyboard (C3–B4). Supports mouse and multi-touch.

sealed class PianoKeyboardWidget : Widget
{
    public event Action<float>? NoteOn;
    public event Action?        NoteOff;
    public event Action<int>?   NoteOnSemi;
    public event Action<int>?   NoteOffSemi;

    // 2 octaves: C3..B4 — semitone index 0-23, plus isBlack flag
    static readonly (int semi, bool black)[] Keys = BuildKeys();

    static (int semi, bool black)[] BuildKeys()
    {
        var pattern = new[] { false, true, false, true, false, false, true, false, true, false, true, false };
        var list = new List<(int, bool)>();
        for (int oct = 0; oct < 2; oct++)
            for (int i = 0; i < 12; i++)
                list.Add((oct * 12 + i, pattern[i]));
        return list.ToArray();
    }

    static readonly string[] NoteNames = { "C","C#","D","D#","E","F","F#","G","G#","A","A#","B" };
    static float NoteFreq(int semi) => 440f * MathF.Pow(2f, (semi + 48 - 69) / 12f);

    // Multi-touch/mouse: key → pressed by mouse(-1) or touch id
    readonly Dictionary<int, int> _pressed = new(); // semi → input id (-1=mouse)

    public override Vector2 PreferredSize(Renderer renderer) => FixedSize ?? new Vector2(400, 200);

    (float wkW, float wkH, float bkW, float bkH, int wkCount) CalcLayout(float w, float h)
    {
        int wkCount = Keys.Count(k => !k.black);
        float gap    = MathF.Max(1f, w * 0.002f);   // thin gap between white keys
        float wkW    = (w - gap * (wkCount - 1)) / wkCount;
        float bkW    = wkW * 0.58f;
        float bkH    = h * 0.60f;
        return (wkW, h, bkW, bkH, wkCount);
    }

    // Returns x position of a white key by its white-key index
    float WhiteKeyX(int wkIdx, float wkW, float w)
    {
        int wkCount = Keys.Count(k => !k.black);
        float gap = MathF.Max(1f, w * 0.002f);
        return wkIdx * (wkW + gap);
    }

    int HitTest(float lx, float ly)
    {
        float w = Bounds.Width, h = Bounds.Height;
        var (wkW, wkH, bkW, bkH, _) = CalcLayout(w, h);

        // Black keys first (they're on top)
        int wkIdx = 0;
        for (int i = 0; i < Keys.Length; i++)
        {
            if (!Keys[i].black) { wkIdx++; continue; }
            float kx = WhiteKeyX(wkIdx - 1, wkW, w) + wkW - bkW * 0.5f;
            if (lx >= kx && lx <= kx + bkW && ly >= 0f && ly <= bkH)
                return Keys[i].semi;
        }
        // White keys
        wkIdx = 0;
        for (int i = 0; i < Keys.Length; i++)
        {
            if (Keys[i].black) continue;
            float kx = WhiteKeyX(wkIdx, wkW, w);
            if (lx >= kx && lx <= kx + wkW && ly >= 0f && ly <= wkH)
                return Keys[i].semi;
            wkIdx++;
        }
        return -1;
    }

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;
        float w = Bounds.Width, h = Bounds.Height;
        var (wkW, wkH, bkW, bkH, _) = CalcLayout(w, h);
        renderer.FillRect(new Rect(0, 0, w, h), UIColor.FromHex("#0A0A12"));

        // White keys
        int wkIdx = 0;
        for (int i = 0; i < Keys.Length; i++)
        {
            if (Keys[i].black) continue;
            float kx = WhiteKeyX(wkIdx, wkW, w);
            bool pressed = _pressed.ContainsKey(Keys[i].semi);
            float r = MathF.Max(3f, wkW * 0.06f);

            // Key body with rounded bottom corners
            renderer.BeginPath();
            renderer.MoveTo(kx + r, 0f);
            renderer.LineTo(kx + wkW - r, 0f);
            renderer.LineTo(kx + wkW - r, wkH - r);
            renderer.ArcTo(kx + wkW, wkH, kx + wkW, wkH - r, r);
            renderer.LineTo(kx + wkW, wkH - r);
            renderer.ArcTo(kx + wkW, wkH, kx + wkW - r, wkH, r);
            renderer.LineTo(kx + r, wkH);
            renderer.ArcTo(kx, wkH, kx, wkH - r, r);
            renderer.LineTo(kx, r);
            renderer.ArcTo(kx, 0f, kx + r, 0f, r);
            renderer.ClosePath();

            if (pressed)
            {
                renderer.SetFillColor(new UIColor(0.40f, 0.78f, 1.00f, 1f));
            }
            else
            {
                // Gradient: warm ivory top → slightly darker at bottom
                renderer.SetFillPaint(renderer.LinearGradient(kx, 0f, kx, wkH,
                    new UIColor(0.97f, 0.96f, 0.92f, 1f),
                    new UIColor(0.82f, 0.80f, 0.76f, 1f)));
            }
            renderer.Fill();

            // Border
            renderer.SetStrokeColor(new UIColor(0.18f, 0.18f, 0.22f, 1f));
            renderer.SetStrokeWidth(1.2f);
            renderer.Stroke();

            // Note label at bottom of key
            int noteInOct = Keys[i].semi % 12;
            if (noteInOct == 0) // C notes only
            {
                renderer.SetFontSize(MathF.Max(9f, wkW * 0.28f));
                renderer.SetFont("sans");
                renderer.SetTextAlign(TextHAlign.Center, TextVAlign.Bottom);
                renderer.SetFillColor(pressed
                    ? new UIColor(0.05f, 0.15f, 0.25f, 0.9f)
                    : new UIColor(0.35f, 0.25f, 0.15f, 0.7f));
                int octave = 3 + Keys[i].semi / 12;
                renderer.DrawTextRaw(kx + wkW * 0.5f, wkH - 6f, $"C{octave}");
            }

            wkIdx++;
        }

        // Black keys
        wkIdx = 0;
        for (int i = 0; i < Keys.Length; i++)
        {
            if (!Keys[i].black) { wkIdx++; continue; }
            float kx = WhiteKeyX(wkIdx - 1, wkW, w) + wkW - bkW * 0.5f;
            bool pressed = _pressed.ContainsKey(Keys[i].semi);
            float r = MathF.Max(2f, bkW * 0.10f);

            renderer.BeginPath();
            renderer.AddRoundedRectVarying(kx, 0f, bkW, bkH, 0f, 0f, r, r);

            if (pressed)
            {
                renderer.SetFillColor(new UIColor(0.15f, 0.50f, 0.80f, 1f));
            }
            else
            {
                renderer.SetFillPaint(renderer.LinearGradient(kx, 0f, kx, bkH,
                    new UIColor(0.20f, 0.20f, 0.24f, 1f),
                    new UIColor(0.06f, 0.06f, 0.08f, 1f)));
            }
            renderer.Fill();

            // Sheen highlight on top edge
            if (!pressed)
            {
                renderer.BeginPath();
                renderer.AddRoundedRect(kx + bkW * 0.15f, 2f, bkW * 0.70f, bkH * 0.18f, r * 0.5f);
                renderer.SetFillColor(new UIColor(1f, 1f, 1f, 0.10f));
                renderer.Fill();
            }
        }
    }

    // ── Mouse input ──────────────────────────────────────────────────────────

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button == MouseButton.Left)
        {
            int semi = HitTest(e.LocalPosition.X, e.LocalPosition.Y);
            if (semi >= 0)
            {
                Press(-1, semi);
                return true;
            }
        }
        return false;
    }

    public override bool OnMouseUp(MouseEvent e)
    {
        if (e.Button == MouseButton.Left)
        {
            ReleaseAll(-1);
            return true;
        }
        return false;
    }

    public override bool OnMouseMove(MouseEvent e)
    {
        if (_pressed.ContainsValue(-1))
        {
            int semi = HitTest(e.LocalPosition.X, e.LocalPosition.Y);
            if (semi >= 0)
            {
                // Find which semi is currently pressed by mouse
                int? prev = null;
                foreach (var kv in _pressed) if (kv.Value == -1) { prev = kv.Key; break; }
                if (prev.HasValue && prev.Value != semi)
                {
                    ReleaseAll(-1);
                    Press(-1, semi);
                }
            }
        }
        return false;
    }

    // ── Touch input ──────────────────────────────────────────────────────────

    public override bool OnTouchDown(TouchEvent e)
    {
        foreach (var pt in e.Touches)
        {
            if (!pt.Changed) continue;
            var local = ToLocal(pt.Position);
            int semi = HitTest(local.X, local.Y);
            if (semi >= 0) Press(pt.Id, semi);
        }
        return true;
    }

    public override bool OnTouchUp(TouchEvent e)
    {
        foreach (var pt in e.Touches)
        {
            if (!pt.Changed) continue;
            ReleaseAll(pt.Id);
        }
        return true;
    }

    public override bool OnTouchMove(TouchEvent e)
    {
        foreach (var pt in e.Touches)
        {
            if (!pt.Changed) continue;
            var local = ToLocal(pt.Position);
            int semi = HitTest(local.X, local.Y);
            if (semi < 0) { ReleaseAll(pt.Id); continue; }
            int? prev = null;
            foreach (var kv in _pressed) if (kv.Value == pt.Id) { prev = kv.Key; break; }
            if (prev.HasValue && prev.Value != semi)
            {
                ReleaseAll(pt.Id);
                Press(pt.Id, semi);
            }
            else if (!prev.HasValue)
            {
                Press(pt.Id, semi);
            }
        }
        return true;
    }

    // ── Press / Release helpers ──────────────────────────────────────────────

    void Press(int inputId, int semi)
    {
        if (_pressed.TryGetValue(semi, out int existing) && existing == inputId) return;
        if (_pressed.ContainsKey(semi)) return;
        _pressed[semi] = inputId;
        NoteOn?.Invoke(NoteFreq(semi));
        NoteOnSemi?.Invoke(semi);
    }

    void ReleaseAll(int inputId)
    {
        var toRelease = new List<int>();
        foreach (var kv in _pressed) if (kv.Value == inputId) toRelease.Add(kv.Key);
        foreach (int semi in toRelease)
        {
            _pressed.Remove(semi);
            NoteOff?.Invoke();
            NoteOffSemi?.Invoke(semi);
        }
    }
}

