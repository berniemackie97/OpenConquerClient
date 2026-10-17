namespace OpenConquer.Client.UI.Login.Credentials;

/// <summary>Maps pointer positions to caret positions using the glyph measurements of the rendered fields.</summary>
internal interface ILoginCaretLocator
{
    /// <summary>The caret position nearest a press inside either field.</summary>
    bool TryLocate(LoginCredentialInput input, int x, int y, out int caret);

    /// <summary>The caret position for a drag in the focused field, scrolling past either edge.</summary>
    int LocateWhileDragging(LoginCredentialInput input, int x);
}
