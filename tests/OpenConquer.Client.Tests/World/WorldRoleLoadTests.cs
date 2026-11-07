using OpenConquer.Client.World;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldRoleLoadTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);
    private static readonly WorldRoleSources s_original = new(2_000_000, 0, 0, 0);
    private static readonly WorldRoleSources s_intermediate = new(2_130_000, 0, 0, 0);
    private static readonly WorldRoleSources s_latest = new(2_130_010, 0, 0, 0);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Poll_DiscardsStaleWorkAndStartsOnlyTheLatestAppearance(bool staleFails)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<WorldRoleSources> requests = [];
        await using WorldRoleLoad load = new((sources, stop) =>
        {
            requests.Add(sources);
            if (sources == s_original)
            {
                entered.SetResult();
                release.Task.WaitAsync(s_timeout, stop).GetAwaiter().GetResult();
                if (staleFails)
                {
                    throw new InvalidDataException("Obsolete appearance failed.");
                }
            }

            return null; // No C3 catalog is a valid completed result.
        });

        Assert.False(load.TryPrepare(s_original, null, out _));
        await entered.Task.WaitAsync(s_timeout, token);
        Assert.False(load.TryPrepare(s_intermediate, null, out _));
        Assert.False(load.TryPrepare(s_latest, null, out _));
        release.SetResult();

        await PollUntilReadyAsync(load, s_latest, token);
        Assert.Equal([s_original, s_latest], requests);
        Assert.False(load.TryPrepare(s_latest, s_latest, out _));
    }

    [Fact]
    public async Task Poll_SurfacesFailureForTheCurrentAppearance()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using WorldRoleLoad load = new((_, _) => throw new IOException("Appearance read failed."));
        await Assert.ThrowsAsync<IOException>(() => PollUntilReadyAsync(load, s_original, token));
    }

    [Fact]
    public async Task Dispose_CancelsAndWaitsForAnInFlightRead()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource canceled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        WorldRoleLoad load = new((_, stop) =>
        {
            using CancellationTokenRegistration registration = stop.Register(() => canceled.SetResult());
            entered.SetResult();
            release.Task.WaitAsync(s_timeout, token).GetAwaiter().GetResult();
            stop.ThrowIfCancellationRequested();
            return null;
        });

        Assert.False(load.TryPrepare(s_original, null, out _));
        await entered.Task.WaitAsync(s_timeout, token);

        Task closing = load.DisposeAsync().AsTask();
        try
        {
            await canceled.Task.WaitAsync(s_timeout, token);
            Assert.False(closing.IsCompleted);
        }
        finally
        {
            release.SetResult();
            await closing.WaitAsync(s_timeout, token);
        }
    }

    private static async Task PollUntilReadyAsync(WorldRoleLoad load, WorldRoleSources sources, CancellationToken token)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(s_timeout);
        while (!load.TryPrepare(sources, null, out _))
        {
            await Task.Delay(1, deadline.Token);
        }
    }
}
