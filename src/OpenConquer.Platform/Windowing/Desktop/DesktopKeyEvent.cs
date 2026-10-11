namespace OpenConquer.Platform.Windowing.Desktop;

/// <summary>
/// A desktop command-key event. Text characters are delivered by DesktopWindow.TextEntered.
/// </summary>
public readonly record struct DesktopKeyEvent(DesktopKey Key, DesktopKeyModifiers Modifiers, bool IsRepeat);
