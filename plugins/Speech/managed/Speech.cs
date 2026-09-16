using System;
using System.Collections.Generic;

/// <summary>
/// Platform text-to-speech for Sokol.NET apps — the OS engine synthesises at RUNTIME, so no
/// voice licence attaches to the app and nothing is bundled. Backends: Android
/// <c>TextToSpeech</c>, iOS/macOS <c>AVSpeechSynthesizer</c>, Windows
/// <c>Windows.Media.SpeechSynthesis</c>, web <c>speechSynthesis</c>, Linux <c>spd-say</c>
/// (speech-dispatcher, probed at runtime, never linked). All calls are non-blocking; results
/// arrive as <see cref="SpeechEvent"/>s raised by <see cref="Speech.Poll"/>, which the app calls
/// once per frame from its game loop (the NearNet.Poll model).
///
/// <see cref="Available(string)"/> means "an OFFLINE voice for this language is installed right
/// now" — re-check it at game start, the user can install or remove voices at any time. A
/// platform without a backend answers <c>false</c> for every language and <see cref="Say"/>
/// returns 0, so callers switch to their no-speech tier without <c>#if</c>.
/// </summary>
public enum SpeechEventType
{
    /// <summary>Engine initialised; <c>Code == 0</c> means usable.</summary>
    Ready   = 1,
    /// <summary>Utterance <c>Id</c> began playing.</summary>
    Started = 2,
    /// <summary>Utterance <c>Id</c> finished; <c>Code == 0</c> spoken to the end, <c>1</c> cancelled.</summary>
    Done    = 3,
    /// <summary>Utterance <c>Id</c> failed; <c>Code</c> is the engine's error.</summary>
    Error   = 4,
}

public readonly struct SpeechEvent
{
    public SpeechEventType Type { get; init; }
    public int Id { get; init; }
    public int Code { get; init; }
}

public static class Speech
{
    /// <summary>True when this platform has a speech backend at all (Linux: when <c>spd-say</c> is
    /// on the PATH). A backend can still lack the voice for a given language — ask
    /// <see cref="Available(string)"/>.</summary>
#if __ANDROID__ || __IOS__ || __MACOS__ || __WINDOWS__ || WEB
    public static bool Supported => true;
#elif __LINUX__
    public static bool Supported => SpdSay.Installed;
#else
    public static bool Supported => false;
#endif

    /// <summary>The engine reported <see cref="SpeechEventType.Ready"/> with code 0. Android
    /// initialises asynchronously; until then <see cref="Available(string)"/> is false.</summary>
    public static bool Ready { get; private set; }

    /// <summary>The utterance id in flight (0 = idle). Cleared by its Done/Error event or by
    /// <see cref="Stop"/>.</summary>
    public static int Speaking { get; private set; }

    /// <summary>Raised from within <see cref="Poll"/>, on the polling thread.</summary>
    public static event Action<SpeechEvent>? OnEvent;

    static readonly Queue<SpeechEvent> _stubQueue = new();
    static bool _inited;

    /// <summary>Create the engine. Call once at app start; idempotent.</summary>
    public static void Init()
    {
        if (_inited) return;
        _inited = true;
#if __ANDROID__ || __IOS__ || __MACOS__ || __WINDOWS__ || WEB
        SokolSpeech.Init();
#elif __LINUX__
        _stubQueue.Enqueue(new SpeechEvent { Type = SpeechEventType.Ready, Code = SpdSay.Installed ? 0 : -1 });
#else
        _stubQueue.Enqueue(new SpeechEvent { Type = SpeechEventType.Ready, Code = -1 });
#endif
    }

    /// <summary>An OFFLINE voice for <paramref name="lang"/> (BCP-47: "he", "pt-BR", "en-US"; a
    /// bare language matches any region) is installed right now.</summary>
    public static bool Available(string lang)
    {
        if (!Ready || string.IsNullOrEmpty(lang)) return false;
#if __ANDROID__ || __IOS__ || __MACOS__ || __WINDOWS__ || WEB
        return SokolSpeech.Available(lang);
#elif __LINUX__
        return SpdSay.Available(lang);
#else
        return false;
#endif
    }

    /// <summary>Speak <paramref name="text"/> in <paramref name="lang"/>, interrupting whatever is
    /// playing. Returns the utterance id (> 0) the Started/Done/Error events will carry, or 0 when
    /// no offline voice exists (nothing is queued then).</summary>
    public static int Say(string text, string lang)
    {
        if (!Ready || string.IsNullOrEmpty(text) || string.IsNullOrEmpty(lang)) return 0;
        int id;
#if __ANDROID__ || __IOS__ || __MACOS__ || __WINDOWS__ || WEB
        id = SokolSpeech.Say(text, lang);
#elif __LINUX__
        id = SpdSay.Say(text, lang, _stubQueue);
#else
        id = 0;
#endif
        if (id > 0) Speaking = id;
        return id;
    }

    /// <summary>Stop the current utterance.</summary>
    public static void Stop()
    {
        Speaking = 0;
#if __ANDROID__ || __IOS__ || __MACOS__ || __WINDOWS__ || WEB
        SokolSpeech.Stop();
#elif __LINUX__
        SpdSay.Stop();
#endif
    }

