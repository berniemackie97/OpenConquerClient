using System.IO.Pipes;
using System.Security.Principal;
using OpenConquer.Launcher.Instances;

namespace OpenConquer.Launcher.Tests.Instances;

public sealed class LauncherActivationServerTests : IDisposable
{
    private readonly ActivationTestNamespace _namespace = new();

    public void Dispose() => _namespace.Dispose();
    [Fact]
    public async Task Activation_AcknowledgesExactlyOneCallbackAndHandlesRepeatedConnections()
    {
        string name = NewPipeName();
        await using LauncherActivationServer server = new(name);
        int activations = 0;
        Task listener = server.RunAsync(_ => Task.FromResult(Interlocked.Increment(ref activations) > 0));
        using CancellationTokenSource deadline = NewDeadline();

        Assert.True(await LauncherActivationProtocol.TryActivateAsync(name, deadline.Token));
        Assert.True(await LauncherActivationProtocol.TryActivateAsync(name, deadline.Token));
        Assert.Equal(2, activations);
        await server.StopAsync();
        await listener;
    }

    [Fact]
    public async Task Activation_ReturnsUnavailableWhenWindowIsClosing()
    {
        string name = NewPipeName();
        await using LauncherActivationServer server = new(name);
        _ = server.RunAsync(_ => Task.FromResult(false));
        using CancellationTokenSource deadline = NewDeadline();
        Assert.False(await LauncherActivationProtocol.TryActivateAsync(name, deadline.Token));
    }

    [Fact]
    public async Task InvalidAndTruncatedFrames_DoNotActivateAndDoNotStopTheListener()
    {
        string name = NewPipeName();
        await using LauncherActivationServer server = new(name);
        int activations = 0;
        _ = server.RunAsync(_ => Task.FromResult(Interlocked.Increment(ref activations) > 0));
        using CancellationTokenSource deadline = NewDeadline();

        using (NamedPipeClientStream invalid = NewClient(name))
        {
            await invalid.ConnectAsync(deadline.Token);
            await invalid.WriteAsync(new byte[LauncherActivationProtocol.RequestLength], deadline.Token);
            byte[] response = new byte[1];
            Assert.Equal(0, await invalid.ReadAsync(response, deadline.Token));
        }
        using (NamedPipeClientStream truncated = NewClient(name))
        {
            await truncated.ConnectAsync(deadline.Token);
            await truncated.WriteAsync(new byte[] { 0x4f }, deadline.Token);
        }

        Assert.True(await LauncherActivationProtocol.TryActivateAsync(name, deadline.Token));
        Assert.Equal(1, activations);
    }

    [Fact]
    public async Task StalledClient_ExpiresAndAllowsTheNextRequest()
    {
        string name = NewPipeName();
        await using LauncherActivationServer server = new(name, TimeSpan.FromMilliseconds(100));
        _ = server.RunAsync(_ => Task.FromResult(true));
        using CancellationTokenSource deadline = NewDeadline();
        using NamedPipeClientStream stalled = NewClient(name);
        await stalled.ConnectAsync(deadline.Token);
        byte[] response = new byte[1];
        Assert.Equal(0, await stalled.ReadAsync(response, deadline.Token));
        Assert.True(await LauncherActivationProtocol.TryActivateAsync(name, deadline.Token));
    }

    [Fact]
    public async Task Shutdown_CancelsAStalledReadAndSharesItsDrainTask()
    {
        string name = NewPipeName();
        LauncherActivationServer server = new(name);
        Task listener = server.RunAsync(_ => Task.FromResult(true));
        using CancellationTokenSource deadline = NewDeadline();
        using NamedPipeClientStream stalled = NewClient(name);
        await stalled.ConnectAsync(deadline.Token);
        Task first = server.StopAsync();
        Task second = server.StopAsync();
        await first.WaitAsync(deadline.Token);
        await listener;
        Assert.Same(first, second);
        await server.DisposeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => server.RunAsync(_ => Task.FromResult(true)));
    }

    [Fact]
    public async Task Shutdown_DrainsAnActivationWaitingForTheUi()
    {
        string name = NewPipeName();
        LauncherActivationServer server = new(name);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task listener = server.RunAsync(async token =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return true;
        });
        using CancellationTokenSource deadline = NewDeadline();
        Task<bool> activation = LauncherActivationProtocol.TryActivateAsync(name, deadline.Token);
        await entered.Task.WaitAsync(deadline.Token);
        await server.StopAsync().WaitAsync(deadline.Token);
        await listener;
        Assert.False(await activation);
    }

    [Fact]
    public async Task ApplicationIoFailure_IsNotMisclassifiedAsAPeerDisconnect()
    {
        string name = NewPipeName();
        LauncherActivationServer server = new(name);
        IOException failure = new("application failure");
        Task listener = server.RunAsync(_ => Task.FromException<bool>(failure));
        using CancellationTokenSource deadline = NewDeadline();
        Task<bool> activation = LauncherActivationProtocol.TryActivateAsync(name, deadline.Token);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => listener));
        Assert.False(await activation);
        AggregateException shutdown = await Assert.ThrowsAsync<AggregateException>(server.StopAsync);
        Assert.Contains(failure, shutdown.Flatten().InnerExceptions);
    }

    [Fact]
    public async Task ServerCanBeDisposedBeforeItStarts()
    {
        LauncherActivationServer server = new(NewPipeName());
        await server.DisposeAsync();
        await server.DisposeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => server.RunAsync(_ => Task.FromResult(true)));
    }

    [Fact]
    public async Task ClientHonorsItsCancellationBudgetWhenNoServerExists()
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            LauncherActivationProtocol.TryActivateAsync(NewPipeName(), deadline.Token));
    }

    [Fact]
    public async Task UnixSocketIsRestrictedToTheCurrentUser()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string name = NewPipeName();
        await using LauncherActivationServer server = new(name);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(name));
    }

    [Fact]
    public void ProtocolRejectsUnknownVersionsCommandsAndReservedFields()
    {
        byte[] valid = [0x4f, 0x43, 0x4c, 0x41, 1, 1, 0, 0];
        Assert.True(LauncherActivationProtocol.IsActivationRequest(valid));
        for (int i = 0; i < valid.Length; ++i)
        {
            byte[] modified = (byte[])valid.Clone();
            modified[i] ^= 0xff;
            Assert.False(LauncherActivationProtocol.IsActivationRequest(modified));
        }
        Assert.False(LauncherActivationProtocol.IsActivationRequest(valid.AsSpan(0, 7)));
        Assert.False(LauncherActivationProtocol.IsActivationRequest(new byte[9]));
    }

    private static NamedPipeClientStream NewClient(string name) => new(".", name, PipeDirection.InOut,
        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, TokenImpersonationLevel.Anonymous);

    private static CancellationTokenSource NewDeadline()
    {
        CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        return deadline;
    }

    private string NewPipeName() => _namespace.NewPipeName();
}
