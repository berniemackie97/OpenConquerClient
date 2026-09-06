using System.Diagnostics;

namespace OpenConquer.Launcher.Instances;

internal enum LauncherInstanceAdmission
{
    Primary,
    ActivatedExisting,
    ExistingUnavailable,
}

internal static class LauncherInstanceStartup
{
    private static readonly TimeSpan s_startupBudget = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan s_attemptBudget = TimeSpan.FromMilliseconds(750);

    // Synchronous by design: mutex ownership must remain on the main thread through desktop exit.
    public static LauncherInstanceAdmission Enter(LauncherProcessLease lease, string pipeName)
    {
        ArgumentNullException.ThrowIfNull(lease);
        long started = Stopwatch.GetTimestamp();

        do
        {
            if (lease.TryAcquire())
            {
                return LauncherInstanceAdmission.Primary;
            }

            TimeSpan remaining = s_startupBudget - Stopwatch.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            using CancellationTokenSource attempt = new(remaining < s_attemptBudget ? remaining : s_attemptBudget);
            try
            {
                if (LauncherActivationProtocol.TryActivateAsync(pipeName, attempt.Token).GetAwaiter().GetResult())
                {
                    return LauncherInstanceAdmission.ActivatedExisting;
                }
            }
            catch (OperationCanceledException) when (attempt.IsCancellationRequested)
            {
                // Recheck ownership when the previous process exits during startup or shutdown.
            }

            // A peer that immediately closes/rejects connections must not cause a busy loop.
            Thread.Sleep(50);
        }
        while (Stopwatch.GetElapsedTime(started) < s_startupBudget);

        return lease.TryAcquire() ? LauncherInstanceAdmission.Primary : LauncherInstanceAdmission.ExistingUnavailable;
    }
}
