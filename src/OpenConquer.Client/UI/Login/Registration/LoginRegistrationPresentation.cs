using System.Diagnostics;
using OpenConquer.Client.UI.Login.Controls;
using OpenConquer.Client.UI.Login.Credentials;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Content.Images;
using OpenConquer.Content.Login;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Client.UI.Login.Registration;

/// <summary>
/// Draws the Sign Up dialog for one OpenGL context. Labels, title, guidance and field text use the account
/// panel's device Arial 14 and field colour (#908B37); errors use the native dialogs' #FF4642.
/// </summary>
internal sealed class LoginRegistrationPresentation : ILoginRegistrationCaretLocator, IDisposable
{
    private const int FontHeight = 14;
    private const int MaximumMessageLines = 2;
    private static readonly SpriteColor s_error = new(0xFF, 0x46, 0x42, 255);
    private static readonly NativeTextRenderOptions s_errorStyle = new(NativeTextRenderStyle.Normal, s_error, s_error, 0, 0,
        NativeTextVertexColors.Solid(s_error));

    private readonly ResourceOwnership _ownership = new();
    private readonly LoginRegistrationLayout _layout;
    private readonly OpenGLTexture2D _dialog;
    private readonly OpenGLTexture2D _box;
    private readonly OpenGLTexture2D[,] _buttons = new OpenGLTexture2D[2, 2];
    private readonly OpenGLTextContext _font;
    private readonly OpenGLTextLayout _title;
    private readonly OpenGLTextLayout[] _labels;
    private readonly LoginTextFieldView[] _views;
    private string? _message;
    private OpenGLTextLayout[] _messageLines = [];
    private int _lastRevision = -1;
    private int _lastContentRevision = -1;
    private long _blinkStarted;

