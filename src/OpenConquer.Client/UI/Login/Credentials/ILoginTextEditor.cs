namespace OpenConquer.Client.UI.Login.Credentials;

/// <summary>The clipboard side of a group of login text fields.</summary>
internal interface ILoginTextEditor
{
    /// <summary>A field of the group has keyboard focus.</summary>
    bool HasFocus
    {
        get;
    }

    /// <summary>All-or-nothing paste into the focused field.</summary>
    bool Paste(string? text);

    /// <summary>The selection of the focused field; never the characters of a masked field.</summary>
    string? SelectedText();

    /// <summary>Deletes the selection of the focused unmasked field.</summary>
    bool CutSelection();
}
