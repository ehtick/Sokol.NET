using System;

namespace Sokol.GUI;

/// <summary>
/// A TextBox that fires <see cref="CommitRequested"/> on Enter/focus-loss
/// and <see cref="CancelRequested"/> on Escape.
/// </summary>
public class CommittableTextBox : TextBox
{
    public Action? CommitRequested;
    public Action? CancelRequested;

    public override bool OnKeyDown(KeyEvent e)
    {
        const int KEY_ESCAPE    = 256;
        const int KEY_ENTER     = 257;
        const int KEY_KP_ENTER  = 335;
        if (e.KeyCode == KEY_ENTER || e.KeyCode == KEY_KP_ENTER) { CommitRequested?.Invoke(); return true; }
        if (e.KeyCode == KEY_ESCAPE)                              { CancelRequested?.Invoke(); return true; }
        return base.OnKeyDown(e);
    }

    public override void OnFocusLost() { base.OnFocusLost(); CommitRequested?.Invoke(); }
}
