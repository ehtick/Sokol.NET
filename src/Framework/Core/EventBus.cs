using System;
using Frent;

namespace GameEditor.Framework.Core
{
    public enum PlayModeState { Stopped, Playing, Paused }

    public static class EventBus
    {
        public static event Action<Entity>? EntitySelected;
        public static event Action<Entity>? EntityCreated;
        public static event Action<Entity>? EntityDestroyed;
        public static event Action<Entity, string>? ComponentChanged;
        public static event Action<PlayModeState>? PlayModeChanged;
        public static event Action? SceneLoaded;
        public static event Action? SceneUnloaded;

        public static void RaiseEntitySelected(Entity e)             => EntitySelected?.Invoke(e);
        public static void RaiseEntityCreated(Entity e)              => EntityCreated?.Invoke(e);
        public static void RaiseEntityDestroyed(Entity e)            => EntityDestroyed?.Invoke(e);
        public static void RaiseComponentChanged(Entity e, string c) => ComponentChanged?.Invoke(e, c);
        public static void RaisePlayModeChanged(PlayModeState s)     => PlayModeChanged?.Invoke(s);
        public static void RaiseSceneLoaded()                        => SceneLoaded?.Invoke();
        public static void RaiseSceneUnloaded()                      => SceneUnloaded?.Invoke();
    }
}
