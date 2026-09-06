using OpenConquer.Launcher.Instances;

namespace OpenConquer.Launcher.Tests.Instances;

public sealed class LauncherActivationDispatcherTests
{
    [Fact]
    public async Task CanceledDispatch_PreservesRequestCancellationWithoutInvokingTheWindow()
    {
        using CancellationTokenSource canceled = new();
        await canceled.CancelAsync();
        bool invoked = false;
        OperationCanceledException failure = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            LauncherActivationDispatcher.InvokeAsync(() => invoked = true, canceled.Token));
        Assert.Equal(canceled.Token, failure.CancellationToken);
        Assert.False(invoked);
    }
}
