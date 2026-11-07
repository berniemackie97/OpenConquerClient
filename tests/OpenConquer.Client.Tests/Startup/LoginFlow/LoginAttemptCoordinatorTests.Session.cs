using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using OpenConquer.Client.Startup.Catalog;
using OpenConquer.Client.Startup.LoginFlow;
using OpenConquer.Client.Tests.Networking;
using OpenConquer.Client.Tests.UI.Login;
using OpenConquer.Client.Tests.UI.Role;
using OpenConquer.Client.UI.Login;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Client.World;
using OpenConquer.Content.Login;
using OpenConquer.Networking.Game.Framing;
using OpenConquer.Networking.Game.Handshake;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Networking.Game.World;
using OpenConquer.Platform.Geometry;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.Startup.LoginFlow;

public sealed partial class LoginAttemptCoordinatorTests
{
    private static readonly int[] s_loadingMatrix = [1, 0, 0, 1, 0, 0];
    [Fact]
    public async Task Session_ShutdownCancelsLoginWaitingForAccountSeed()
    {
        using CancellationTokenSource budget = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(10));
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        TaskCompletionSource accepted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task peer = ObservePendingAccountCloseAsync(listener, accepted, budget.Token);
        DateTimeOffset now = new(2026, 10, 17, 22, 0, 0, TimeSpan.Zero);
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = CreateSignedLocalCatalog(signer, ((IPEndPoint)listener.LocalEndpoint).Port, now);
        LoginScreen screen = CreateFilledScreen(catalog);
        LoginAttemptCoordinator attempts = new(_folder, new FixedClock(now));
        using ClientSession session = new(new ClientRoleCreationTests.MissingContentSource(), new LogicalRenderSize(1024, 768),
            () => { }, () => Assert.Throws<ObjectDisposedException>(() => attempts.TryStart(screen, null)),
            canLogin => new ClientLogin(new ClientRoleCreationTests.MissingContentSource(), screen, attempts,
                new LoginRegistrationCoordinator(), LoginErrorTests.Messages(), null, canLogin, new FixedClock(now)));
        Assert.True(attempts.TryStart(screen, catalog));
        await accepted.Task.WaitAsync(budget.Token);
        Assert.Equal(ClientState.Login, session.State);

        session.Dispose();

