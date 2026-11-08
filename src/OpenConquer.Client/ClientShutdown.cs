using System.Runtime.ExceptionServices;

namespace OpenConquer.Client;

/// <summary>Runs ordered cleanup stages without allowing one failure to skip later owners.</summary>
internal sealed class ClientShutdown(Exception? initialFailure = null)
{
    private readonly List<Exception> _failures = initialFailure is null ? [] : [initialFailure];

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

    internal static async ValueTask CancelAndDrainAsync(CancellationTokenSource stopping, Func<ValueTask> drain)
    {
        ClientShutdown cleanup = new();
        await cleanup.RunAsync(() => new ValueTask(stopping.CancelAsync())).ConfigureAwait(false);
        try
        {
            await cleanup.RunAsync(async () =>
            {
                try
                {
                    await drain().ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stopping.IsCancellationRequested)
                {
                    // The owner requested cancellation before draining the operation.
                }
            }).ConfigureAwait(false);
        }
        finally
        {
            stopping.Dispose();
        }

        cleanup.ThrowIfFailed();
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
