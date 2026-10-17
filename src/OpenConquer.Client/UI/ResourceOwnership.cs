using System.Runtime.ExceptionServices;

namespace OpenConquer.Client.UI;

/// <summary>
/// Owns a set of disposable resources created in sequence and releases them in reverse order. If a later
/// resource fails to construct, the ones already created are released without masking that failure.
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
        ExceptionDispatchInfo? firstFailure = null;

        for (int index = _resources.Count - 1; index >= 0; index--)
        {
            try
            {
                _resources[index].Dispose();
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        _resources.Clear();
        firstFailure?.Throw();
    }
}
