using System.Numerics;
using OpenConquer.Client.UI.Flash;
using OpenConquer.Networking.Game.Handshake;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Platform.Windowing.Desktop;

namespace OpenConquer.Client.UI.Role;

/// <summary>The role stage and exchange hosted by the role-creation lifetime owner.</summary>
internal interface IRoleCreationScreen : IAsyncDisposable
{
    event Action? ReturnRequested;
    event Action? ExitRequested;
    FlashStage Stage
    {
        get;
    }
    bool ConnectionLost
    {
        get;
    }
    bool TryTakeAdmittedWorld(out GameServerHandshakeSession? game, out GameWorldEntrySnapshot? world);
    void Tick(TimeSpan elapsed);
    void PointerMoved(Vector2 position);
    void PointerPressed(bool extendSelection);
    void PointerReleased();
    void TextEntered(char character);
    void KeyPressed(DesktopKeyEvent key, Func<string?> readClipboard, Action<string> writeClipboard);
}