        await peer.WaitAsync(budget.Token);
        Assert.Equal(ClientState.Closed, session.State);
        Assert.Throws<ObjectDisposedException>(() => screen.Advance(TimeSpan.Zero));
    }

    private static async Task ObservePendingAccountCloseAsync(TcpListener listener, TaskCompletionSource accepted, CancellationToken token)
    {
        using TcpClient peer = await listener.AcceptTcpClientAsync(token);
        accepted.SetResult();
        Assert.Equal(0, await peer.GetStream().ReadAsync(new byte[1], token));
    }

    [Fact]
    public async Task Session_ShutdownDuringRoleCreationClosesItsSocketAndUi()
    {
        await using SessionHarness harness = await OpenSessionAsync(creation: true);
        harness.Session.Update(TimeSpan.Zero, null, frameMilliseconds: 0);
        Assert.Equal(ClientState.RoleCreation, harness.Session.State);
        TestRoleCreationScreen screen = Assert.IsType<TestRoleCreationScreen>(harness.Role);

        harness.Session.Dispose();

        Assert.Equal(ClientState.Closed, harness.Session.State);
        Assert.Equal(1, screen.DisposeCalls);
        Assert.Throws<ObjectDisposedException>(() => harness.Login.Screen.Advance(TimeSpan.Zero));
        await harness.Peers.WaitAsync(harness.Token);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task Session_WorldCloseReturnsToLoginOnlyAfterCanceledLoadIsReaped(bool connectionLost, bool delayedLoad)
    {
        WorldCloseReason reason = connectionLost ? WorldCloseReason.ConnectionLost : WorldCloseReason.ContentUnavailable;
        await using SessionHarness harness = await OpenSessionAsync();
        Assert.Equal(ClientState.Login, harness.Session.State);
        harness.Session.Update(TimeSpan.Zero, null, frameMilliseconds: 0);
        Assert.Equal(ClientState.WorldLoading, harness.Session.State);
        TestClientWorld world = Assert.IsType<TestClientWorld>(harness.World);
        harness.Session.UpdatePresentation(TimeSpan.Zero);
        Assert.Equal(ClientState.WorldLoading, harness.Session.State);
        world.IsPresentable = true;
        harness.Session.UpdatePresentation(TimeSpan.Zero);
        Assert.Equal(ClientState.World, harness.Session.State);

        world.LoadReaped = !delayedLoad;
        world.BeginClose(reason);
        Assert.Equal(ClientState.ReturningToLogin, harness.Session.State);
        await world.ClosedTask.WaitAsync(harness.Token);
        harness.Session.Update(TimeSpan.Zero, null, frameMilliseconds: 0);
        Assert.Equal(delayedLoad ? ClientState.ReturningToLogin : ClientState.Login, harness.Session.State);
        Assert.Equal(reason == WorldCloseReason.ConnectionLost ? LoginFailureKind.Resource : LoginFailureKind.WorldContent,
            harness.Login.Attempts.Feedback.Failure!.Kind);
        world.LoadReaped = true;
        harness.Session.Update(TimeSpan.Zero, null, frameMilliseconds: 0);
        Assert.Equal(ClientState.Login, harness.Session.State);
        await harness.Peers.WaitAsync(harness.Token);
    }

    [Fact]
    public async Task Session_PresentableWorldStillWaitsForAuthoredLoadingGate()
    {
        LoginLoadingScreen loading = LoadingScreen();
        loading.Start();
        await using SessionHarness harness = await OpenSessionAsync(loading: loading);
        harness.Session.Update(TimeSpan.Zero, null, frameMilliseconds: 0);
        TestClientWorld world = Assert.IsType<TestClientWorld>(harness.World);
        for (int tick = 0; tick < 80; tick++)
        {
            harness.Session.UpdatePresentation(TimeSpan.FromMilliseconds(34));
        }
        Assert.Equal(36, loading.Sequence.Frame);
        Assert.Equal(ClientState.WorldLoading, harness.Session.State);
        world.IsPresentable = true;
        harness.Session.UpdatePresentation(TimeSpan.FromMilliseconds(34));
        Assert.Equal(ClientState.WorldLoading, harness.Session.State);
        for (int tick = 0; tick < 100; tick++)
        {
            harness.Session.UpdatePresentation(TimeSpan.FromMilliseconds(34));
        }
        Assert.Equal(99, loading.Sequence.Frame);
        Assert.Equal(ClientState.World, harness.Session.State);
        harness.Session.Dispose();
        await harness.Peers.WaitAsync(harness.Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Session_ShutdownClosesLoadingOrVisibleWorldBeforeWindow(bool visible)
    {
        await using SessionHarness harness = await OpenSessionAsync();
        harness.Session.Update(TimeSpan.Zero, null, frameMilliseconds: 0);
        TestClientWorld world = Assert.IsType<TestClientWorld>(harness.World);
        world.IsPresentable = visible;
        harness.Session.UpdatePresentation(TimeSpan.Zero);
        Assert.Equal(visible ? ClientState.World : ClientState.WorldLoading, harness.Session.State);
        harness.Session.Dispose();
        Assert.Equal(ClientState.Closed, harness.Session.State);
        Assert.Equal(["world.close", "window"], harness.CleanupOrder);
        harness.Session.Dispose();
        Assert.Equal(1, world.DisposeCalls);
        await harness.Peers.WaitAsync(harness.Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Session_RoleReturnWaitsForCloseAndPreservesConnectionLoss(bool connectionLost)
    {
        await using SessionHarness harness = await OpenSessionAsync(creation: true);
        harness.Session.Update(TimeSpan.Zero, null, frameMilliseconds: 0);
        Assert.Equal(ClientState.RoleCreation, harness.Session.State);
        TestRoleCreationScreen screen = Assert.IsType<TestRoleCreationScreen>(harness.Role);
        GameServerHandshakeSession game = Assert.IsType<GameServerHandshakeSession>(harness.Game);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? closing = null;
        screen.Close = () => new ValueTask(closing ??= CloseAfterReleaseAsync(game, release.Task, harness.Token));
        screen.ConnectionLost = connectionLost;
        try
        {
            screen.RequestReturn();
            harness.Session.Update(TimeSpan.Zero, null, frameMilliseconds: 0);
            Assert.Equal(ClientState.ReturningToLogin, harness.Session.State);
            harness.Session.Update(TimeSpan.Zero, null, frameMilliseconds: 0);
            Assert.Equal(ClientState.ReturningToLogin, harness.Session.State);
        }
        finally { release.TrySetResult(); }
        Assert.NotNull(closing);
        await closing.WaitAsync(harness.Token);
        harness.Session.Update(TimeSpan.Zero, null, frameMilliseconds: 0);
        Assert.Equal(ClientState.Login, harness.Session.State);
        Assert.Equal(connectionLost ? LoginAttemptStatus.Unavailable : LoginAttemptStatus.Idle, harness.Login.Attempts.Status);
        Assert.Equal(1, screen.DisposeCalls);
        await harness.Peers.WaitAsync(harness.Token);
    }

    [Fact]
    public async Task Session_MissingRoleContentClosesAcceptedGameAndReturnsToLogin()
    {
        await using SessionHarness harness = await OpenSessionAsync(creation: true);
        harness.RoleFailure = new FileNotFoundException();
        harness.Session.Update(TimeSpan.Zero, null, frameMilliseconds: 0);
        Assert.Equal(ClientState.ReturningToLogin, harness.Session.State);
        while (harness.Session.State != ClientState.Login)
        {
            harness.Session.Update(TimeSpan.Zero, null, frameMilliseconds: 0);
            await Task.Delay(1, harness.Token);
        }
        Assert.Equal(100116u, harness.Login.Attempts.Feedback.Failure!.Code);
        await harness.Peers.WaitAsync(harness.Token);
    }

    [Fact]
    public async Task Session_RoleAdmissionTransfersOriginalSocketThroughWorldLoadingAndWorld()
    {
        await using SessionHarness harness = await OpenSessionAsync(creation: true, enterCreatedWorld: true);
        harness.Session.Update(TimeSpan.Zero, null, frameMilliseconds: 0);
        Assert.Equal(ClientState.RoleCreation, harness.Session.State);
        TestRoleCreationScreen screen = Assert.IsType<TestRoleCreationScreen>(harness.Role);
        GameServerHandshakeSession game = Assert.IsType<GameServerHandshakeSession>(harness.Game);
        await game.SendCharacterCreationAsync("Hero"u8.ToArray(), 1003, 10, TimeSpan.FromSeconds(5), harness.Token);
        Assert.Equal(GameCharacterCreationKind.Created,
            (await game.ReadCharacterCreationResultAsync(TimeSpan.FromSeconds(5), harness.Token)).Kind);
        await game.SendRepeatedLoginProofAsync(TimeSpan.FromSeconds(5), harness.Token);
        Assert.Equal(GameLoginControlKind.ExistingCharacter,
            (await game.ReadLoginControlAsync(TimeSpan.FromSeconds(5), harness.Token)).Kind);
        _ = await game.ReadExistingCharacterPreludeAsync(TimeSpan.FromSeconds(5), harness.Token);
        GameCharacterInfo1006 character = await game.ReadCharacterInfoAsync(TimeSpan.FromSeconds(5), harness.Token);
        screen.AdmittedSession = game;
        screen.AdmittedWorld = await GameWorldEntryBootstrap.RunAsync(game, character, cancellationToken: harness.Token);

        harness.Session.Update(TimeSpan.Zero, null, frameMilliseconds: 0);
        Assert.Equal(ClientState.WorldLoading, harness.Session.State);
        Assert.Equal(1, screen.DisposeCalls);
        TestClientWorld world = Assert.IsType<TestClientWorld>(harness.World);
        world.IsPresentable = true;
        harness.Session.UpdatePresentation(TimeSpan.Zero);
        Assert.Equal(ClientState.World, harness.Session.State);
        Task<GameSecuredInboundFrame?> echoed = game.ReadWorldFrameAsync(harness.Token);
        await game.SendWorldWalkAsync(character.EntityId, 6, 0x01020304, 0x05060708, harness.Token);
        using GameSecuredInboundFrame walk = await echoed.WaitAsync(harness.Token) ?? throw new EndOfStreamException();
        Assert.Equal(GameWalk10005.PacketLength, walk.Packet.Length);
        GameWalk10005 confirmed = GameWalk10005.Read(GameMessages.Load(walk.Packet.ToArray()));
        Assert.Equal(character.EntityId, confirmed.EntityId);
        harness.Session.Dispose();
        await harness.Peers.WaitAsync(harness.Token);
    }

    [Fact]
    public async Task Session_FailedWorldFactoryStillClosesTransferredSocket()
    {
        await using SessionHarness harness = await OpenSessionAsync();
        InvalidOperationException failure = new("World factory failed.");
        harness.WorldFailure = failure;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => harness.Session.Update(TimeSpan.Zero, null, frameMilliseconds: 0)));
        Assert.Equal(ClientState.ReturningToLogin, harness.Session.State);
        harness.Session.Dispose();
        Assert.Equal(ClientState.Closed, harness.Session.State);
        await harness.Peers.WaitAsync(harness.Token);
    }

    [Fact]
    public async Task Session_ShutdownRetainsWorldAndWindowFailuresAndReleasesLogin()
    {
        await using SessionHarness harness = await OpenSessionAsync();
        harness.Session.Update(TimeSpan.Zero, null, frameMilliseconds: 0);
        TestClientWorld world = Assert.IsType<TestClientWorld>(harness.World);
        IOException first = new("World cleanup failed.");
        IOException second = new("Window cleanup failed.");
        Func<ValueTask> close = world.Close;
        world.Close = async () => { await close(); throw first; };
        harness.WindowFailure = second;
        AggregateException combined = Assert.Throws<AggregateException>(harness.Session.Dispose);
        Assert.Equal([first, second], combined.InnerExceptions);
        Assert.Equal(["world.close", "window"], harness.CleanupOrder);
        Assert.Equal(ClientState.Closed, harness.Session.State);
        Assert.Throws<ObjectDisposedException>(() => harness.Login.Screen.Advance(TimeSpan.Zero));
        await harness.Peers.WaitAsync(harness.Token);
    }

    private async Task<SessionHarness> OpenSessionAsync(bool creation = false, bool enterCreatedWorld = false, LoginLoadingScreen? loading = null)
    {
        SessionHarness harness = new();
        using TcpListener accounts = new(IPAddress.Loopback, 0);
        using TcpListener games = new(IPAddress.Loopback, 0);
        accounts.Start();
        games.Start();
        Task accountPeer = RedirectAccountAsync(accounts, ((IPEndPoint)games.LocalEndpoint).Port, harness.Token);
        Task gamePeer = creation
            ? Networking.GameServerHandshakeSessionTests.CoordinatorCreationPeerAsync(games, enterCreatedWorld, harness.Token)
            : Networking.GameServerHandshakeSessionTests.AcceptedCoordinatorWorldPeerAsync(games, harness.Token);
        harness.Peers = Task.WhenAll(accountPeer, gamePeer);
        DateTimeOffset now = new(2026, 10, 17, 22, 0, 0, TimeSpan.Zero);
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = CreateSignedLocalCatalog(signer, ((IPEndPoint)accounts.LocalEndpoint).Port, now);
        LoginScreen screen = CreateFilledScreen(catalog);
        LoginAttemptCoordinator attempts = new(_folder, new FixedClock(now));
        try
        {
            // The pinned creation proof carries resource version 5517, including on its repeated proof.
            File.WriteAllText(Path.Combine(_folder, "res.dat"), "5517");
            Assert.True(attempts.TryStart(screen, catalog));
            await CollectAsync(attempts, catalog);
            if (harness.Peers.IsFaulted)
            {
                await harness.Peers;
            }
            Assert.Equal(creation ? LoginAttemptStatus.CharacterCreation : LoginAttemptStatus.CharacterReceived, attempts.Status);
            harness.Session = new ClientSession(new ClientRoleCreationTests.MissingContentSource(), new LogicalRenderSize(1024, 768),
                () => { }, () =>
                {
                    harness.CleanupOrder.Add("window");
                    Assert.Equal(ClientState.Closing, harness.Session.State);
                    if (harness.WindowFailure is { } failure)
                    {
                        throw failure;
                    }
                }, canLogin => harness.Login = new ClientLogin(new ClientRoleCreationTests.MissingContentSource(), screen, attempts,
                    new LoginRegistrationCoordinator(), LoginErrorTests.Messages(), loading, canLogin, new FixedClock(now)),
                (game, _) =>
                {
                    harness.Game = game;
                    if (harness.RoleFailure is { } failure)
                    {
                        throw failure;
                    }
                    TestRoleCreationScreen role = new();
                    role.Close = () => role.AdmittedWorld is null ? game.DisposeAsync() : ValueTask.CompletedTask;
                    harness.Role = role;
                    return role;
                }, (game, _, closing) =>
                {
                    harness.Game = game;
                    if (harness.WorldFailure is { } failure)
                    {
                        throw failure;
                    }
                    harness.World = new TestClientWorld(closing, () => game.DisposeAsync(), harness.CleanupOrder);
                    return harness.World;
                });
            return harness;
        }
        catch
        {
            // Failed fixture setup still owns the attempt and its accepted socket.
            await attempts.DisposeAsync();
            screen.Dispose();
            await harness.DisposeAsync();
            throw;
        }
    }

    private static async Task CloseAfterReleaseAsync(GameServerHandshakeSession game, Task release, CancellationToken token)
    {
        await release.WaitAsync(token);
        await game.DisposeAsync();
    }

    private static LoginLoadingScreen LoadingScreen()
    {
        List<object[]> events = [];
        for (int frame = 18; frame <= 100; frame++)
        {
            events.Add([frame, new { depth = 4, m = s_loadingMatrix }]);
            events.Add([frame, new { depth = 9, m = s_loadingMatrix }]);
        }
        byte[] timeline = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object> { ["394"] = new { frames = 100, events } });
        return new LoginLoadingScreen(LoginLoadingTimeline.Parse(timeline), "Account", "Game", "Initializing", "Map");
    }

    private sealed class SessionHarness : IAsyncDisposable
    {
        private readonly CancellationTokenSource _budget = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        public SessionHarness() => _budget.CancelAfter(TimeSpan.FromSeconds(15));
        public CancellationToken Token => _budget.Token;
        private ClientSession? _session;
        private ClientLogin? _login;
        public ClientSession Session
        {
            get => _session ?? throw new InvalidOperationException(); set => _session = value;
        }
        public ClientLogin Login
        {
            get => _login ?? throw new InvalidOperationException(); set => _login = value;
        }
        public Task Peers { get; set; } = Task.CompletedTask;
        public GameServerHandshakeSession? Game
        {
            get; set;
        }
        public TestRoleCreationScreen? Role
        {
            get; set;
        }
        public TestClientWorld? World
        {
            get; set;
        }
        public Exception? RoleFailure
        {
            get; set;
        }
        public Exception? WorldFailure
        {
            get; set;
        }
        public Exception? WindowFailure
        {
            get; set;
        }
        public List<string> CleanupOrder { get; } = [];
        public async ValueTask DisposeAsync()
        {
            try
            {
                _session?.Dispose();
            }
            finally
            {
                await _budget.CancelAsync();
                await Peers.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                _budget.Dispose();
            }
        }
    }
}

internal sealed class TestClientWorld(Action closing, Func<ValueTask> close, List<string> order) : IClientWorld
{
    private Task? _closed;
    private bool _observed;
    private WorldCloseReason _reason;
    public bool IsActive { get; private set; } = true;
    public bool HasScene => IsPresentable;
    public bool IsPresentable
    {
        get; set;
    }
    public bool IsClosed => _observed && LoadReaped;
    public bool LoadReaped { get; set; } = true;
    public Func<ValueTask> Close { get; set; } = close;
    public Task ClosedTask => _closed ?? Task.CompletedTask;
    public int DisposeCalls
    {
        get; private set;
    }
    public void BeginClose(WorldCloseReason reason)
    {
        _reason = reason;
        IsActive = false;
        closing();
        _closed = Close().AsTask();
    }
    public bool TryObserveClose(out WorldCloseReason reason)
    {
        reason = _reason;
        if (_observed || _closed is not { IsCompleted: true } closed)
        {
            return false;
        }
        closed.GetAwaiter().GetResult();
        _observed = true;
        return true;
    }
    public ValueTask DisposeAsync()
    {
        DisposeCalls++;
        order.Add("world.close");
        return new ValueTask(_closed ??= Close().AsTask());
    }
    public void DrainIngress(uint frameMilliseconds)
    {
    }
    public void CompleteLoad()
    {
    }
    public void RefreshAppearance(OpenGLGraphicsDevice? device)
    {
    }
    public void PrepareFirstTextures(DesktopWindow window, OpenGLGraphicsDevice device, OpenGLRenderer fallback) => throw new NotSupportedException();
    public OpenGLRenderer GetRenderer(DesktopWindow window, OpenGLGraphicsDevice? device, OpenGLRenderer fallback) => throw new NotSupportedException();
    public void PrepareFrame(OpenGLGraphicsDevice device, OpenGLRenderer renderer) => throw new NotSupportedException();
    public void DrawFrame(OpenGLRenderer renderer, DesktopWindow window) => throw new NotSupportedException();
    public void HandlePointerMoved(PixelPoint point) => throw new NotSupportedException();
    public void HandlePointerPressed(PixelPoint point) => throw new NotSupportedException();
    public void HandlePointerReleased(PixelPoint point) => throw new NotSupportedException();
    public void ResizeFramebuffer(PixelSize size)
    {
    }
    public void ReleaseGraphics()
    {
    }
}
