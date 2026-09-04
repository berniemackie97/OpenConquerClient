namespace OpenConquer.Client;

/// <summary>
/// The one shot surface shown while the client initializes.
/// </summary>
internal interface IStartupSplash : IDisposable
{
    void Show();
}
