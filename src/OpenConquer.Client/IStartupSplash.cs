namespace OpenConquer.Client;

internal interface IStartupSplash : IDisposable
{
    void Show();

    void Complete();
}
