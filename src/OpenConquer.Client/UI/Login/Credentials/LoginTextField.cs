using System.Runtime.InteropServices;
using System.Security.Cryptography;
using OpenConquer.Client.UI.Login.Account;
using OpenConquer.Platform.Windowing.Desktop;

namespace OpenConquer.Client.UI.Login.Credentials;

/// <summary>What an edit did to a <see cref="LoginTextField"/>.</summary>
internal enum LoginTextChange
{
    None,
    /// <summary>Only the caret or the selection moved.</summary>
    Caret,
    /// <summary>The characters changed.</summary>
    Text,
}

/// <summary>
/// One single-line editor over a fixed buffer: caret, selection and insertion of the native keypad alphabet.
/// Clearing zeroes the buffer, and a masked field never turns its characters into a string.
/// </summary>
internal sealed class LoginTextField
{
    private readonly char[] _buffer;
    private int _length;
    private int _caret;
    private int _anchor;

    internal LoginTextField(int capacity, bool masked)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _buffer = new char[capacity];
        IsMasked = masked;
    }

    internal int Capacity => _buffer.Length;
    internal bool IsMasked
    {
        get;
    }
    internal int Length => _length;
    internal int CaretIndex => _caret;
    internal int SelectionStart => Math.Min(_anchor, _caret);
    internal int SelectionEnd => Math.Max(_anchor, _caret);
    internal bool HasSelection => _anchor != _caret;

    /// <summary>The text as drawn: the characters, or one <c>*</c> per character when masked.</summary>
    internal string Display => IsMasked ? new string('*', _length) : new string(_buffer, 0, _length);

    /// <summary>The live characters. Callers copy what they need and never keep the span.</summary>
    internal ReadOnlySpan<char> Characters => _buffer.AsSpan(0, _length);

    internal static bool Accepts(char value) => LoginAccountHistory.IsAccountCharacter(value);

    /// <summary>Replaces the selection with one accepted character.</summary>
    internal bool TextEntered(char character) => Accepts(character) && Insert([character]);

    /// <summary>All-or-nothing paste; unsupported characters and text longer than the field are rejected.</summary>
    internal bool Paste(string? text)
    {
        if (string.IsNullOrEmpty(text) || text.Length > _buffer.Length)
        {
            return false;
        }

        foreach (char character in text)
        {
            if (!Accepts(character))
            {
                return false;
            }
        }

        return Insert(text.AsSpan());
    }

    /// <summary>Applies a navigation or deletion key; other keys do nothing.</summary>
    internal LoginTextChange EditKey(DesktopKey key, bool shift)
    {
        switch (key)
        {
            case DesktopKey.Left:
                return MoveTo(!shift && HasSelection ? SelectionStart : _caret - 1, shift) ? LoginTextChange.Caret : LoginTextChange.None;
            case DesktopKey.Right:
                return MoveTo(!shift && HasSelection ? SelectionEnd : _caret + 1, shift) ? LoginTextChange.Caret : LoginTextChange.None;
            case DesktopKey.Home:
                return MoveTo(0, shift) ? LoginTextChange.Caret : LoginTextChange.None;
            case DesktopKey.End:
                return MoveTo(_length, shift) ? LoginTextChange.Caret : LoginTextChange.None;
            case DesktopKey.Backspace:
                if (DeleteSelection())
                {
                    return LoginTextChange.Text;
                }

                if (_caret > 0)
                {
                    Remove(_caret - 1, 1);
                    return LoginTextChange.Text;
                }

                return LoginTextChange.None;
            case DesktopKey.Delete:
                if (DeleteSelection())
                {
                    return LoginTextChange.Text;
                }

                if (_caret < _length)
                {
                    Remove(_caret, 1);
                    return LoginTextChange.Text;
                }

                return LoginTextChange.None;
            default:
                return LoginTextChange.None;
        }
    }

    /// <summary>Moves the caret, extending the selection or setting its anchor. Returns whether either moved.</summary>
    internal bool MoveTo(int position, bool extend, int? anchor = null)
    {
        int target = Math.Clamp(position, 0, _length);
        int newAnchor = anchor ?? (extend ? _anchor : target);
        if (target == _caret && newAnchor == _anchor)
        {
            return false;
        }

        _caret = target;
        _anchor = newAnchor;
        return true;
    }

    internal bool SelectAll() => MoveTo(_length, extend: false, anchor: 0);

    internal bool DeleteSelection()
    {
        if (!HasSelection)
        {
            return false;
        }

        Remove(SelectionStart, SelectionEnd - SelectionStart);
        return true;
    }

    /// <summary>The selected characters of an unmasked field; null when masked or nothing is selected.</summary>
    internal string? SelectedText() =>
        IsMasked || !HasSelection ? null : new string(_buffer, SelectionStart, SelectionEnd - SelectionStart);

    /// <summary>Replaces every character, leaving the caret at the end. The text must fit and be accepted.</summary>
    internal void Replace(ReadOnlySpan<char> text)
    {
        if (text.Length > _buffer.Length)
        {
            throw new ArgumentException("The text does not fit the field.", nameof(text));
        }

        foreach (char character in text)
        {
            if (!Accepts(character))
            {
                throw new ArgumentException("The text has a character the field does not accept.", nameof(text));
            }
        }

        Wipe();
        text.CopyTo(_buffer);
        _length = text.Length;
        _caret = _anchor = _length;
    }

    /// <summary>Zeroes the buffer and returns the caret home. Returns whether the field held any text.</summary>
    internal bool Clear()
    {
        bool hadText = _length != 0;
        Wipe();
        _length = 0;
        _caret = _anchor = 0;
        return hadText;
    }

    private bool Insert(ReadOnlySpan<char> characters)
    {
        int start = SelectionStart;
        int end = SelectionEnd;
        int newLength = _length - (end - start) + characters.Length;
        if (newLength > _buffer.Length)
        {
            return false;
        }

        _buffer.AsSpan(end, _length - end).CopyTo(_buffer.AsSpan(start + characters.Length));
        characters.CopyTo(_buffer.AsSpan(start));
        _buffer.AsSpan(newLength).Clear();
        _length = newLength;
        _caret = _anchor = start + characters.Length;
        return true;
    }

    private void Remove(int start, int count)
    {
        _buffer.AsSpan(start + count, _length - start - count).CopyTo(_buffer.AsSpan(start));
        _buffer.AsSpan(_length - count).Clear();
        _length -= count;
        _caret = _anchor = start;
    }

    private void Wipe() => CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(_buffer.AsSpan()));
}
