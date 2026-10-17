using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Images;
using OpenConquer.Content.Login;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;

namespace OpenConquer.Client.UI.Login.Controls;

/// <summary>The Up, Over and Down textures of every login button, owned by one OpenGL context.</summary>
internal sealed class LoginButtonPresentation : IDisposable
{
    private readonly ResourceOwnership _ownership = new();
    private readonly OpenGLTexture2D[,] _textures;

    public LoginButtonPresentation(OpenGLGraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        LoginButton[] buttons = Enum.GetValues<LoginButton>();
        LoginButtonState[] states = Enum.GetValues<LoginButtonState>();
        _textures = new OpenGLTexture2D[buttons.Length, states.Length];

        try
        {
            foreach (LoginButton button in buttons)
            {
                foreach (LoginButtonState state in states)
                {
                    RgbaImage image = LoginButtonArtwork.Load(button, state);
                    _textures[(int)button, (int)state] = _ownership.Track(graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span));
                }
            }
        }
        catch
        {
            _ownership.DisposeAfterInitializationFailure();
            throw;
        }
    }

    public void Draw(OpenGLRenderer renderer, LoginButton button, LoginButtonState state, LoginRect bounds)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.DrawSprite(_textures[(int)button, (int)state], bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    public void Dispose() => _ownership.Dispose();
}
