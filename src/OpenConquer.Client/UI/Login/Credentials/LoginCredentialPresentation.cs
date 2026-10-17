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
    private static readonly SpriteColor s_text = new(0x90, 0x8B, 0x37, 255);
    private static readonly SpriteColor s_selection = new(90, 117, 158, 150);
    private static readonly NativeTextRenderOptions s_style = new(NativeTextRenderStyle.Normal, s_text, s_text, 0, 0, NativeTextVertexColors.Solid(s_text));
    private readonly OpenGLGraphicsDevice _graphics;
    private readonly LoginCredentialLayout _layout;
    private readonly int _fontHeight;
    private readonly int _padding;
    private OpenGLTextContext? _font;
    private Line? _account;
    private Line? _password;
    private int _accountStart;
    private int _passwordStart;
    private string _accountValue = string.Empty;
    private string _passwordValue = string.Empty;
    private int[] _accountAdvances = [0];
    private int[] _passwordAdvances = [0];
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
        _padding = Math.Max(1, layout.Scale(2));
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
        EnsureLayout(input);
        DrawLine(renderer, _account, input.Focus == LoginCredentialFocus.Account, input);
        DrawLine(renderer, _password, input.Focus == LoginCredentialFocus.Password, input);
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
        Line? line = _layout.AccountField.Contains(x, y) ? _account : _layout.PasswordField.Contains(x, y) ? _password : null;
        if (line is null)
        {
            return false;
        }

        caret = HitTest(line, x);
        return true;
    }

    /// <summary>Drag-selection is captured to the originally focused field even beyond its visible left/right edges.</summary>
    public int LocateWhileDragging(LoginCredentialInput input, int x)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(input);
        EnsureLayout(input);
        Line? line = input.Focus == LoginCredentialFocus.Account ? _account : input.Focus == LoginCredentialFocus.Password ? _password : null;
        if (line is null)
        {
            return 0;
        }

        if (x < line.Field.X)
        {
            return Math.Max(0, line.Start - 1);
        }

        if (x >= line.Field.X + line.Field.Width)
        {
            return Math.Min(input.Focus == LoginCredentialFocus.Account ? input.AccountLength : input.PasswordLength, line.End + 1);
        }

        return HitTest(line, x);
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
        if (contentChanged)
        {
            _accountValue = input.AccountDisplay;
            _passwordValue = input.PasswordMask;
            _accountAdvances = Measure(font, _accountValue);
            _passwordAdvances = Measure(font, _passwordValue);
            _lastContentRevision = input.ContentRevision;
        }
        _account = BuildLine(font, _accountValue, _accountAdvances, _layout.AccountField,
            input.Focus == LoginCredentialFocus.Account ? input.CaretIndex : -1, contentChanged ? null : _account, ref _accountStart);
        _password = BuildLine(font, _passwordValue, _passwordAdvances, _layout.PasswordField,
            input.Focus == LoginCredentialFocus.Password ? input.CaretIndex : -1, contentChanged ? null : _password, ref _passwordStart);
        _lastRevision = input.Revision;
        _blinkStarted = Stopwatch.GetTimestamp();
    }

    private static int[] Measure(OpenGLTextContext font, string value)
    {
        int[] advances = new int[value.Length + 1];
        for (int length = 1; length <= value.Length; length++)
        {
            advances[length] = font.LayoutUnicode(value.AsSpan(0, length)).WidthPixels;
        }

        return advances;
    }

    private Line BuildLine(OpenGLTextContext font, string value, int[] advances, LoginRect field,
        int caret, Line? previous, ref int scrollStart)
    {
        int usableWidth = Math.Max(1, field.Width - 2 * _padding);
        int start = Math.Clamp(scrollStart, 0, value.Length);
        int target = caret < 0 ? value.Length : Math.Clamp(caret, 0, value.Length);
        if (target < start)
        {
            start = target;
        }

        while (start < target && advances[target] - advances[start] > usableWidth)
        {
            start++;
        }

        while (start > 0 && advances[value.Length] - advances[start - 1] <= usableWidth)
        {
            start--;
        }

        int end = start;
        while (end < value.Length && advances[end + 1] - advances[start] <= usableWidth)
        {
            end++;
        }

        scrollStart = start;
        if (previous is { } retained && retained.Start == start && retained.End == end && retained.Field == field)
        {
            return retained;
        }

        string visible = value[start..end];
        OpenGLTextLayout? text = visible.Length == 0 ? null : font.LayoutUnicode(visible);
        int[] boundaries = new int[visible.Length + 1];
        // NativeTextLayoutEngine advances each glyph without kerning; prefix deltas are exact.
        for (int index = 1; index < boundaries.Length; index++)
        {
            boundaries[index] = advances[start + index] - advances[start];
        }

        return new Line(field, start, end, field.X + _padding, field.Y + _padding, text, boundaries);
    }

    private void DrawLine(OpenGLRenderer renderer, Line? line, bool focused, LoginCredentialInput input)
    {
        if (line is null)
        {
            return;
        }

        using OpenGLRenderer.ClipScope clip = renderer.Clip(line.Field.X + 1, line.Field.Y + 1,
            line.Field.Width - 2, line.Field.Height - 2);
        if (focused && input.HasSelection)
        {
            int left = line.X + Boundary(line, input.SelectionStart);
            int right = line.X + Boundary(line, input.SelectionEnd);
            if (right > left)
            {
                renderer.DrawSolidRectangle(left, line.Field.Y + 2, right - left, Math.Max(1, line.Field.Height - 4), s_selection);
            }
        }
        if (line.Text is not null && !line.Text.HasMissingGlyphs)
        {
            GetFont().Draw(renderer, line.Text, s_style, line.X, line.Y);
        }

        if (focused && Stopwatch.GetElapsedTime(_blinkStarted).TotalMilliseconds % 1000 < 530)
        {
            int x = line.X + Boundary(line, input.CaretIndex);
            renderer.DrawSolidRectangle(Math.Clamp(x, line.Field.X + 1, line.Field.X + line.Field.Width - 2),
                line.Field.Y + 2, 1, Math.Max(1, line.Field.Height - 4), s_text);
        }
    }

    private static int Boundary(Line line, int index) => line.Boundaries[Math.Clamp(index - line.Start, 0, line.Boundaries.Length - 1)];

    private static int HitTest(Line line, int x)
    {
        int relative = x - line.X;
        ReadOnlySpan<int> boundaries = line.Boundaries;
        if (relative <= 0)
        {
            return line.Start;
        }

        for (int index = 1; index < boundaries.Length; index++)
        {
            if (relative < (boundaries[index - 1] + boundaries[index]) / 2)
            {
                return line.Start + index - 1;
            }
        }

        return line.End;
    }

    private void ReleaseFont()
    {
        OpenGLTextContext? font = _font;
        _font = null;
        _account = null;
        _password = null;
        _accountStart = _passwordStart = 0;
        _accountValue = _passwordValue = string.Empty;
        _accountAdvances = _passwordAdvances = [0];
        _lastContentRevision = -1;
        _lastRevision = -1;
        font?.Dispose();
    }



    private sealed record Line(LoginRect Field, int Start, int End, int X, int Y, OpenGLTextLayout? Text, int[] Boundaries);
}
