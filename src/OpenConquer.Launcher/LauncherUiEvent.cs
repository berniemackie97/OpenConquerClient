using System.Runtime.ExceptionServices;
using Avalonia.Threading;

namespace OpenConquer.Launcher;

/// <summary>Observes asynchronous work started by synchronous Avalonia events.</summary>
internal static class LauncherUiEvent
{
    // If the window is already closed, the desktop host still retains its failure.
    public static void RethrowOnDispatcher(Exception failure) =>
        Dispatcher.UIThread.Post(ExceptionDispatchInfo.Capture(failure).Throw);

    public static async void Run(Func<Task> operation, Action<Exception> onFailure)
    {
        try
        {
            await operation();
        }
        catch (Exception failure)
        {
            onFailure(failure);
        }
    }
}
