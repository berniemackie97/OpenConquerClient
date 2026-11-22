using System.Runtime.ExceptionServices;

namespace OpenConquer.Client;

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
            _failures.Add(failure);
        }
    }

    public void ThrowIfFailed()
    {
        switch (_failures.Count)
        {
            case 1:
                ExceptionDispatchInfo.Capture(_failures[0]).Throw();
                break;
            case > 1:
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
                    // cancellation requested
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
            _failures.Add(failure);
        }
    }
}
