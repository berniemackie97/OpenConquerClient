namespace OpenConquer.Client.UI.Login.Credentials;

internal interface ILoginCaretLocator
{
    bool TryLocate(LoginCredentialInput input, int x, int y, out int caret);
    int LocateWhileDragging(LoginCredentialInput input, int x);
}
