using System.Diagnostics;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Client.UI.Login.Credentials;

/// <summary>
/// Measures visible, whole ASCII glyphs for editing, selection and hit testing in the retail fields:
/// device Arial 14, color #908B37, text area inside each field's 2-pixel gutter.
/// </summary>
internal sealed class LoginCredentialPresentation : ILoginCaretLocator, IDisposable
{
    internal static readonly SpriteColor TextColor = new(0x90, 0x8B, 0x37, 255);
    internal static readonly SpriteColor SelectionColor = new(90, 117, 158, 150);
    internal static readonly NativeTextRenderOptions TextStyle = new(NativeTextRenderStyle.Normal, TextColor, TextColor, 0, 0, NativeTextVertexColors.Solid(TextColor));

    private readonly OpenGLGraphicsDevice _graphics;
    private readonly LoginCredentialLayout _layout;
    private readonly int _fontHeight;
    private readonly LoginTextFieldView _account;
    private readonly LoginTextFieldView _password;
    private OpenGLTextContext? _font;
    private int _lastContentRevision = -1;
    private int _lastRevision = -1;
    private long _blinkStarted;
    private bool _disposed;

    public LoginCredentialPresentation(OpenGLGraphicsDevice graphics, LoginStageLayout layout)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(layout);
        _graphics = graphics;
        _layout = LoginCredentialLayout.Create(layout);
        _fontHeight = Math.Max(1, layout.Scale(14));
        int padding = Math.Max(1, layout.Scale(2));
        _account = new LoginTextFieldView(padding);
        _password = new LoginTextFieldView(padding);
    }

    /// <summary>The caret blinks 530 ms on, 470 ms off, restarting whenever the caret or text changes.</summary>
    internal static bool CaretVisible(long blinkStarted) => Stopwatch.GetElapsedTime(blinkStarted).TotalMilliseconds % 1000 < 530;

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
        EnsureLayout(input);
        OpenGLTextContext font = GetFont();
        bool caretVisible = CaretVisible(_blinkStarted);
        _account.Draw(renderer, font, TextStyle, TextColor, SelectionColor, State(input, LoginCredentialFocus.Account, caretVisible));
        _password.Draw(renderer, font, TextStyle, TextColor, SelectionColor, State(input, LoginCredentialFocus.Password, caretVisible));
    }

    /// <summary>Returns the nearest measured caret boundary for a press within either input field.</summary>
    public bool TryLocate(LoginCredentialInput input, int x, int y, out int caret)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(input);
        caret = 0;
        if (!input.Enabled)
        {
            return false;
        }

        EnsureLayout(input);
        LoginTextFieldView? view = _layout.AccountField.Contains(x, y) ? _account : _layout.PasswordField.Contains(x, y) ? _password : null;
        if (view is null)
        {
            return false;
        }

        caret = view.Locate(x);
        return true;
    }

    /// <summary>Drag-selection is captured to the originally focused field even beyond its visible left/right edges.</summary>
    public int LocateWhileDragging(LoginCredentialInput input, int x)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(input);
        EnsureLayout(input);
        return input.Focus switch
        {
            LoginCredentialFocus.Account => _account.LocateWhileDragging(x, input.AccountLength),
            LoginCredentialFocus.Password => _password.LocateWhileDragging(x, input.PasswordLength),
            _ => 0,
        };
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

    private static LoginTextFieldState State(LoginCredentialInput input, LoginCredentialFocus field, bool caretVisible) =>
        new(input.Focus == field, input.CaretIndex, input.SelectionStart, input.SelectionEnd, caretVisible);

    private OpenGLTextContext GetFont() => _font ??= new OpenGLTextContext(_graphics, fontToken: "Arial",
        nominalPixelHeight: _fontHeight, effectiveCodePage: 0, antialiasEnabled: true);

    private void EnsureLayout(LoginCredentialInput input)
    {
        if (!input.Enabled || _lastRevision == input.Revision)
        {
            return;
        }

        OpenGLTextContext font = GetFont();
        bool contentChanged = _lastContentRevision != input.ContentRevision;
        _account.Update(font, _layout.AccountField, contentChanged ? input.AccountDisplay : null,
            input.Focus == LoginCredentialFocus.Account ? input.CaretIndex : -1);
        _password.Update(font, _layout.PasswordField, contentChanged ? input.PasswordMask : null,
            input.Focus == LoginCredentialFocus.Password ? input.CaretIndex : -1);
        _lastContentRevision = input.ContentRevision;
        _lastRevision = input.Revision;
        _blinkStarted = Stopwatch.GetTimestamp();
    }

    private void ReleaseFont()
    {
        OpenGLTextContext? font = _font;
        _font = null;
        _account.Reset();
        _password.Reset();
        _lastContentRevision = -1;
        _lastRevision = -1;
        font?.Dispose();
    }
}
