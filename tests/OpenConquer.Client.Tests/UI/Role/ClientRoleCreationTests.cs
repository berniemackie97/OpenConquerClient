using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using OpenConquer.Client.UI.Flash;
using OpenConquer.Client.UI.Role;
using OpenConquer.Content;
using OpenConquer.Content.Flash;
using OpenConquer.Networking.Game.Handshake;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Platform.Windowing.Desktop;

namespace OpenConquer.Client.Tests.UI.Role;

public sealed class ClientRoleCreationTests
{
    [Fact]
    public async Task MissingContent_ClosesTheUnclaimedSessionAndBlocksReopeningUntilObserved()
    {
        Harness harness = new();
        await using ClientRoleCreation owner = harness.Create();
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task closeCompletion = closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        int closes = 0;

        Assert.False(owner.TryOpen(() => throw new FileNotFoundException(), () =>
        {
            closes++;
            return new ValueTask(closeCompletion);
        }));
        Assert.False(owner.IsActive);
        Assert.True(owner.IsClosing);
        Assert.False(owner.CanOpen);
        owner.ObserveClose();
        Assert.True(owner.IsClosing);

        closed.SetResult();
        await closeCompletion;
        owner.ObserveClose();

        Assert.True(owner.CanOpen);
        Assert.Equal(1, closes);
        Assert.True(owner.TryOpen(() => harness.Screen, () => throw new InvalidOperationException()));
    }

    [Fact]
    public async Task FailedEventBindingClosesTheCandidateAndDetachesLateCallbacks()
    {
        Harness harness = new();
        await using ClientRoleCreation owner = harness.Create();
        TestRoleCreationScreen candidate = new()
        {
            ExitBindingFailure = new IOException("Binding failed.")
        };

        Assert.False(owner.TryOpen(() => candidate, () => throw new InvalidOperationException("Screen owns the session.")));
        Assert.False(owner.IsActive);
        Assert.Equal(1, candidate.DisposeCalls);
        owner.ObserveClose();
        Assert.True(owner.TryOpen(() => harness.Screen, () => throw new InvalidOperationException()));

        candidate.RequestReturn();
        candidate.RequestExit();
        owner.Update(TimeSpan.Zero);

        Assert.True(owner.IsActive);
        Assert.DoesNotContain("exit", harness.Calls);
        Assert.DoesNotContain("return:False", harness.Calls);
    }

