using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Client.UI.Login;

/// <summary>Draws ephemeral account text and password-length masks without ever formatting a password.</summary>
internal sealed class LoginCredentialPresentation : IDisposable
{
    private const int ReferenceWidth = 1024;
    private static readonly SpriteColor s_text = new(245, 230, 194, 255);
    private static readonly SpriteColor s_border = new(148, 123, 82, 255);
    private static readonly SpriteColor s_focused = new(255, 231, 135, 255);
    private static readonly NativeTextRenderOptions s_style = new(NativeTextRenderStyle.Normal, s_text, s_text, 0, 0, NativeTextVertexColors.Solid(s_text));
    private readonly OpenGLGraphicsDevice _graphics;
    private readonly LoginCredentialLayout _layout;
    private readonly int _fontHeight;
    private readonly int _padding;
    private readonly int _titleGap;
    private OpenGLTextContext? _font;
    private Label? _account;
    private Label? _password;
    private Label? _accountTitle;
    private Label? _passwordTitle;
    private int _lastRevision = -1;
    private bool _disposed;

    public LoginCredentialPresentation(OpenGLGraphicsDevice graphics, LoginVisualLayout layout)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(layout);
        _graphics = graphics;
        _layout = LoginCredentialLayout.Create(layout);
        _fontHeight = Math.Max(1, Scale(12, layout.Width));
        _padding = Math.Max(1, Scale(5, layout.Width));
        _titleGap = Math.Max(1, Scale(12, layout.Width));
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
            _accountTitle ??= FitTitle(text, "Account", _layout.AccountField);
            _passwordTitle ??= FitTitle(text, "Password", _layout.PasswordField);
            _lastRevision = input.Revision;
        }

        DrawBorder(renderer, _layout.AccountField, input.Focus == LoginCredentialFocus.Account ? s_focused : s_border);
        DrawBorder(renderer, _layout.PasswordField, input.Focus == LoginCredentialFocus.Password ? s_focused : s_border);
        DrawText(text, renderer, _accountTitle);
        DrawText(text, renderer, _passwordTitle);
        DrawText(text, renderer, _account);
        DrawText(text, renderer, _password);
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

        // Both inputs contain individual, validated ASCII code units; every suffix boundary is safe.
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

    private Label? FitTitle(OpenGLTextContext font, string value, LoginSpritePlacement field)
    {
        OpenGLTextLayout layout = font.LayoutUnicode(value);
        if (layout.HasMissingGlyphs || !layout.TryGetInkBounds(out int left, out int top, out int right, out int bottom))
        {
            return null;
        }

        int x = checked(field.X - _titleGap - (right - left) - left);
        int y = checked(field.Y + (field.Height - (bottom - top)) / 2 - top);
        return new Label(layout, x, y);
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

    private static void DrawBorder(OpenGLRenderer renderer, LoginSpritePlacement field, SpriteColor color) =>
        renderer.DrawLineRectangle(field.X, field.Y, field.X + field.Width - 1, field.Y + field.Height - 1, color);

    private static int Scale(int referenceValue, int logicalWidth) => checked((int)(((long)referenceValue * logicalWidth + ReferenceWidth / 2) / ReferenceWidth));

    private void ReleaseFont()
    {
        OpenGLTextContext? font = _font;
        _font = null;
        _account = null;
        _password = null;
        _accountTitle = null;
        _passwordTitle = null;
        _lastRevision = -1;
        font?.Dispose();
    }

    private readonly record struct Label(OpenGLTextLayout Text, int X, int Y);
}
