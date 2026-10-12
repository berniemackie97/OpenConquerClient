using System.Runtime.ExceptionServices;
using OpenConquer.Content.Startup;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Login;

/// <summary>Owns the original retail Enter/Exit image states for one OpenGL context.</summary>
internal sealed class LoginActionPresentation : IDisposable
{
    private static readonly SpriteColor s_inactive = new(116, 116, 116, 255);
    private readonly LoginActionLayout _layout;
    private readonly OpenGLTexture2D[] _textures;
    private bool _disposed;

    public LoginActionPresentation(OpenGLGraphicsDevice graphicsDevice, LoginActionLayout layout, LoginArtworkSet artwork)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(artwork);
        _layout = layout;

        OpenGLTexture2D?[] textures = new OpenGLTexture2D?[4];

        try
        {
            textures[0] = Create(graphicsDevice, artwork.EnterUp);
            textures[1] = Create(graphicsDevice, artwork.ExitUp);
            textures[2] = Create(graphicsDevice, artwork.ExitHover);
            textures[3] = Create(graphicsDevice, artwork.ExitPressed);
            _textures = Array.ConvertAll(textures, static texture => texture!);
        }
        catch
        {
            foreach (OpenGLTexture2D? texture in textures)
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

    public void Draw(OpenGLRenderer renderer, LoginActionInput input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(input);

        // Never suggest that native Enter authentication is wired when it is not.
        DrawTexture(renderer, _textures[0], _layout.Enter, s_inactive);

        int state = input.Pressed == LoginActionButton.Exit && input.Hovered == LoginActionButton.Exit ? 2 :
            input.Hovered == LoginActionButton.Exit ? 1 : 0;
        DrawTexture(renderer, _textures[1 + state], _layout.Exit, SpriteColor.White);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ExceptionDispatchInfo? firstFailure = null;

        for (int index = _textures.Length - 1; index >= 0; index--)
        {
            try
            {
                _textures[index].Dispose();
            }
            catch (Exception exception) { firstFailure ??= ExceptionDispatchInfo.Capture(exception); }
        }

        firstFailure?.Throw();
    }

    private static OpenGLTexture2D Create(OpenGLGraphicsDevice graphicsDevice, OpenConquer.Content.Images.RgbaImage image)
    {
        if (image is not { Width: 132, Height: 40 })
        {
            throw new InvalidDataException("Retail login action artwork must be 132x40 pixels.");
        }

        return graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
    }

    private static void DrawTexture(OpenGLRenderer renderer, OpenGLTexture2D texture, LoginSpritePlacement bounds, SpriteColor tint) =>
        renderer.DrawSprite(texture, bounds.X, bounds.Y, bounds.Width, bounds.Height, tint);
}
