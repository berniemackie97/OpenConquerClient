using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Client.UI.Login;

/// <summary>Places account text and password masks within the original retail input artwork.</summary>
internal sealed class LoginCredentialPresentation : IDisposable
{
    private const int ReferenceWidth = 1024;
    private static readonly SpriteColor s_text = new(245, 230, 194, 255);
    private static readonly SpriteColor s_caret = new(255, 231, 135, 255);
    private static readonly NativeTextRenderOptions s_style = new(NativeTextRenderStyle.Normal, s_text, s_text, 0, 0, NativeTextVertexColors.Solid(s_text));
    private readonly OpenGLGraphicsDevice _graphics;
    private readonly LoginCredentialLayout _layout;
    private readonly int _fontHeight;
    private readonly int _padding;
    private OpenGLTextContext? _font;
    private Label? _account;
    private Label? _password;
    private int _lastRevision = -1;
    private bool _disposed;

    public LoginCredentialPresentation(OpenGLGraphicsDevice graphics, LoginVisualLayout layout)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(layout);
        _graphics = graphics;
        _layout = LoginCredentialLayout.Create(layout);
        _fontHeight = Math.Max(1, Scale(12, layout.Width));
        _padding = Math.Max(1, Scale(4, layout.Width));
    }

    public void Draw(OpenGLRenderer renderer, LoginCredentialInput input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(input);

        if (!input.Enabled)
        {
            ReleaseFont();
            return;
        }

        OpenGLTextContext text = _font ??= new OpenGLTextContext(_graphics, fontToken: null, nominalPixelHeight: _fontHeight, effectiveCodePage: 0, antialiasEnabled: true);

        if (_lastRevision != input.Revision)
        {
            _account = FitText(text, input.AccountDisplay, _layout.AccountField);
            _password = FitText(text, input.PasswordMask, _layout.PasswordField);
            _lastRevision = input.Revision;
        }

        // The original 508x98 panel already contains its captions, field backgrounds, and frames.
        // Redrawing them here would obscure the retail image and introduce duplicate labels.
        DrawText(text, renderer, _account);
        DrawText(text, renderer, _password);

        if (input.Focus == LoginCredentialFocus.Account)
        {
            DrawCaret(renderer, _layout.AccountField, _account);
        }
        else if (input.Focus == LoginCredentialFocus.Password)
        {
            DrawCaret(renderer, _layout.PasswordField, _password);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ReleaseFont();
    }

    private Label? FitText(OpenGLTextContext font, string value, LoginSpritePlacement field)
    {
        if (value.Length == 0)
        {
            return null;
        }

        OpenGLTextLayout full = font.LayoutUnicode(value);
        if (TryPosition(full, field, out int x, out int y))
        {
            return new Label(full, x, y);
        }

        // The credential input already validates every character as individual ASCII.
        for (int start = 1; start < value.Length; start++)
        {
            OpenGLTextLayout shortened = font.LayoutUnicode("..." + value[start..]);
            if (TryPosition(shortened, field, out x, out y))
            {
                return new Label(shortened, x, y);
            }
        }

        return null;
    }

    private bool TryPosition(OpenGLTextLayout text, LoginSpritePlacement field, out int x, out int y)
    {
        x = y = 0;

        if (text.HasMissingGlyphs || !text.TryGetInkBounds(out int left, out int top, out int right, out int bottom) ||
            (long)right - left > (long)field.Width - 2L * _padding || bottom - top > field.Height)
        {
            return false;
        }

        x = checked(field.X + _padding - left);
        y = checked(field.Y + (field.Height - (bottom - top)) / 2 - top);
        return true;
    }

    private static void DrawText(OpenGLTextContext font, OpenGLRenderer renderer, Label? item)
    {
        if (item is { } label)
        {
            font.Draw(renderer, label.Text, s_style, label.X, label.Y);
        }
    }

    private void DrawCaret(OpenGLRenderer renderer, LoginSpritePlacement field, Label? item)
    {
        int x = checked(field.X + _padding);

        if (item is { } label && label.Text.TryGetInkBounds(out _, out _, out int right, out _))
        {
            x = checked(label.X + right + 1);
        }

        int minimum = field.X + 1;
        int maximum = checked(field.X + field.Width - 2);
        int y = field.Y + 2;
        int height = field.Height - 4;

        renderer.DrawSolidRectangle(Math.Clamp(x, minimum, maximum), y, 1, height, s_caret);
    }

    private static int Scale(int referenceValue, int logicalWidth) => checked((int)(((long)referenceValue * logicalWidth + ReferenceWidth / 2) / ReferenceWidth));

    private void ReleaseFont()
    {
        OpenGLTextContext? font = _font;
        _font = null;
        _account = null;
        _password = null;
        _lastRevision = -1;
        font?.Dispose();
    }

    private readonly record struct Label(OpenGLTextLayout Text, int X, int Y);
}
