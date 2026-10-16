using System.Diagnostics;
using System.Runtime.ExceptionServices;
using OpenConquer.Content.Images;
using OpenConquer.Content.Startup;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;

namespace OpenConquer.Client.UI.Login;

/// <summary>One-context owner of authentic 5517 button states, logo frames and account UI.</summary>
internal sealed class LoginRetailFlashPresentation : IDisposable
{
    private const int StateCount = 3;
    private readonly OpenGLTexture2D[] _buttons;
    private readonly OpenGLTexture2D[] _logo;
    private readonly LoginRetailPanelPresentation _panel;
    private readonly LoginRetailFlashLayout _layout;
    private readonly long _started;
    private bool _disposed;

    public LoginRetailFlashPresentation(OpenGLGraphicsDevice graphics, LoginVisualLayout layout)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(layout);
        _layout = LoginRetailFlashLayout.Create(layout);
        OpenGLTexture2D?[] buttons = new OpenGLTexture2D?[6 * StateCount];
        OpenGLTexture2D?[] logo = new OpenGLTexture2D?[LoginRetailFlashArtwork.LogoFrameCount];
        LoginRetailPanelPresentation? panel = null;
        try
        {
            for (int button = 0; button < 6; button++)
                for (int state = 0; state < StateCount; state++)
                {
                    RgbaImage image = LoginRetailFlashArtwork.LoadButton((LoginRetailFlashArtwork.Button)button,
                        (LoginRetailFlashArtwork.State)state);
                    buttons[button * StateCount + state] = graphics.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
                }
            for (int index = 0; index < logo.Length; index++)
            {
                RgbaImage image = LoginRetailFlashArtwork.LoadLogoFrame(index);
                logo[index] = graphics.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
            }
            panel = new LoginRetailPanelPresentation(graphics, layout.Width);
            _buttons = Array.ConvertAll(buttons, static texture => texture!);
            _logo = Array.ConvertAll(logo, static texture => texture!);
            _panel = panel;
            _started = Stopwatch.GetTimestamp();
        }
        catch
        {
            try
            {
                panel?.Dispose();
            }
            catch { }
            foreach (OpenGLTexture2D? texture in logo)
                TryDispose(texture);
            foreach (OpenGLTexture2D? texture in buttons)
                TryDispose(texture);
            throw;
        }
    }

    public void Draw(OpenGLRenderer renderer, LoginRetailPanelControls controls)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(controls);
        for (int index = 0; index < 6; index++)
        {
            LoginRetailFlashArtwork.Button button = (LoginRetailFlashArtwork.Button)index;
            int state = (int)controls.ButtonState(button);
            Draw(renderer, _buttons[index * StateCount + state], _layout.ButtonAt(button));
        }
        long framesElapsed = Stopwatch.GetElapsedTime(_started).Ticks * LoginRetailFlashArtwork.LogoFramesPerSecond / TimeSpan.TicksPerSecond;
        Draw(renderer, _logo[(int)(framesElapsed % _logo.Length)], _layout.Logo);
        _panel.Draw(renderer, controls);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        ExceptionDispatchInfo? failure = null;
        try
        {
            _panel.Dispose();
        }
        catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); }
        for (int index = _logo.Length - 1; index >= 0; index--)
            DisposeTexture(_logo[index], ref failure);
        for (int index = _buttons.Length - 1; index >= 0; index--)
            DisposeTexture(_buttons[index], ref failure);
        failure?.Throw();
    }

    private static void Draw(OpenGLRenderer renderer, OpenGLTexture2D texture, LoginSpritePlacement rect) =>
        renderer.DrawSprite(texture, rect.X, rect.Y, rect.Width, rect.Height);

    private static void DisposeTexture(OpenGLTexture2D texture, ref ExceptionDispatchInfo? failure)
    {
        try
        {
            texture.Dispose();
        }
        catch (Exception ex) { failure ??= ExceptionDispatchInfo.Capture(ex); }
    }

    private static void TryDispose(OpenGLTexture2D? texture)
    {
        try
        {
            texture?.Dispose();
        }
        catch { }
    }
}
