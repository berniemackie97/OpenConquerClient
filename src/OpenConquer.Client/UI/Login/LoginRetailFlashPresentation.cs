using System.Diagnostics;
using System.Runtime.ExceptionServices;
using OpenConquer.Content.Images;
using OpenConquer.Content.Startup;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;

namespace OpenConquer.Client.UI.Login;

/// <summary>GPU-owned retail Flash button artwork and independently looping original logo video.</summary>
internal sealed class LoginRetailFlashPresentation : IDisposable
{
    private readonly OpenGLTexture2D[] _buttons;
    private readonly OpenGLTexture2D[] _logo;
    private readonly LoginRetailFlashLayout _layout;
    private readonly long _started;
    private bool _disposed;

    public LoginRetailFlashPresentation(OpenGLGraphicsDevice graphics, LoginVisualLayout layout)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(layout);

        _layout = LoginRetailFlashLayout.Create(layout);
        OpenGLTexture2D?[] buttons = new OpenGLTexture2D?[6];
        OpenGLTexture2D?[] logo = new OpenGLTexture2D?[LoginRetailFlashArtwork.LogoFrameCount];
        try
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                RgbaImage image = LoginRetailFlashArtwork.LoadButton((LoginRetailFlashArtwork.Button)i,
                    LoginRetailFlashArtwork.State.Up);
                buttons[i] = graphics.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
            }
            for (int i = 0; i < logo.Length; i++)
            {
                RgbaImage image = LoginRetailFlashArtwork.LoadLogoFrame(i);
                logo[i] = graphics.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
            }
            _buttons = Array.ConvertAll(buttons, static texture => texture!);
            _logo = Array.ConvertAll(logo, static texture => texture!);
            _started = Stopwatch.GetTimestamp();
        }
        catch
        {
            foreach (OpenGLTexture2D? texture in logo)
                TryDispose(texture);
            foreach (OpenGLTexture2D? texture in buttons)
                TryDispose(texture);
            throw;
        }
    }

    public void Draw(OpenGLRenderer renderer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        // These are authentic UP artwork; interactions are intentionally not fabricated here.
        for (int i = 0; i < _buttons.Length; i++)
            Draw(renderer, _buttons[i], _layout.ButtonAt((LoginRetailFlashArtwork.Button)i));
        long framesElapsed = Stopwatch.GetElapsedTime(_started).Ticks * LoginRetailFlashArtwork.LogoFramesPerSecond / TimeSpan.TicksPerSecond;
        int frame = (int)(framesElapsed % _logo.Length);
        Draw(renderer, _logo[frame], _layout.Logo);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        ExceptionDispatchInfo? failure = null;
        for (int i = _logo.Length - 1; i >= 0; i--)
            Dispose(_logo[i], ref failure);
        for (int i = _buttons.Length - 1; i >= 0; i--)
            Dispose(_buttons[i], ref failure);
        failure?.Throw();
    }

    private static void Draw(OpenGLRenderer renderer, OpenGLTexture2D texture, LoginSpritePlacement p) =>
        renderer.DrawSprite(texture, p.X, p.Y, p.Width, p.Height);

    private static void Dispose(OpenGLTexture2D texture, ref ExceptionDispatchInfo? failure)
    {
        try
        {
            texture.Dispose();
        }
        catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
    }

    private static void TryDispose(OpenGLTexture2D? texture)
    {
        try
        {
            texture?.Dispose();
        }
        catch { /* Preserve the primary initialization exception. */ }
    }
}
