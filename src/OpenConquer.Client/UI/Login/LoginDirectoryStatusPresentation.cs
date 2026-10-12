using System.Runtime.ExceptionServices;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Login;

/// <summary>Owns fixed operational-status textures for the active OpenGL context.</summary>
internal sealed class LoginDirectoryStatusPresentation : IDisposable
{
    private static readonly SpriteColor s_backdrop = new(19, 15, 12, 210);
    private static readonly SpriteColor s_border = new(138, 112, 70, 255);
    private readonly OpenGLTexture2D?[] _labels = new OpenGLTexture2D?[(int)LoginDirectoryAvailability.ChooseServer + 1];
    private readonly LoginSpritePlacement _bounds;
    private readonly int _logicalWidth;
    private bool _disposed;

    public LoginDirectoryStatusPresentation(OpenGLGraphicsDevice graphicsDevice, LoginVisualLayout layout)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(layout);
        _bounds = LoginDirectoryStatus.Bounds(layout);
        _logicalWidth = layout.Width;

        try
        {
            for (int index = 1; index < _labels.Length; index++)
            {
                string text = LoginDirectoryStatus.Text((LoginDirectoryAvailability)index);
                (int width, int height, byte[] pixels) = LoginDirectoryStatusRaster.Create(text);
                int scaledWidth = LoginDirectoryStatus.Scale(checked(width * 2), _logicalWidth);
                int scaledHeight = LoginDirectoryStatus.Scale(checked(height * 2), _logicalWidth);

                if (scaledWidth > _bounds.Width - 12 || scaledHeight > _bounds.Height - 4)
                {
                    throw new InvalidOperationException("A directory-status message does not fit in the reserved presentation rectangle.");
                }

                _labels[index] = graphicsDevice.CreateTexture2D(width, height, pixels);
            }
        }
        catch
        {
            foreach (OpenGLTexture2D? texture in _labels)
            {
                try
                {
                    texture?.Dispose();
                }
                catch { }
            }

            throw;
        }
    }

    public void Draw(OpenGLRenderer renderer, LoginDirectoryAvailability availability)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);

        if (availability == LoginDirectoryAvailability.None)
        {
            return;
        }

        if ((uint)availability >= (uint)_labels.Length || _labels[(int)availability] is not { } label)
        {
            throw new ArgumentOutOfRangeException(nameof(availability), availability, "Unknown directory presentation state.");
        }

        int width = LoginDirectoryStatus.Scale(checked(label.Width * 2), _logicalWidth);
        int height = LoginDirectoryStatus.Scale(checked(label.Height * 2), _logicalWidth);
        int x = _bounds.X + (_bounds.Width - width) / 2;
        int y = _bounds.Y + (_bounds.Height - height) / 2;

        renderer.DrawSolidRectangle(_bounds.X, _bounds.Y, _bounds.Width, _bounds.Height, s_backdrop);
        renderer.DrawLineRectangle(_bounds.X, _bounds.Y, _bounds.X + _bounds.Width - 1, _bounds.Y + _bounds.Height - 1, s_border);
        renderer.DrawSprite(label, x, y, width, height);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ExceptionDispatchInfo? firstFailure = null;

        foreach (OpenGLTexture2D? label in _labels)
        {
            try
            {
                label?.Dispose();
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        firstFailure?.Throw();
    }
}
