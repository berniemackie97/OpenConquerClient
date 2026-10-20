using OpenConquer.Client.UI.Login.Selection;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Login.Status;

/// <summary>
/// Bounded diagnostic login notice until the native error/loading movie
/// transition is ported. Does not impersonate a completed world screen.
/// </summary>
internal sealed class LoginAttemptStatusPresentation : IDisposable
{
    private static readonly SpriteColor s_background = new(19, 15, 12, 210);
    private static readonly SpriteColor s_outline = new(138, 112, 70, 255);
    private readonly OpenGLTexture2D?[] _labels =
        new OpenGLTexture2D[Enum.GetValues<LoginAttemptStatus>().Length];
    private readonly LoginRect _bounds;
    private readonly LoginStageLayout _layout;

    internal LoginAttemptStatusPresentation(OpenGLGraphicsDevice device, LoginStageLayout layout)
    {
        ArgumentNullException.ThrowIfNull(device);
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _bounds = LoginDirectoryStatus.Bounds(layout);
        try
        {
            foreach (LoginAttemptStatus status in Enum.GetValues<LoginAttemptStatus>())
            {
                if (status == LoginAttemptStatus.Idle)
                {
                    continue;
                }

                (int width, int height, byte[] pixels) = LoginDirectoryStatusRaster.Create(Text(status));
                OpenGLTexture2D texture = device.CreateTexture2D(width, height, pixels);
                _labels[(int)status] = texture;
                if (layout.Scale(width * 2) > _bounds.Width - 12 ||
                    layout.Scale(height * 2) > _bounds.Height - 4)
                {
                    throw new InvalidOperationException("Login feedback does not fit the reserved status area.");
                }
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal void Draw(OpenGLRenderer renderer, LoginAttemptStatus status)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        if (status == LoginAttemptStatus.Idle)
        {
            return;
        }

        OpenGLTexture2D label = _labels[(int)status] ??
            throw new ArgumentOutOfRangeException(nameof(status));

        int width = _layout.Scale(label.Width * 2);
        int height = _layout.Scale(label.Height * 2);
        int x = _bounds.X + (_bounds.Width - width) / 2;
        int y = _bounds.Y + (_bounds.Height - height) / 2;

        renderer.DrawSolidRectangle(_bounds.X, _bounds.Y, _bounds.Width, _bounds.Height, s_background);
        renderer.DrawLineRectangle(_bounds.X, _bounds.Y,
            _bounds.X + _bounds.Width - 1, _bounds.Y + _bounds.Height - 1, s_outline);
        renderer.DrawSprite(label, x, y, width, height);
    }

    public void Dispose()
    {
        foreach (OpenGLTexture2D? texture in _labels)
        {
            texture?.Dispose();
        }
    }

    private static string Text(LoginAttemptStatus status) => status switch
    {
        LoginAttemptStatus.SelectRealm => "SELECT A LOCAL REALM",
        LoginAttemptStatus.EnterCredentials => "ENTER ACCOUNT AND PASSWORD",
        LoginAttemptStatus.ConnectingAccount => "CONNECTING TO ACCOUNT SERVER",
        LoginAttemptStatus.AccountRefused => "ACCOUNT LOGIN REFUSED",
        LoginAttemptStatus.ConnectingGame => "CONNECTING TO GAME SERVER",
        LoginAttemptStatus.ReadingCharacter => "LOADING CHARACTER DATA",
        LoginAttemptStatus.GameRefused => "GAME SERVER REFUSED LOGIN",
        LoginAttemptStatus.NewRoleUnavailable => "CHARACTER CREATION NOT READY",
        LoginAttemptStatus.CharacterReceived => "CHARACTER RECEIVED WORLD PENDING",
        LoginAttemptStatus.Timeout => "LOGIN CONNECTION TIMED OUT",
        LoginAttemptStatus.Unavailable => "LOGIN FAILED CHECK LOCAL SERVERS",
        LoginAttemptStatus.Cancelled => "LOGIN CANCELLED",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}
