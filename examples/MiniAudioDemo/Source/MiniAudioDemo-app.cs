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

    // ── Engine tab — live stats label updated each frame ──────────────────────
    static Label? _engActiveLabel;

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

        // Master volume slider
        var masterVolLbl = new Label { Text = "1.00  (0.0 dB)", FixedSize = new Vector2(150, 26) };
        float initVol    = _engineReady ? ma_engine_get_volume(_engine) : 1f;
        var volRow = new Panel
        {
            Layout    = new BoxLayout(Orientation.Horizontal, Alignment.Center, 12),
            FixedSize = new Vector2(0, 32),
        };
        var masterSlider = new Slider { Min = 0f, Max = 2f, Value = initVol, FixedSize = new Vector2(280, 24) };
        masterSlider.ValueChanged += v =>
        {
            if (_engineReady) ma_engine_set_volume(_engine, v);
            float dB = v > 0f ? 20f * MathF.Log10(v) : float.NegativeInfinity;
            masterVolLbl.Text = float.IsNegativeInfinity(dB)
                ? $"{v:F2}  (\u2212\u221e dB)"
                : $"{v:F2}  ({dB:+0.0;-0.0} dB)";
        };
        volRow.AddChild(new Label { Text = "Master Vol:", FixedSize = new Vector2(85, 26) });
        volRow.AddChild(masterSlider);
        volRow.AddChild(masterVolLbl);
        root.AddChild(volRow);

        root.AddChild(new Separator());

        root.AddChild(new Label { Text = "Live Stats", FontSize = 16 });
        _engActiveLabel = new Label { Text = "Active sounds: 0", ForeColor = UIColor.FromHex("#66AAFF") };
        root.AddChild(_engActiveLabel);
        root.AddChild(new Label
        {
            Text      = _engineReady ? "\u2713 Engine ready" : "\u2717 Engine init failed",
            ForeColor = _engineReady ? UIColor.FromHex("#66DD88") : UIColor.FromHex("#FF6666"),
        });

        return new ScrollView { Content = root, CanScrollVertical = true };
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

