using Avalonia.Threading;

namespace OpenConquer.Launcher.Instances;

internal static class LauncherActivationDispatcher
{
    public static async Task<bool> InvokeAsync(Func<bool> activate, CancellationToken cancellationToken)
    {
        Task<bool> operation = Dispatcher.UIThread.InvokeAsync(activate, DispatcherPriority.Normal, cancellationToken).GetTask();
        try
        {
            return await operation.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (operation.IsCanceled && cancellationToken.IsCancellationRequested)
        {
            // Avalonia aborts dispatcher operations without preserving the supplied token. Only
            // normalize an aborted operation, never a fault thrown by the activation callback.
            throw new OperationCanceledException(cancellationToken);
        }
    }
}
