using OpenConquer.Client.Startup.Catalog;
using OpenConquer.Client.Startup.LoginFlow;
using OpenConquer.Client.Tests.UI.Login;
using OpenConquer.Client.Tests.UI.Role;
using OpenConquer.Client.UI.Login;
using OpenConquer.Client.UI.Login.Account;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests;

public sealed class ClientSessionTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("oc-session-").FullName;

    [Fact]
    public void DisposeBeforeRun_IsIdempotentAndRejectsRun()
    {
        ClientApplication application = new(_folder);
        application.Dispose();
        application.Dispose();
        Assert.Throws<ObjectDisposedException>(() => application.Run());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShutdownBeforeServicesStart_DisposesOwnersEvenWhenWindowCloseFails(bool fail)
    {
        ClientLogin? login = null;
        ClientSession? session = null;
        int windows = 0;
        IOException failure = new("Window release failed.");
        session = new ClientSession(new ClientRoleCreationTests.MissingContentSource(), new LogicalRenderSize(1024, 768),
            () => { }, () =>
            {
                windows++;
                Assert.Equal(ClientState.Closing, session!.State);
                session.Dispose(); // Reentrant window callbacks cannot start another shutdown.
                if (fail)
                {
                    throw failure;
                }
            }, canLogin => login = CreateLogin(canLogin));
        Assert.Equal(ClientState.Login, session.State);
        if (fail)
        {
            Assert.Same(failure, Assert.Throws<IOException>(session.Dispose));
        }
        else
        {
            session.Dispose();
        }
        Assert.Equal(ClientState.Closed, session.State);
        Assert.Equal(1, windows);
        Assert.NotNull(login);
        Assert.Throws<ObjectDisposedException>(() => login.Screen.Advance(TimeSpan.Zero));
        Assert.Throws<ObjectDisposedException>(() => login.Attempts.TryStart(login.Screen, null));
        Assert.Throws<ObjectDisposedException>(() => login.Registration.Open(login.Screen.Selection, null));
        Assert.Throws<ObjectDisposedException>(() => session.Update(TimeSpan.Zero, null));
        session.Dispose();
        Assert.Equal(1, windows);
    }

    [Fact]
    public void Shutdown_CancelsPendingCatalogBeforeWindowAndDisposesLoginUi()
    {
        bool canceled = false;
        using LoginCatalogRefresh refresh = new(token =>
        {
            token.Register(() => canceled = true);
            return WaitForCancellationAsync(token);
        });
        ClientLogin? login = null;
        using ClientSession session = new(new ClientRoleCreationTests.MissingContentSource(), new LogicalRenderSize(1024, 768),
            () => { }, () => Assert.True(canceled),
            canLogin => login = CreateLogin(canLogin, new LoginCatalogBinding(refresh)));
        session.Update(TimeSpan.Zero, null);

        session.Dispose();

        Assert.True(canceled);
        Assert.Equal(ClientState.Closed, session.State);
        Assert.NotNull(login);
        Assert.Throws<ObjectDisposedException>(() => login.Screen.Advance(TimeSpan.Zero));
    }

    [Fact]
    public void Cleanup_ContinuesInOrderAndRetainsAllFailures()
    {
        List<int> order = [];
        IOException first = new("first");
        InvalidOperationException second = new("second");
        ClientShutdown cleanup = new();
        cleanup.Run(() => { order.Add(1); throw first; });
        cleanup.Run(() => order.Add(2));
        cleanup.Run(() => { order.Add(3); throw second; });
        cleanup.Run(() => order.Add(4));

        AggregateException combined = Assert.Throws<AggregateException>(cleanup.ThrowIfFailed);

        Assert.Equal([1, 2, 3, 4], order);
        Assert.Equal([first, second], combined.InnerExceptions);
    }

    private ClientLogin CreateLogin(Func<bool> canLogin, LoginCatalogBinding? catalog = null) => new(
        new ClientRoleCreationTests.MissingContentSource(),
        new LoginScreen(LoginStageLayout.Create(new LogicalRenderSize(1024, 768)), new LoginAccountHistory(_folder), "5517", _ => { }),
        new LoginAttemptCoordinator(_folder), new LoginRegistrationCoordinator(), LoginErrorTests.Messages(), null, canLogin, catalog: catalog);

    private static async Task<SignedLoginCatalog?> WaitForCancellationAsync(CancellationToken token)
    {
        await Task.Delay(Timeout.Infinite, token);
        return null;
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
