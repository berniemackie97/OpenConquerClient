namespace OpenConquer.Platform;

/// <summary>
/// Determines how the desktop game window is presented independently of the logical render size.
/// </summary>
public enum DesktopWindowMode
{
    /// <summary>Allows the player to resize the game window.</summary>
    Resizable,

    /// <summary>Uses the requested desktop size as a non-resizable window.</summary>
    Fixed,

    /// <summary>Requests the desktop size while presenting the game fullscreen.</summary>
    Fullscreen,
}