    public LoginRegistrationPresentation(OpenGLGraphicsDevice graphicsDevice, LoginRegistrationLayout layout)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));

        try
        {
            _dialog = Create(graphicsDevice, LoginRegistrationArtwork.LoadDialog());
            _box = Create(graphicsDevice, LoginRegistrationArtwork.LoadField());
            foreach (LoginDialogButton button in Enum.GetValues<LoginDialogButton>())
            {
                _buttons[(int)button, 0] = Create(graphicsDevice, LoginAccountArtwork.LoadDialogButton(button, LoginButtonState.Over));
                _buttons[(int)button, 1] = Create(graphicsDevice, LoginAccountArtwork.LoadDialogButton(button, LoginButtonState.Down));
            }

            _font = _ownership.Track(new OpenGLTextContext(graphicsDevice, "Arial", Math.Max(1, layout.Scale(FontHeight)),
                effectiveCodePage: 0, antialiasEnabled: true));
            _title = _font.LayoutUnicode(LoginRegistrationText.Title);
            _labels =
            [
                _font.LayoutUnicode(LoginRegistrationText.AccountLabel),
                _font.LayoutUnicode(LoginRegistrationText.PasswordLabel),
                _font.LayoutUnicode(LoginRegistrationText.ConfirmLabel),
            ];

            int padding = Math.Max(1, layout.Scale(2));
            _views = [new LoginTextFieldView(padding), new LoginTextFieldView(padding), new LoginTextFieldView(padding)];
        }
        catch
        {
            _ownership.DisposeAfterInitializationFailure();
            throw;
        }
    }

    public void Draw(OpenGLRenderer renderer, LoginRegistrationForm form)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(form);
        if (!form.IsOpen)
        {
            return;
        }

        SpriteColor mask = LoginModalMask.At(form.Frame);
        if (!form.IsHolding)
        {
            if (mask.Alpha > 0)
            {
                renderer.DrawSolidRectangle(_layout.Mask.X, _layout.Mask.Y, _layout.Mask.Width, _layout.Mask.Height, mask);
            }

            return;
        }

        LoginRect dialog = _layout.Dialog;
        LoginModalMask.DrawAround(renderer, _layout.Mask, dialog, mask);
        renderer.DrawSprite(_dialog, dialog.X, dialog.Y, dialog.Width, dialog.Height);
        DrawCentred(renderer, _title, LoginCredentialPresentation.TextStyle, _layout.Title);

        EnsureLayout(form);
        bool caretVisible = LoginCredentialPresentation.CaretVisible(_blinkStarted);
        foreach (LoginRegistrationField field in Enum.GetValues<LoginRegistrationField>())
        {
            LoginRect box = _layout.Box(field);
            renderer.DrawSprite(_box, box.X, box.Y, box.Width, box.Height);

            // Labels sit on the field's text line, inside the same 2-pixel gutter.
            LoginRect label = _layout.Label(field);
            OpenGLTextLayout text = _labels[(int)field];
            if (!text.HasMissingGlyphs)
            {
                _font.Draw(renderer, text, LoginCredentialPresentation.TextStyle, label.X + label.Width - text.WidthPixels,
                    label.Y + _layout.Scale(2));
            }

            LoginTextField entry = form.Field(field);
            _views[(int)field].Draw(renderer, _font, LoginCredentialPresentation.TextStyle, LoginCredentialPresentation.TextColor,
                LoginCredentialPresentation.SelectionColor,
                new LoginTextFieldState(form.Focus == field, entry.CaretIndex, entry.SelectionStart, entry.SelectionEnd, caretVisible));
        }

        DrawMessage(renderer, form);
        DrawButton(renderer, form, LoginDialogButton.Ok, _layout.Ok);
        DrawButton(renderer, form, LoginDialogButton.Cancel, _layout.Cancel);
    }

    public bool TryLocate(LoginRegistrationForm form, LoginRegistrationField field, int x, out int caret)
    {
        ArgumentNullException.ThrowIfNull(form);
        EnsureLayout(form);
        caret = _views[(int)field].Locate(x);
        return true;
    }

    public int LocateWhileDragging(LoginRegistrationForm form, int x)
    {
        ArgumentNullException.ThrowIfNull(form);
        EnsureLayout(form);
        return form.Focus is { } field ? _views[(int)field].LocateWhileDragging(x, form.Field(field).Length) : 0;
    }

    public void Dispose() => _ownership.Dispose();

    private void EnsureLayout(LoginRegistrationForm form)
    {
        if (_lastRevision == form.Revision)
        {
            return;
        }

        bool contentChanged = _lastContentRevision != form.ContentRevision;
        foreach (LoginRegistrationField field in Enum.GetValues<LoginRegistrationField>())
        {
            LoginTextField entry = form.Field(field);
            _views[(int)field].Update(_font, _layout.Field(field), contentChanged ? entry.Display : null,
                form.Focus == field ? entry.CaretIndex : -1);
        }

        _lastContentRevision = form.ContentRevision;
        _lastRevision = form.Revision;
        _blinkStarted = Stopwatch.GetTimestamp();
    }

    private void DrawMessage(OpenGLRenderer renderer, LoginRegistrationForm form)
    {
        LoginRect area = _layout.Message;
        if (!string.Equals(_message, form.Message, StringComparison.Ordinal))
        {
            _message = form.Message;
            IReadOnlyList<string> lines = LoginErrorTextLayout.Wrap(form.Message, area.Width,
                line => _font.LayoutUnicode(line).WidthPixels);
            // Wrapped lines keep the space they broke at, which would pull centred text off centre.
            _messageLines = [.. lines.Select(static line => line.TrimEnd()).Where(static line => line.Length > 0)
                .Take(MaximumMessageLines).Select(line => _font.LayoutUnicode(line))];
        }

        if (_messageLines.Length == 0)
        {
            return;
        }

        NativeTextRenderOptions style = form.MessageIsError ? s_errorStyle : LoginCredentialPresentation.TextStyle;
        int advance = _layout.Scale(LoginErrorFontMetrics.LineAdvance);
        int top = area.Y + (area.Height - advance * _messageLines.Length) / 2;
        for (int line = 0; line < _messageLines.Length; line++)
        {
            DrawCentred(renderer, _messageLines[line], style, new LoginRect(area.X, top + line * advance, area.Width, advance));
        }
    }

    private void DrawCentred(OpenGLRenderer renderer, OpenGLTextLayout text, NativeTextRenderOptions style, LoginRect area)
    {
        if (!text.HasMissingGlyphs)
        {
            _font.Draw(renderer, text, style, area.X + (area.Width - text.WidthPixels) / 2, area.Y + (area.Height - text.HeightPixels) / 2);
        }
    }

    private void DrawButton(OpenGLRenderer renderer, LoginRegistrationForm form, LoginDialogButton button, LoginRect bounds)
    {
        // The Up states are part of the dialog picture.
        LoginButtonState state = form.ButtonState(button);
        if (state != LoginButtonState.Up)
        {
            OpenGLTexture2D texture = _buttons[(int)button, state == LoginButtonState.Down ? 1 : 0];
            renderer.DrawSprite(texture, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }
    }

    private OpenGLTexture2D Create(OpenGLGraphicsDevice graphicsDevice, RgbaImage image) =>
        _ownership.Track(graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span));
}
