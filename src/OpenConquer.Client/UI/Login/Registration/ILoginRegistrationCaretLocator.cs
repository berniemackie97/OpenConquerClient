namespace OpenConquer.Client.UI.Login.Registration;

/// <summary>Maps pointer positions to caret positions using the glyph measurements of the rendered Sign Up fields.</summary>
internal interface ILoginRegistrationCaretLocator
{
    /// <summary>The caret position nearest <paramref name="x"/> in <paramref name="field"/>.</summary>
    bool TryLocate(LoginRegistrationForm form, LoginRegistrationField field, int x, out int caret);

    /// <summary>The caret position for a drag in the focused field, scrolling past either edge.</summary>
    int LocateWhileDragging(LoginRegistrationForm form, int x);
}