    /// <summary>Open the system page where voices are installed (Android: the engine's voice-data
    /// installer or TTS settings; iOS/macOS: the app's Settings page). No-op elsewhere.</summary>
    public static void OpenVoiceSettings()
    {
#if __ANDROID__ || __IOS__ || __MACOS__ || __WINDOWS__ || WEB
        SokolSpeech.OpenVoiceSettings();
#endif
    }

    /// <summary>Drain pending engine events; call once per frame from the game loop.</summary>
    public static void Poll()
    {
        while (_stubQueue.Count > 0) Dispatch(_stubQueue.Dequeue());
#if __ANDROID__ || __IOS__ || __MACOS__ || __WINDOWS__ || WEB
        while (SokolSpeech.PollEvent(out SokolSpeech.Event e))
            Dispatch(new SpeechEvent { Type = (SpeechEventType)e.type, Id = e.id, Code = e.code });
#elif __LINUX__
        SpdSay.Reap(_stubQueue);
        while (_stubQueue.Count > 0) Dispatch(_stubQueue.Dequeue());
#endif
    }

    /// <summary>Release the engine (app shutdown).</summary>
    public static void Shutdown()
    {
        Speaking = 0;
        Ready = false;
        _inited = false;
#if __ANDROID__ || __IOS__ || __MACOS__ || __WINDOWS__ || WEB
        SokolSpeech.Shutdown();
#elif __LINUX__
        SpdSay.Stop();
#endif
    }

    static void Dispatch(SpeechEvent e)
    {
        switch (e.Type)
        {
            case SpeechEventType.Ready: Ready = e.Code == 0; break;
            case SpeechEventType.Done:
            case SpeechEventType.Error:
                if (e.Id == Speaking) Speaking = 0;
                break;
        }
        OnEvent?.Invoke(e);
    }

#if __LINUX__
    /// <summary>Linux: speech-dispatcher's <c>spd-say</c> client, run as a process. Nothing is
    /// linked (espeak-ng behind it is GPL) — if the tool is not installed, speech is simply
    /// unavailable. Availability = the daemon lists a voice whose language matches.</summary>
    static class SpdSay
    {
        static bool? _installed;
        static string[]? _langs;
        static int _nextId = 1;
        static System.Diagnostics.Process? _proc;
        static int _procId;

        public static bool Installed
        {
            get
            {
                if (_installed == null)
                {
                    _installed = false;
                    foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':'))
                        if (dir.Length > 0 && System.IO.File.Exists(System.IO.Path.Combine(dir, "spd-say"))) { _installed = true; break; }
                }
                return _installed.Value;
            }
        }

        /// <summary>Languages of the voices <c>spd-say -L</c> lists (NAME LANGUAGE VARIANT rows).</summary>
        static string[] Langs()
        {
            if (_langs != null) return _langs;
            var found = new List<string>();
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("spd-say", "-L") { RedirectStandardOutput = true, UseShellExecute = false };
                using var p = System.Diagnostics.Process.Start(psi);
                if (p != null)
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);
                    foreach (var line in output.Split('\n'))
                    {
                        var cols = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (cols.Length >= 2 && cols[0] != "NAME") found.Add(cols[1].ToLowerInvariant());
                    }
                }
            }
            catch { }
            _langs = found.ToArray();
            return _langs;
        }

        public static bool Available(string lang)
        {
            if (!Installed) return false;
            string want = lang.Replace('_', '-').ToLowerInvariant();
            string wantLang = want.Split('-')[0];
            foreach (var l in Langs())
                if (l == want || l.Split('-')[0] == wantLang) return true;
            return false;
        }

        public static int Say(string text, string lang, Queue<SpeechEvent> events)
        {
            if (!Available(lang)) return 0;
            Stop();
            int id = _nextId++;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("spd-say") { UseShellExecute = false };
                psi.ArgumentList.Add("-w");                       // wait for the utterance: exit = Done
                psi.ArgumentList.Add("-l"); psi.ArgumentList.Add(lang.Split('-', '_')[0]);
                psi.ArgumentList.Add(text);
                _proc = System.Diagnostics.Process.Start(psi);
                _procId = id;
                if (_proc == null) { events.Enqueue(new SpeechEvent { Type = SpeechEventType.Error, Id = id, Code = -1 }); return id; }
                events.Enqueue(new SpeechEvent { Type = SpeechEventType.Started, Id = id });
            }
            catch
            {
                events.Enqueue(new SpeechEvent { Type = SpeechEventType.Error, Id = id, Code = -1 });
            }
            return id;
        }

        public static void Stop()
        {
            try
            {
                if (_proc != null && !_proc.HasExited)
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("spd-say", "-S") { UseShellExecute = false };
                    System.Diagnostics.Process.Start(psi)?.WaitForExit(1000);
                }
            }
            catch { }
        }

        /// <summary>Called from Poll: an exited <c>-w</c> process means the utterance ended.</summary>
        public static void Reap(Queue<SpeechEvent> events)
        {
            if (_proc == null) return;
            try
            {
                if (!_proc.HasExited) return;
                events.Enqueue(new SpeechEvent { Type = SpeechEventType.Done, Id = _procId, Code = _proc.ExitCode == 0 ? 0 : 1 });
                _proc.Dispose();
            }
            catch { }
            _proc = null;
        }
    }
#endif
}
