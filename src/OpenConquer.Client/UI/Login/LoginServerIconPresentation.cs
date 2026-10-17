using System.Runtime.ExceptionServices;
using OpenConquer.Content.Images;
using OpenConquer.Content.Startup;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;

namespace OpenConquer.Client.UI.Login;

/// <summary>Bounded cache of trusted retail server movies, drawn as each <c>son</c> holder shows them, for one OpenGL context.</summary>
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

        ReadOnlySpan<LoginStagePoint> holders = _layout.ServerHolders;

        for (int slot = 0; slot < holders.Length; slot++)
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

            // The loaded movie's root shape sits at the holder origin, unscaled.
            LoginSpritePlacement bounds = _layout.Offset(holders[slot], 0, 0, texture.Width, texture.Height);
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
