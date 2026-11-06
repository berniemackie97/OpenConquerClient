using System.Runtime.ExceptionServices;

namespace OpenConquer.Client;

/// <summary>Runs ordered cleanup stages without allowing one failure to skip later owners.</summary>
internal sealed class ClientShutdown
{
    private readonly List<Exception> _failures = [];

    public void Run(Action cleanup)
    {
        try
        {
            cleanup();
        }
        catch (Exception failure)
        {
            // Retain this failure and continue releasing the remaining owners.
            _failures.Add(failure);
        }
    }

    public void ThrowIfFailed()
    {
        if (_failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(_failures[0]).Throw();
        }
        if (_failures.Count > 1)
        {
            throw new AggregateException("Client cleanup failed.", _failures).Flatten();
        }
    }

    public async ValueTask RunAsync(Func<ValueTask> cleanup)
    {
        try
        {
            await cleanup().ConfigureAwait(false);
        }
        catch (Exception failure)
        {
            // Retain this failure and continue releasing the remaining owners.
            _failures.Add(failure);
        }
    }
}
