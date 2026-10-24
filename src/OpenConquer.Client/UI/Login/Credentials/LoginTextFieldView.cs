using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Client.UI.Login.Credentials;

/// <summary>How one edit field is drawn this frame.</summary>
internal readonly record struct LoginTextFieldState(bool Focused, int Caret, int SelectionStart, int SelectionEnd, bool CaretVisible);

/// <summary>
/// The visible run of one edit field: the whole glyphs that fit inside its gutter, scrolled to keep the caret in
/// view, with the selection and caret drawn over them. Glyph advances are measured once per content change.
/// </summary>
internal sealed class LoginTextFieldView
{
    private readonly int _padding;
    private string _value = string.Empty;
    private int[] _advances = [0];
    private int _scrollStart;
    private Line? _line;

    internal LoginTextFieldView(int padding)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(padding);
        _padding = padding;
    }

    /// <summary>
    /// Re-measures when <paramref name="changedValue"/> is given, then scrolls so <paramref name="caret"/> stays
    /// visible; a negative caret keeps the end of the text in view.
    /// </summary>
    internal void Update(OpenGLTextContext font, LoginRect field, string? changedValue, int caret)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (changedValue is not null)
        {
            _value = changedValue;
            _advances = Measure(font, changedValue);
            _line = null;
        }

        _line = Build(font, field, caret, _line);
    }

    internal void Draw(OpenGLRenderer renderer, OpenGLTextContext font, NativeTextRenderOptions style, SpriteColor caretColor,
        SpriteColor selectionColor, LoginTextFieldState state)
    {
        if (_line is not { } line)
        {
            return;
        }

        using OpenGLRenderer.ClipScope clip = renderer.Clip(line.Field.X + 1, line.Field.Y + 1,
            line.Field.Width - 2, line.Field.Height - 2);
        if (state.Focused && state.SelectionEnd > state.SelectionStart)
        {
            int left = line.X + Boundary(line, state.SelectionStart);
            int right = line.X + Boundary(line, state.SelectionEnd);
            if (right > left)
            {
                renderer.DrawSolidRectangle(left, line.Field.Y + 2, right - left, Math.Max(1, line.Field.Height - 4), selectionColor);
            }
        }

        if (line.Text is not null && !line.Text.HasMissingGlyphs)
        {
            font.Draw(renderer, line.Text, style, line.X, line.Y);
        }

        if (state.Focused && state.CaretVisible)
        {
            int x = line.X + Boundary(line, state.Caret);
            renderer.DrawSolidRectangle(Math.Clamp(x, line.Field.X + 1, line.Field.X + line.Field.Width - 2),
                line.Field.Y + 2, 1, Math.Max(1, line.Field.Height - 4), caretColor);
        }
    }

    /// <summary>The measured caret boundary nearest <paramref name="x"/>.</summary>
    internal int Locate(int x)
    {
        if (_line is not { } line)
        {
            return 0;
        }

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

    /// <summary>Drag-selection is captured by the field even beyond its visible left and right edges.</summary>
    internal int LocateWhileDragging(int x, int length)
    {
        if (_line is not { } line)
        {
            return 0;
        }

        if (x < line.Field.X)
        {
            return Math.Max(0, line.Start - 1);
        }

        if (x >= line.Field.X + line.Field.Width)
        {
            return Math.Min(length, line.End + 1);
        }

        return Locate(x);
    }

    internal void Reset()
    {
        _line = null;
        _scrollStart = 0;
        _value = string.Empty;
        _advances = [0];
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

    private Line Build(OpenGLTextContext font, LoginRect field, int caret, Line? previous)
    {
        int usableWidth = Math.Max(1, field.Width - 2 * _padding);
        int start = Math.Clamp(_scrollStart, 0, _value.Length);
        int target = caret < 0 ? _value.Length : Math.Clamp(caret, 0, _value.Length);
        if (target < start)
        {
            start = target;
        }

        while (start < target && _advances[target] - _advances[start] > usableWidth)
        {
            start++;
        }

        while (start > 0 && _advances[_value.Length] - _advances[start - 1] <= usableWidth)
        {
            start--;
        }

        int end = start;
        while (end < _value.Length && _advances[end + 1] - _advances[start] <= usableWidth)
        {
            end++;
        }

        _scrollStart = start;
        if (previous is { } retained && retained.Start == start && retained.End == end && retained.Field == field)
        {
            return retained;
        }

        string visible = _value[start..end];
        OpenGLTextLayout? text = visible.Length == 0 ? null : font.LayoutUnicode(visible);
        int[] boundaries = new int[visible.Length + 1];
        // NativeTextLayoutEngine advances each glyph without kerning; prefix deltas are exact.
        for (int index = 1; index < boundaries.Length; index++)
        {
            boundaries[index] = _advances[start + index] - _advances[start];
        }

        return new Line(field, start, end, field.X + _padding, field.Y + _padding, text, boundaries);
    }

    private static int Boundary(Line line, int index) => line.Boundaries[Math.Clamp(index - line.Start, 0, line.Boundaries.Length - 1)];

    private sealed record Line(LoginRect Field, int Start, int End, int X, int Y, OpenGLTextLayout? Text, int[] Boundaries);
}
