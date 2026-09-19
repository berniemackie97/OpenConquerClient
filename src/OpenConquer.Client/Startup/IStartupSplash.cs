namespace OpenConquer.Client.Startup;

/// <summary>
/// Represents the splash shown during client initialization.
/// </summary>
internal interface IStartupSplash : IDisposable
{
    void Show();
}
