using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Login.Selection;

/// <summary>Owns the single unconfigured-authority banner for one OpenGL context.</summary>
internal sealed class LoginDirectoryStatusPresentation : IDisposable
{
    private static readonly SpriteColor s_backdrop = new(19, 15, 12, 210);
    private static readonly SpriteColor s_border = new(138, 112, 70, 255);
    private readonly OpenGLTexture2D _label;
    private readonly LoginRect _bounds;
    private readonly LoginStageLayout _layout;
    private bool _disposed;

    public LoginDirectoryStatusPresentation(OpenGLGraphicsDevice graphicsDevice, LoginStageLayout layout)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(layout);
        _bounds = LoginDirectoryStatus.Bounds(layout);
        _layout = layout;

        (int width, int height, byte[] pixels) = LoginDirectoryStatusRaster.Create(LoginDirectoryStatus.Text(LoginDirectoryAvailability.NotConfigured));
        int scaledWidth = _layout.Scale(checked(width * 2));
        int scaledHeight = _layout.Scale(checked(height * 2));
        if (scaledWidth > _bounds.Width - 12 || scaledHeight > _bounds.Height - 4)
        {
            throw new InvalidOperationException("The unconfigured-authority banner does not fit its reserved rectangle.");
        }

        _label = graphicsDevice.CreateTexture2D(width, height, pixels);
    }

    public void Draw(OpenGLRenderer renderer, LoginDirectoryAvailability availability)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        if (!LoginDirectoryStatus.IsVisible(availability))
        {
            return;
        }

        int width = _layout.Scale(checked(_label.Width * 2));
        int height = _layout.Scale(checked(_label.Height * 2));
        int x = _bounds.X + (_bounds.Width - width) / 2;
        int y = _bounds.Y + (_bounds.Height - height) / 2;

        renderer.DrawSolidRectangle(_bounds.X, _bounds.Y, _bounds.Width, _bounds.Height, s_backdrop);
        renderer.DrawLineRectangle(_bounds.X, _bounds.Y, _bounds.X + _bounds.Width - 1, _bounds.Y + _bounds.Height - 1, s_border);
        renderer.DrawSprite(_label, x, y, width, height);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _label.Dispose();
    }
}
