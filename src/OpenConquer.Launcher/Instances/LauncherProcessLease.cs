namespace OpenConquer.Launcher.Instances;

/// <summary>A user-scoped process lease acquired and released on the executable's main thread.</summary>
internal sealed class LauncherProcessLease : IDisposable
{
    private readonly Mutex _mutex;
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private bool _owned;
    private bool _disposed;

    public LauncherProcessLease(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _mutex = new Mutex(name, new NamedWaitHandleOptions
        {
            CurrentUserOnly = true,
            CurrentSessionOnly = false,
        });
    }

    public bool TryAcquire()
    {
        VerifyThread();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_owned)
        {
            return true;
        }

        try
        {
            _owned = _mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            // The OS granted ownership after the previous process/thread terminated.
            _owned = true;
        }

        return _owned;
    }

    public void Dispose()
    {
        VerifyThread();
        if (_disposed)
        {
            return;
        }

        if (_owned)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
        _disposed = true;
    }

    private void VerifyThread()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread)
        {
            throw new InvalidOperationException("The launcher process lease must stay on its owning thread.");
        }
    }
}
