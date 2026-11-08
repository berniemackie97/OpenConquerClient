namespace OpenConquer.Client.UI;

/// <summary>
/// Holds disposable resources created one after another and disposes them in reverse order. If creating one
/// fails, the ones already created are disposed and the original exception still propagates.
/// </summary>
internal sealed class ResourceOwnership : IDisposable
{
    private readonly List<IDisposable> _resources = [];
    private bool _disposed;

    public T Track<T>(T resource) where T : class, IDisposable
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(resource);

        try
        {
            _resources.Add(resource);
        }
        catch
        {
            try
            {
                resource.Dispose();
            }
            catch
            {
                // Preserve the registration failure.
            }

            throw;
        }

        return resource;
    }

    public void DisposeAfterInitializationFailure()
    {
        try
        {
            Dispose();
        }
        catch
        {
            // Preserve the construction failure.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ClientShutdown cleanup = new();
        for (int index = _resources.Count - 1; index >= 0; index--)
        {
            cleanup.Run(_resources[index].Dispose);
        }

        _resources.Clear();
        cleanup.ThrowIfFailed();
    }
}
