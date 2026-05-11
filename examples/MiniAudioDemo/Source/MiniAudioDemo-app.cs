using System;
using System.Collections.Generic;
using System.Diagnostics;
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

public static unsafe class MiniaudiodemoApp
{
    // ── Audio file catalogue ──────────────────────────────────────────────────
    struct AudioFile { public string Path; public string Label; public bool IsMusic; }
    static readonly AudioFile[] _audioFiles =
    {
        new() { Path = "Music/music.ogg",      Label = "music",      IsMusic = true  },
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
    struct ActiveSound { public ma_sound* Sound; }
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
            logger      = { func = &slog_func },
        });

        stm_setup();
        SFilesystem.Initialize();

        _engine = (ma_engine*)NativeMemory.AllocZeroed((nuint)sizeof(ma_engine));
        var engCfg = ma_engine_config_init();
        _engineReady = ma_engine_init(in engCfg, _engine) == ma_result.MA_SUCCESS;

        _passAction = default;
        _passAction.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        _passAction.colors[0].clear_value = new sg_color { r = 0.12f, g = 0.12f, b = 0.15f, a = 1f };
        _passAction.depth.load_action     = sg_load_action.SG_LOADACTION_CLEAR;
        _passAction.depth.clear_value     = 1.0f;
        _passAction.stencil.load_action   = sg_load_action.SG_LOADACTION_CLEAR;
        _passAction.stencil.clear_value   = 0;

        _vg = nvgCreateSokol(NVG_ANTIALIAS | NVG_STENCIL_STROKES);
        _screen = Screen.Initialize(_vg);
        FontRegistry.Instance.RegisterAsync(_vg, "sans", "fonts/Roboto-Regular.ttf");
        FontRegistry.Instance.RegisterAsync(_vg, "bold", "fonts/Roboto-Bold.ttf");

        foreach (var af in _audioFiles)
            LoadAudioFileAsync(af.Path);

        BuildUI();
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

        // Drain finished one-shot sounds
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var s = _active[i];
            if (ma_sound_at_end(in *s.Sound) != 0)
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

        foreach (var s in _active)
        {
            ma_sound_uninit(s.Sound);
            NativeMemory.Free(s.Sound);
        }
        _active.Clear();

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
        tabs.AddTab("Engine",   BuildEngineTab());
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

        var musicRow = new Panel
        {
            Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Center, 12),
            FixedSize = new Vector2(0, 40),
        };
        _musicButton = new Button("▶  Play") { CornerRadius = 6 };
        _musicButton.Clicked += () => ToggleMusic("Music/music.ogg");
        musicRow.AddChild(_musicButton);
        musicRow.AddChild(new Label { Text = "music.ogg  (loops while playing)" });
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
            if (!_loaded.ContainsKey("Music/music.ogg")) { statusLbl.Text = "Music not loaded yet"; return; }
            _fadeSound = (ma_sound*)NativeMemory.AllocZeroed((nuint)sizeof(ma_sound));
            uint flags = (uint)(ma_sound_flags.MA_SOUND_FLAG_DECODE | ma_sound_flags.MA_SOUND_FLAG_NO_SPATIALIZATION);
            if (ma_sound_init_from_file(_engine, "Music/music.ogg", flags, null, null, _fadeSound) != ma_result.MA_SUCCESS)
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
            Layout = new BoxLayout(Orientation.Horizontal, Alignment.Stretch, 8),
            Expand = true,
        };

        _oscWidget = new OscilloscopeWidget { Expand = true };
        vizRow.AddChild(_oscWidget);

        _vuWidget = new VuMeterWidget { FixedSize = new Vector2(90, 0) };
        vizRow.AddChild(_vuWidget);

        root.AddChild(vizRow);
        root.Expand = true;

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

        return root;
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

        // Sound picker
        var sfxPaths  = new List<string>();
        var sfxLabels = new List<string>();
        foreach (var af in _audioFiles)
            if (!af.IsMusic) { sfxPaths.Add(af.Path); sfxLabels.Add(af.Label); }

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
            Expand  = true,
            MaxDist = spatMaxDist,
            MinDist = spatMinDist,
        };
        _spatCanvas.PositionChanged += (x, z) =>
        {
            if (_spatSound != null) ma_sound_set_position(_spatSound, x, 0f, z);
        };
        root.AddChild(_spatCanvas);

        root.Expand = true;

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

        return root;
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
            Expand  = true,
        };

        _eqVuWidget = new VuMeterWidget { FixedSize = new Vector2(90, 0) };
        outer.AddChild(controls);
        outer.AddChild(_eqVuWidget);

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
            var decCfg = ma_decoder_config_init(ma_format.ma_format_f32, 2, 0);
            _eqDecoder = (ma_decoder*)NativeMemory.AllocZeroed((nuint)sizeof(ma_decoder));
            if (ma_decoder_init_file(af.Path, in decCfg, _eqDecoder) != ma_result.MA_SUCCESS)
            {
                NativeMemory.Free(_eqDecoder);
                _eqDecoder = null;
            }

            playBtn.Text = "■  Stop";
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

        // Draw waveform curve using raw NanoVG through VGContext
        var vg   = renderer.VGContext;
        int n    = (int)(w - 8f);
        if (n < 2) { renderer.Restore(); return; }

        // Fixed display window = 3 cycles at 440 Hz reference (~6.8ms).
        // As Freq changes the number of cycles drawn changes, making the
        // visual update in real time.
        float displayTime = 3f / 440f;
        float usableH = (h - 16f) * 0.5f;

        static NVGcolor NvgRgbaf(float r, float g, float b, float a) =>
            new NVGcolor { r = r, g = g, b = b, a = a };

        nvgBeginPath(vg);
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
            if (i == 0) nvgMoveTo(vg, px, py); else nvgLineTo(vg, px, py);
        }
        nvgStrokeColor(vg, NvgRgbaf(0.2f, 0.85f, 1f, 1f));
        nvgStrokeWidth(vg, 2f);
        nvgStroke(vg);

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

        DrawBar(renderer, lx, ty, barW, barH, _smoothRmsL, _holdL);
        DrawBar(renderer, rx, ty, barW, barH, _smoothRmsR, _holdR);

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
                        float rms, float hold)
    {
        // Background
        renderer.FillRect(new Rect(x, y, bw, bh), UIColor.FromHex("#1A1A2E"));

        // dB tick marks at 0, -6, -12, -18, -24 dBFS
        ReadOnlySpan<float> dbTicks = stackalloc float[] { 0f, -6f, -12f, -18f, -24f };
        foreach (float db in dbTicks)
        {
            float level = db >= 0f ? 1f : MathF.Pow(10f, db / 20f);
            float ty = y + bh - level * bh;
            if (ty >= y && ty <= y + bh)
                renderer.FillRect(new Rect(x, ty, bw, 1f), UIColor.FromHex("#2A3A4A"));
        }

        // RMS fill bar with green→yellow→red gradient (bottom to top)
        float fillH = MathF.Min(rms, 1f) * bh;
        if (fillH >= 1f)
        {
            float barY = y + bh - fillH;
            var vg     = renderer.VGContext;
            // Gradient bottom (green) → top (red), scaled by fill so short bars stay green
            var paint = nvgLinearGradient(vg, x, y + bh, x, y,
                new NVGcolor { r = 0.15f, g = 0.85f, b = 0.20f, a = 1f },   // green at bottom
                new NVGcolor { r = 0.95f, g = 0.20f, b = 0.15f, a = 1f });  // red at top
            nvgBeginPath(vg);
            nvgRect(vg, x, barY, bw, fillH);
            nvgFillPaint(vg, paint);
            nvgFill(vg);
        }

        // Peak hold line (white)
        if (hold > 0.005f)
        {
            float holdY = y + bh - MathF.Min(hold, 1f) * bh;
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

        var vg = renderer.VGContext;

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
                var paint = nvgLinearGradient(vg, bx, centerY, bx, Pad,
                    new NVGcolor { r = 0.05f, g = 0.55f, b = 1.0f, a = 0.85f },  // blue at center
                    new NVGcolor { r = 0.30f, g = 0.85f, b = 1.0f, a = 1.0f });  // bright cyan at top
                nvgBeginPath(vg);
                nvgRoundedRect(vg, bx, barY, barW, fillH, 3f);
                nvgFillPaint(vg, paint);
                nvgFill(vg);
            }
            else
            {
                // Cut: grow downward from center line
                float fillH = -norm * barAreaH * 0.5f;
                float barY  = centerY;
                var paint = nvgLinearGradient(vg, bx, centerY, bx, Pad + barAreaH,
                    new NVGcolor { r = 1.0f, g = 0.55f, b = 0.05f, a = 0.85f },  // orange at center
                    new NVGcolor { r = 1.0f, g = 0.15f, b = 0.0f,  a = 1.0f });  // red at bottom
                nvgBeginPath(vg);
                nvgRoundedRect(vg, bx, barY, barW, fillH, 3f);
                nvgFillPaint(vg, paint);
                nvgFill(vg);
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

        var vg = renderer.VGContext;

        // Cross-hair axes
        nvgBeginPath(vg);
        nvgMoveTo(vg, cx, cy - pixelRadius); nvgLineTo(vg, cx, cy + pixelRadius);
        nvgMoveTo(vg, cx - pixelRadius, cy); nvgLineTo(vg, cx + pixelRadius, cy);
        nvgStrokeColor(vg, new NVGcolor { r = 0.2f, g = 0.3f, b = 0.4f, a = 0.3f });
        nvgStrokeWidth(vg, 1f);
        nvgStroke(vg);

        // Max distance ring (blue) — grows when MaxDist slider increases
        float maxPR = MathF.Min(MaxDist * scale, pixelRadius);
        nvgBeginPath(vg);
        nvgCircle(vg, cx, cy, maxPR);
        nvgStrokeColor(vg, new NVGcolor { r = 0.3f, g = 0.5f, b = 1f, a = 0.8f });
        nvgStrokeWidth(vg, 2f);
        nvgStroke(vg);

        // Min distance ring (green) — grows when MinDist slider increases
        float minPR = MathF.Min(MinDist * scale, pixelRadius);
        nvgBeginPath(vg);
        nvgCircle(vg, cx, cy, minPR);
        nvgStrokeColor(vg, new NVGcolor { r = 0.3f, g = 0.8f, b = 0.3f, a = 0.7f });
        nvgStrokeWidth(vg, 1.5f);
        nvgStroke(vg);

        // Listener (cyan filled circle)
        nvgBeginPath(vg);
        nvgCircle(vg, cx, cy, 8f);
        nvgFillColor(vg, new NVGcolor { r = 0.2f, g = 0.9f, b = 1f, a = 1f });
        nvgFill(vg);

        // Sound source (orange filled circle)
        float sx = cx + SourceX * scale;
        float sz = cy + SourceZ * scale;
        nvgBeginPath(vg);
        nvgCircle(vg, sx, sz, 10f);
        nvgFillColor(vg, new NVGcolor { r = 1f, g = 0.55f, b = 0.1f, a = 1f });
        nvgFill(vg);
        nvgStrokeColor(vg, new NVGcolor { r = 1f, g = 0.8f, b = 0.5f, a = 1f });
        nvgStrokeWidth(vg, 2f);
        nvgStroke(vg);

        // Distance line from listener to source
        nvgBeginPath(vg);
        nvgMoveTo(vg, cx, cy);
        nvgLineTo(vg, sx, sz);
        nvgStrokeColor(vg, new NVGcolor { r = 0.7f, g = 0.5f, b = 0.2f, a = 0.5f });
        nvgStrokeWidth(vg, 1f);
        nvgStroke(vg);

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

