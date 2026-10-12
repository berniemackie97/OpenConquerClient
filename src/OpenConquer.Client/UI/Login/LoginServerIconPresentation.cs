using System.Runtime.ExceptionServices;
using OpenConquer.Content.Images;
using OpenConquer.Content.Startup;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;

namespace OpenConquer.Client.UI.Login;

/// <summary>Bounded cache of trusted retail-derived server icons for one OpenGL context.</summary>
internal sealed class LoginServerIconPresentation : IDisposable
{
    private readonly OpenGLGraphicsDevice _graphics;
    private readonly LoginVisualLayout _layout;
    private readonly Dictionary<string, OpenGLTexture2D> _textures = new(StringComparer.Ordinal);
    private bool _disposed;

    public LoginServerIconPresentation(OpenGLGraphicsDevice graphics, LoginVisualLayout layout)
    {
        _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
    }

    public void Draw(OpenGLRenderer renderer, LoginServerSelection selection)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(selection);

        ReadOnlySpan<LoginSpritePlacement> cells = _layout.ServerCells;

        for (int slot = 0; slot < cells.Length; slot++)
        {
            if (selection.ServerAtSlot(slot) is not { IconId: { } iconId })
            {
                continue;
            }

            if (!_textures.TryGetValue(iconId, out OpenGLTexture2D? texture))
            {
                RgbaImage image = LoginRealmIconAssets.Load(iconId);
                texture = _graphics.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
                _textures.Add(iconId, texture);
            }

            LoginSpritePlacement bounds = LoginServerIconLayout.Fit(cells[slot], texture.Width, texture.Height);
            renderer.DrawSprite(texture, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ExceptionDispatchInfo? firstFailure = null;

        foreach (OpenGLTexture2D texture in _textures.Values)
        {
            try
            {
                texture.Dispose();
            }
            catch (Exception exception) { firstFailure ??= ExceptionDispatchInfo.Capture(exception); }
        }

        _textures.Clear();
        firstFailure?.Throw();
    }
}

/// <summary>Aspect-preserving fit into the existing retail server-cell interior.</summary>
internal static class LoginServerIconLayout
{
    public static LoginSpritePlacement Fit(LoginSpritePlacement cell, int imageWidth, int imageHeight)
    {
        if (cell.Width < 3 || cell.Height < 3 || imageWidth <= 0 || imageHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cell), "The server cell or icon dimensions are invalid.");
        }

        int availableWidth = cell.Width - 2;
        int availableHeight = cell.Height - 2;
        int width = availableWidth;
        int height = checked((int)((long)imageHeight * width / imageWidth));

        if (height > availableHeight)
        {
            height = availableHeight;
            width = checked((int)((long)imageWidth * height / imageHeight));
        }

        width = Math.Max(1, width);
        height = Math.Max(1, height);
        return new LoginSpritePlacement(checked(cell.X + (cell.Width - width) / 2), checked(cell.Y + (cell.Height - height) / 2), width, height);
    }
}
