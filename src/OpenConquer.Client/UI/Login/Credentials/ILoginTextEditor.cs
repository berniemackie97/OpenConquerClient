namespace OpenConquer.Client.UI.Login.Credentials;

internal interface ILoginTextEditor
{
    bool HasFocus
    {
        get;
    }

    bool Paste(string? text);
    string? SelectedText();
    bool CutSelection();
}
