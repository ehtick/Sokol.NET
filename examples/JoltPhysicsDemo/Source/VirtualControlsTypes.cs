using Sokol;

/// <summary>Describes the type of virtual joystick input a demo uses.</summary>
public enum VirtualControlsType
{
    None,    // No virtual controls
    Arrows,  // Joystick maps to arrow keys (UP/DOWN/LEFT/RIGHT)
    WASD,    // Joystick maps to WASD keys (character controls: W/S/A/D)
}

/// <summary>An action button shown alongside the virtual joystick.</summary>
public struct VirtualActionButton
{
    public string Label;
    public SApp.sapp_keycode Key;
}