    [Fact]
    public async Task UnexpectedInitializationFailure_PropagatesAndShutdownWaitsForUnclaimedSessionClose()
    {
        Harness harness = new();
        ClientRoleCreation owner = harness.Create();
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        InvalidOperationException failure = new("Screen construction failed.");

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
            owner.TryOpen(() => throw failure, () => new ValueTask(closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)))));
        Task disposing = owner.DisposeAsync().AsTask();
        Assert.False(disposing.IsCompleted);

        closed.SetResult();
        await disposing.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(owner.IsClosing);
        Assert.False(owner.CanOpen);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Return_ReportsTheReasonBeforeClosingAndCanReopenAfterClose(bool connectionLost)
    {
        Harness harness = new();
        harness.Screen.ConnectionLost = connectionLost;
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task closeCompletion = closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        harness.Screen.Close = () => new ValueTask(closeCompletion);
        await using ClientRoleCreation owner = harness.Create();
        Assert.True(owner.TryOpen(() => harness.Screen, () => throw new InvalidOperationException()));

        harness.Screen.RequestReturn();
        owner.Update(TimeSpan.FromMilliseconds(25));

        Assert.Equal(["tick", $"return:{connectionLost}", "dispose"], harness.Calls);
        Assert.False(owner.IsActive);
        Assert.True(owner.IsClosing);
        Assert.False(owner.CanOpen);
        owner.Update(TimeSpan.Zero);
        Assert.Equal(1, harness.Screen.DisposeCalls);

        closed.SetResult();
        await closeCompletion;
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while (owner.IsClosing)
        {
            owner.ObserveClose();
            if (owner.IsClosing)
            {
                await Task.Delay(1, deadline.Token);
            }
        }

        Assert.True(owner.CanOpen);
        Assert.True(owner.TryOpen(() => new TestRoleCreationScreen(), () => throw new InvalidOperationException()));
    }

    [Fact]
    public async Task Return_IsDeferredWhileTheWorldHasAScene()
    {
        Harness harness = new()
        {
            HasWorldScene = true,
            CanAdmitWorld = false
        };
        await using ClientRoleCreation owner = harness.Create();
        owner.TryOpen(() => harness.Screen, () => throw new InvalidOperationException());
        harness.Screen.RequestReturn();

        owner.Update(TimeSpan.Zero);

        Assert.True(owner.IsActive);
        Assert.Equal(["tick"], harness.Calls);
        harness.HasWorldScene = false;
        owner.Update(TimeSpan.Zero);
        Assert.False(owner.IsActive);
        Assert.Equal(["tick", "tick", "return:False", "dispose"], harness.Calls);
    }

    [Fact]
    public async Task FailedClose_IsObservedOnceAndCleared()
    {
        Harness harness = new();
        await using ClientRoleCreation owner = harness.Create();
        IOException failure = new("Close failed.");
        owner.TryOpen(() => throw new InvalidDataException(), () => ValueTask.FromException(failure));

        Assert.Same(failure, Assert.Throws<IOException>(owner.ObserveClose));
        owner.ObserveClose();

        Assert.True(owner.CanOpen);
    }

    [Fact]
    public async Task DisposeAsync_CoalescesPendingScreenCloseAndPreventsReopening()
    {
        Harness harness = new();
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Screen.Close = () => new ValueTask(closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        ClientRoleCreation owner = harness.Create();
        owner.TryOpen(() => harness.Screen, () => throw new InvalidOperationException());

        Task first = owner.DisposeAsync().AsTask();
        Task second = owner.DisposeAsync().AsTask();
        Assert.Same(first, second);
        Assert.False(first.IsCompleted);
        Assert.Equal(1, harness.Screen.DisposeCalls);
        Assert.False(owner.CanOpen);

        closed.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await owner.DisposeAsync();

        Assert.False(owner.IsActive);
        Assert.Equal(1, harness.Screen.DisposeCalls);
        Assert.Throws<InvalidOperationException>(() =>
            owner.TryOpen(() => harness.Screen, () => throw new InvalidOperationException()));
    }

    [Fact]
    public async Task DisposeAsync_PropagatesScreenFailureAndStillDropsTheActiveScreen()
    {
        Harness harness = new();
        IOException failure = new("Screen close failed.");
        harness.Screen.Close = () => ValueTask.FromException(failure);
        ClientRoleCreation owner = harness.Create();
        owner.TryOpen(() => harness.Screen, () => throw new InvalidOperationException());

        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => owner.DisposeAsync().AsTask()));

        Assert.False(owner.IsActive);
        Assert.False(owner.IsClosing);
        Assert.False(owner.CanOpen);
        Assert.Equal(1, harness.Screen.DisposeCalls);
    }

    [Fact]
    public async Task Input_PreservesOutsideStageReleaseAndExitRouting()
    {
        Harness harness = new();
        await using ClientRoleCreation owner = harness.Create();
        owner.TryOpen(() => harness.Screen, () => throw new InvalidOperationException());

        owner.PointerMoved(false, 100, 200);
        owner.PointerPressed(false, 100, 200, false);
        owner.PointerPressed(true, 3, 4, true);
        owner.PointerReleased(false, 100, 200);
        owner.TextEntered('A');
        harness.Screen.RequestExit();

        Assert.Equal([new Vector2(-1, -1), new Vector2(3, 4), new Vector2(-1, -1)], harness.Screen.Pointers);
        Assert.Equal([true], harness.Screen.Presses);
        Assert.Equal(["release", "text:A", "exit"], harness.Calls);
    }

    [Fact]
    public async Task ContextRelease_DetachesCaretWithoutClosingTheScreen()
    {
        Harness harness = new();
        await using ClientRoleCreation owner = harness.Create();
        owner.TryOpen(() => harness.Screen, () => throw new InvalidOperationException());
        harness.Screen.Stage.TextCaretAt = static (_, _) => 1;

        owner.ReleaseGraphicsBinding();

        Assert.Null(harness.Screen.Stage.TextCaretAt);
        Assert.True(owner.IsActive);
        Assert.Equal(0, harness.Screen.DisposeCalls);
    }

    private sealed class Harness
    {
        public List<string> Calls { get; } = [];
        public TestRoleCreationScreen Screen
        {
            get;
        }
        public bool CanAdmitWorld { get; set; } = true;
        public bool HasWorldScene
        {
            get; set;
        }

        public Harness() => Screen = new TestRoleCreationScreen(Calls);

        public ClientRoleCreation Create() => new(new MissingContentSource(), _ => { }, () => Calls.Add("exit"),
            () => CanAdmitWorld, () => HasWorldScene, (_, _) => throw new InvalidOperationException(),
            lost => Calls.Add($"return:{lost}"));
    }

    internal sealed class MissingContentSource : IClientContentSource
    {
        public bool TryOpenRead(string contentPath, ContentLookupMode mode, [NotNullWhen(true)] out Stream? stream)
        {
            stream = null;
            return false;
        }

        public Stream OpenRequiredRead(string contentPath, ContentLookupMode mode) => throw new FileNotFoundException();
    }
}

