using System.Text;
using OpenConquer.Client.UI.Login.Controls;
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
    private readonly OpenGLTexture2D[,,] _buttonPictures = new OpenGLTexture2D[2, 2, 2];
    private readonly OpenGLTextContext _font;
    private string? _lastText;
    private GlyphPlacement[] _glyphs = [];
    private sealed record GlyphPlacement(OpenGLTextLayout Layout, int X, int Y);

    internal LoginErrorPresentation(OpenGLGraphicsDevice device, LoginStageLayout layout)
    {
        ArgumentNullException.ThrowIfNull(device);
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        try
        {
            _pictures[0] = Create(device, LoginErrorArtwork.Load(quit: false));
            _pictures[1] = Create(device, LoginErrorArtwork.Load(quit: true));
            for (int quit = 0; quit < 2; quit++)
            {
                foreach (LoginDialogButton button in Enum.GetValues<LoginDialogButton>())
                {
                    if (quit == 0 && button == LoginDialogButton.Cancel)
                    {
                        continue;
                    }
                    _buttonPictures[quit, (int)button, 0] = Create(device, LoginErrorArtwork.Load(quit != 0, button, LoginButtonState.Over));
                    _buttonPictures[quit, (int)button, 1] = Create(device, LoginErrorArtwork.Load(quit != 0, button, LoginButtonState.Down));
                }
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
                _glyphs = [];
            }
            return;
        }

        SpriteColor mask = LoginModalMask.At(dialog.Frame);
        LoginRect band = _layout.Place(dialog.IsLoadingQuit ? 0 : -1, 90, 1024, 570);
        if (dialog.Frame != 6)
        {
            renderer.DrawSolidRectangle(band.X, band.Y, band.Width, band.Height, mask);
            return;
        }

        int kind = dialog.IsQuit ? 1 : 0;
        OpenGLTexture2D picture = _pictures[kind];
        foreach (LoginDialogButton button in Enum.GetValues<LoginDialogButton>())
        {
            if (!dialog.IsQuit && button == LoginDialogButton.Cancel)
            {
                continue;
            }
            LoginButtonState state = dialog.State(button);
            if (state != LoginButtonState.Up)
            {
                picture = _buttonPictures[kind, (int)button, state == LoginButtonState.Over ? 0 : 1];
                break;
            }
        }
        (int x, int y) = LoginErrorArtwork.Position;
        LoginRect area = _layout.Place(x + (dialog.IsLoadingQuit ? 1 : 0), y, picture.Width, picture.Height);
        LoginModalMask.DrawAround(renderer, band, area, mask);
        renderer.DrawSprite(picture, area.X, area.Y, area.Width, area.Height);
        if (dialog.IsQuit)
        {
            return;
        }

        // Field273 includes a two-pixel gutter on each side; origin already includes that gutter.
        LoginRect text = _layout.PlaceExact(371.45, 290, 273.15, 109);
        if (!string.Equals(_lastText, dialog.Text, StringComparison.Ordinal))
        {
            _lastText = dialog.Text;
            List<GlyphPlacement> glyphs = [];
            Dictionary<Rune, OpenGLTextLayout> layouts = [];
            OpenGLTextLayout Glyph(Rune rune)
            {
                if (!layouts.TryGetValue(rune, out OpenGLTextLayout? glyph))
                {
                    glyph = _font.LayoutUnicode(rune.ToString());
                    layouts.Add(rune, glyph);
                }
                return glyph;
            }
            int Advance(Rune rune) => LoginErrorFontMetrics.TryGetAdvance(rune, out int advance) ? advance
                : (int)Math.Round(Glyph(rune).WidthPixels * 1024.0 / _layout.Width);
            int Measure(string value) => value.EnumerateRunes().Sum(Advance);
            IReadOnlyList<string> lines = LoginErrorTextLayout.Wrap(dialog.Text, 273, Measure);
            for (int line = 0; line < lines.Count; line++)
            {
                int xOffset = 0;
                foreach (Rune rune in lines[line].EnumerateRunes())
                {
                    int advance = Advance(rune);
                    if (advance > 0)
                    {
                        glyphs.Add(new(Glyph(rune), xOffset, line * LoginErrorFontMetrics.LineAdvance));
                    }
                    xOffset += advance;
                }
            }
            _glyphs = [.. glyphs];
        }

        using OpenGLRenderer.ClipScope clip = renderer.Clip(text.X, text.Y, text.Width, text.Height);
        foreach (GlyphPlacement glyph in _glyphs)
        {
            int top = text.Y + _layout.Scale(glyph.Y + LoginErrorFontMetrics.BaselineCorrection);
            if (top >= text.Y + text.Height)
            {
                break;
            }
            _font.Draw(renderer, glyph.Layout, s_style, text.X + _layout.Scale(glyph.X), top);
        }
    }

    public void Dispose() => _ownership.Dispose();

    private OpenGLTexture2D Create(OpenGLGraphicsDevice device, RgbaImage image) =>
        _ownership.Track(device.CreateTexture2D(image.Width, image.Height, image.Pixels.Span));
}
