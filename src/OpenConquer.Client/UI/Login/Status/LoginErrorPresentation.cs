using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Images;
using OpenConquer.Content.Login;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Client.UI.Login.Status;

/// <summary>Retail error/quit artwork, mask, buttons and error device-font field for one graphics context.</summary>
internal sealed class LoginErrorPresentation : IDisposable
{
    private static readonly SpriteColor s_red = new(255, 70, 66, 255);
    private static readonly NativeTextRenderOptions s_style = new(NativeTextRenderStyle.Normal,
        s_red, s_red, 0, 0, NativeTextVertexColors.Solid(s_red));
    private readonly ResourceOwnership _ownership = new();
    private readonly LoginStageLayout _layout;
    private readonly OpenGLTexture2D[] _pictures = new OpenGLTexture2D[2];
    private readonly OpenGLTexture2D[,] _buttons = new OpenGLTexture2D[2, 2];
    private readonly OpenGLTextContext _font;
    private string? _lastText;
    private OpenGLTextLayout[] _lines = [];

    internal LoginErrorPresentation(OpenGLGraphicsDevice device, LoginStageLayout layout)
    {
        ArgumentNullException.ThrowIfNull(device);
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        try
        {
            _pictures[0] = Create(device, LoginErrorArtwork.Load(quit: false));
            _pictures[1] = Create(device, LoginErrorArtwork.Load(quit: true));
            foreach (LoginDialogButton button in Enum.GetValues<LoginDialogButton>())
            {
                _buttons[(int)button, 0] = Create(device, LoginAccountArtwork.LoadDialogButton(button, LoginButtonState.Over));
                _buttons[(int)button, 1] = Create(device, LoginAccountArtwork.LoadDialogButton(button, LoginButtonState.Down));
            }

            _font = _ownership.Track(new OpenGLTextContext(device, "Arial", Math.Max(1, layout.Scale(14)), 0, true));
        }
        catch
        {
            _ownership.DisposeAfterInitializationFailure();
            throw;
        }
    }

    internal void Draw(OpenGLRenderer renderer, LoginErrorDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(dialog);
        if (!dialog.IsOpen)
        {
            if (!dialog.IsQuit)
            {
                _lastText = null;
                _lines = [];
            }
            return;
        }

        int alpha = LoginAccountArtwork.MaskAlpha(dialog.Frame) * 255 / 256;
        SpriteColor mask = new(0, 0, 0, (byte)alpha);
        LoginRect band = _layout.Place(-1, 90, 1024, 570);
        if (dialog.Frame != 6)
        {
            renderer.DrawSolidRectangle(band.X, band.Y, band.Width, band.Height, mask);
            return;
        }

        OpenGLTexture2D picture = _pictures[dialog.IsQuit ? 1 : 0];
        (int x, int y) = LoginErrorArtwork.Position;
        LoginRect area = _layout.Place(x, y, picture.Width, picture.Height);
        renderer.DrawSolidRectangle(band.X, band.Y, band.Width, area.Y - band.Y, mask);
        renderer.DrawSolidRectangle(band.X, area.Y + area.Height, band.Width, band.Y + band.Height - area.Y - area.Height, mask);
        renderer.DrawSolidRectangle(band.X, area.Y, area.X - band.X, area.Height, mask);
        renderer.DrawSolidRectangle(area.X + area.Width, area.Y, band.X + band.Width - area.X - area.Width, area.Height, mask);
        renderer.DrawSprite(picture, area.X, area.Y, area.Width, area.Height);
        DrawButton(renderer, dialog, LoginDialogButton.Ok);
        if (dialog.IsQuit)
        {
            DrawButton(renderer, dialog, LoginDialogButton.Cancel);
            return;
        }

        // Field273 includes a two-pixel gutter on each side; origin already includes that gutter.
        LoginRect text = _layout.PlaceExact(371.45, 290, 273.15, 109);
        if (!string.Equals(_lastText, dialog.Text, StringComparison.Ordinal))
        {
            _lastText = dialog.Text;
            _lines = [.. LoginErrorTextLayout.Wrap(dialog.Text, Math.Max(1, text.Width),
                value => _font.LayoutUnicode(value).WidthPixels).Select(value => _font.LayoutUnicode(value))];
        }

        using OpenGLRenderer.ClipScope clip = renderer.Clip(text.X, text.Y, text.Width, text.Height);
        for (int line = 0; line < _lines.Length; line++)
        {
            int top = text.Y + _layout.Scale(line * 16);
            if (top >= text.Y + text.Height)
            {
                break;
            }

            _font.Draw(renderer, _lines[line], s_style, text.X, top);
        }
    }

    public void Dispose() => _ownership.Dispose();

    private void DrawButton(OpenGLRenderer renderer, LoginErrorDialog dialog, LoginDialogButton button)
    {
        LoginButtonState state = dialog.State(button);
        if (state == LoginButtonState.Up)
        {
            return;
        }

        LoginRect bounds = dialog.Bounds(button);
        renderer.DrawSprite(_buttons[(int)button, state == LoginButtonState.Down ? 1 : 0],
            bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private OpenGLTexture2D Create(OpenGLGraphicsDevice device, RgbaImage image) =>
        _ownership.Track(device.CreateTexture2D(image.Width, image.Height, image.Pixels.Span));
}
