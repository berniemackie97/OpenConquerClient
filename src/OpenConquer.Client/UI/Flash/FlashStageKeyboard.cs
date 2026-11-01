using OpenConquer.Platform.Windowing.Desktop;

namespace OpenConquer.Client.UI.Flash;

/// <summary>Delivers desktop key presses to a Flash stage: key polling, editing keys and clipboard shortcuts.</summary>
internal static class FlashStageKeyboard
{
    /// <summary>Flash <c>Key.ENTER</c>.</summary>
    public const int EnterKeyCode = 13;

    /// <summary>Flash <c>Key.ESCAPE</c>.</summary>
    public const int EscapeKeyCode = 27;

    public static void KeyPressed(FlashStage stage, DesktopKeyEvent key, Func<string?> readClipboard, Action<string> writeClipboard)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(readClipboard);
        ArgumentNullException.ThrowIfNull(writeClipboard);

        if (key.Key is DesktopKey.Enter or DesktopKey.Escape)
        {
            stage.SetKey(key.Key == DesktopKey.Enter ? EnterKeyCode : EscapeKeyCode, true);
            return;
        }

        bool shortcut = (key.Modifiers & (DesktopKeyModifiers.Control | DesktopKeyModifiers.Super)) != 0;
        if (shortcut)
        {
            if (!key.IsRepeat)
            {
                Shortcut(stage, key.Key, readClipboard, writeClipboard);
            }

            return;
        }

        FlashEditKey? edit = key.Key switch
        {
            DesktopKey.Backspace => FlashEditKey.Backspace,
            DesktopKey.Delete => FlashEditKey.Delete,
            DesktopKey.Left => FlashEditKey.Left,
            DesktopKey.Right => FlashEditKey.Right,
            DesktopKey.Home => FlashEditKey.Home,
            DesktopKey.End => FlashEditKey.End,
            _ => null,
        };

        if (edit is { } action && (key.Modifiers & DesktopKeyModifiers.Alt) == 0)
        {
            stage.EditKey(action, (key.Modifiers & DesktopKeyModifiers.Shift) != 0);
        }
    }

    /// <summary>Releases the polled keys after each stage frame, so one press is seen by exactly one frame.</summary>
    public static void ReleasePolledKeys(FlashStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        stage.SetKey(EnterKeyCode, false);
        stage.SetKey(EscapeKeyCode, false);
    }

    private static void Shortcut(FlashStage stage, DesktopKey key, Func<string?> readClipboard, Action<string> writeClipboard)
    {
        switch (key)
        {
            case DesktopKey.A:
                stage.SelectAll();
                break;
            case DesktopKey.C:
                if (stage.SelectedText() is { Length: > 0 } selection)
                {
                    writeClipboard(selection);
                }

                break;
            case DesktopKey.X:
                string selected = stage.SelectedText();
                if (selected.Length != 0 && stage.CutSelection())
                {
                    writeClipboard(selected);
                }

                break;
            case DesktopKey.V:
                if (readClipboard() is { } paste)
                {
                    stage.TextEntered(paste);
                }

                break;
        }
    }
}