internal sealed class TestRoleCreationScreen(List<string>? calls = null) : IRoleCreationScreen
{
    private FlashStage? _stage;
    private bool _transferred;
    public event Action? ReturnRequested;
    private Action? _exitRequested;
    public Exception? ExitBindingFailure
    {
        get; set;
    }
    public event Action? ExitRequested
    {
        add
        {
            if (ExitBindingFailure is { } failure)
            {
                throw failure;
            }

            _exitRequested += value;
        }
        remove => _exitRequested -= value;
    }
    public FlashStage Stage => _stage ??= new FlashStage(RoleMovies.LoadRole(), new FlashScriptTable(), new FlashRasterCache());
    public bool ConnectionLost
    {
        get; set;
    }
    public GameServerHandshakeSession? AdmittedSession
    {
        get; set;
    }
    public GameWorldEntrySnapshot? AdmittedWorld
    {
        get; set;
    }
    public Func<ValueTask> Close { get; set; } = static () => ValueTask.CompletedTask;
    public int DisposeCalls
    {
        get; private set;
    }
    public List<Vector2> Pointers { get; } = [];
    public List<bool> Presses { get; } = [];

    public bool TryTakeAdmittedWorld([NotNullWhen(true)] out GameServerHandshakeSession? game,
        [NotNullWhen(true)] out GameWorldEntrySnapshot? world)
    {
        calls?.Add("take");
        game = AdmittedSession;
        world = AdmittedWorld;
        if (_transferred || game is null || world is null)
        {
            return false;
        }

        _transferred = true;
        return true;
    }

    public void Tick(TimeSpan elapsed) => calls?.Add("tick");
    public void PointerMoved(Vector2 position) => Pointers.Add(position);
    public void PointerPressed(bool extendSelection) => Presses.Add(extendSelection);
    public void PointerReleased() => calls?.Add("release");
    public void TextEntered(char character) => calls?.Add($"text:{character}");
    public void KeyPressed(DesktopKeyEvent key, Func<string?> readClipboard, Action<string> writeClipboard) => throw new NotSupportedException();
    public void RequestReturn() => ReturnRequested?.Invoke();
    public void RequestExit() => _exitRequested?.Invoke();

    public ValueTask DisposeAsync()
    {
        DisposeCalls++;
        calls?.Add("dispose");
        return Close();
    }
}
