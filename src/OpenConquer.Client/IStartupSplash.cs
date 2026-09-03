namespace OpenConquer.Client;

/// <summary>
/// The one-shot surface shown while the client initializes.
/// </summary>
/// <remarks>
/// The lifetime is deliberately show-then-dispose. Retail creates, shows, hides, and destroys the
/// startup logo entirely inside <c>CMyShellDlg::OnInitDialog</c> with no minimum display duration
/// and no intervening message pump, so there is no completion step to model.
/// </remarks>
internal interface IStartupSplash : IDisposable
{
    void Show();
}
