namespace OpenConquer.Client.UI.Login.Registration;

internal interface ILoginRegistrationCaretLocator
{
    bool TryLocate(LoginRegistrationForm form, LoginRegistrationField field, int x, out int caret);
    int LocateWhileDragging(LoginRegistrationForm form, int x);
}
