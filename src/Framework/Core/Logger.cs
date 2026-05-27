using System;

namespace GameEditor.Framework.Core
{
    public enum LogLevel { Info, Warning, Error }

    public static class Logger
    {
        public static event Action<LogLevel, string>? OnLog;

        // Callbacks registered by the host (e.g. GameEditor) when this Logger is
        // running inside an isolated AssemblyLoadContext. When set, all log calls
        // are forwarded to the host's Logger instead of firing the local OnLog event.
        private static Action<string>? _infoCallback;
        private static Action<string>? _warningCallback;
        private static Action<string>? _errorCallback;

        /// <summary>
        /// Called by the host after loading a game DLL to redirect all log output
        /// from this Logger instance to the host's logging system.
        /// </summary>
        public static void RegisterCallbacks(Action<string> info, Action<string> warning, Action<string> error)
        {
            _infoCallback    = info;
            _warningCallback = warning;
            _errorCallback   = error;
        }

        public static void Info(string msg)
        {
            if (_infoCallback != null) _infoCallback(msg);
            else OnLog?.Invoke(LogLevel.Info, msg);
        }

        public static void Warning(string msg)
        {
            if (_warningCallback != null) _warningCallback(msg);
            else OnLog?.Invoke(LogLevel.Warning, msg);
        }

        public static void Error(string msg)
        {
            if (_errorCallback != null) _errorCallback(msg);
            else OnLog?.Invoke(LogLevel.Error, msg);
        }
    }
}
