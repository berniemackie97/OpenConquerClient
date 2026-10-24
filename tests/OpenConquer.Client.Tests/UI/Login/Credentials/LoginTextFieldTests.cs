using OpenConquer.Client.UI.Login.Credentials;
using OpenConquer.Platform.Windowing.Desktop;

namespace OpenConquer.Client.Tests.UI.Login.Credentials;

public sealed class LoginTextFieldTests
{
    [Fact]
    public void TypingAcceptsOnlyTheKeypadAlphabetUpToCapacity()
    {
        LoginTextField field = new(4, masked: false);

        Assert.True(field.TextEntered('a'));
        Assert.False(field.TextEntered(' '));
        Assert.False(field.TextEntered('é'));
        Assert.True(field.Paste("b.@"));
        Assert.False(field.TextEntered('c'));

        Assert.Equal("ab.@", field.Display);
        Assert.Equal(4, field.CaretIndex);
    }

    [Fact]
    public void PasteIsAllOrNothing()
    {
        LoginTextField field = new(5, masked: false);
        Assert.True(field.Paste("abc"));

        Assert.False(field.Paste("de f"));
        Assert.False(field.Paste("defghi"));
        Assert.False(field.Paste("def"));
        Assert.False(field.Paste(null));

        Assert.Equal("abc", field.Display);
    }

    [Fact]
    public void TypingReplacesTheSelection()
    {
        LoginTextField field = new(8, masked: false);
        Assert.True(field.Paste("abcdef"));
        Assert.True(field.MoveTo(1, extend: false));
        Assert.True(field.MoveTo(4, extend: true));

        Assert.True(field.TextEntered('X'));

        Assert.Equal("aXef", field.Display);
        Assert.Equal(2, field.CaretIndex);
        Assert.False(field.HasSelection);
    }

    [Fact]
    public void NavigationKeysMoveTheCaretAndCollapseSelections()
    {
        LoginTextField field = new(8, masked: false);
        Assert.True(field.Paste("abcd"));

        Assert.Equal(LoginTextChange.Caret, field.EditKey(DesktopKey.Home, shift: false));
        Assert.Equal(LoginTextChange.None, field.EditKey(DesktopKey.Left, shift: false));
        Assert.Equal(LoginTextChange.Caret, field.EditKey(DesktopKey.End, shift: true));
        Assert.Equal((0, 4), (field.SelectionStart, field.SelectionEnd));

        Assert.Equal(LoginTextChange.Caret, field.EditKey(DesktopKey.Left, shift: false));
        Assert.Equal(0, field.CaretIndex);
        Assert.False(field.HasSelection);
        Assert.Equal(LoginTextChange.None, field.EditKey(DesktopKey.Enter, shift: false));
    }

    [Fact]
    public void DeletionKeysRemoveTheSelectionOrOneCharacter()
    {
        LoginTextField field = new(8, masked: false);
        Assert.True(field.Paste("abcd"));

        Assert.Equal(LoginTextChange.Text, field.EditKey(DesktopKey.Backspace, shift: false));
        Assert.Equal("abc", field.Display);
        Assert.Equal(LoginTextChange.None, field.EditKey(DesktopKey.Delete, shift: false));

        Assert.True(field.SelectAll());
        Assert.Equal(LoginTextChange.Text, field.EditKey(DesktopKey.Delete, shift: false));
        Assert.Equal(string.Empty, field.Display);
        Assert.Equal(LoginTextChange.None, field.EditKey(DesktopKey.Backspace, shift: false));
    }

    [Fact]
    public void MaskedFieldNeverShowsItsCharacters()
    {
        LoginTextField field = new(8, masked: true);
        Assert.True(field.Paste("secret"));
        Assert.True(field.SelectAll());

        Assert.Equal("******", field.Display);
        Assert.Null(field.SelectedText());
        Assert.Equal("secret", field.Characters.ToString());
    }

    [Fact]
    public void ReplaceValidatesAndClearEmpties()
    {
        LoginTextField field = new(4, masked: false);

        Assert.Throws<ArgumentException>(() => field.Replace("abcde"));
        Assert.Throws<ArgumentException>(() => field.Replace("a b"));
        field.Replace("abcd");
        Assert.Equal(("abcd", 4), (field.Display, field.CaretIndex));

        Assert.True(field.Clear());
        Assert.False(field.Clear());
        Assert.Equal((0, 0), (field.Length, field.CaretIndex));
    }
}
