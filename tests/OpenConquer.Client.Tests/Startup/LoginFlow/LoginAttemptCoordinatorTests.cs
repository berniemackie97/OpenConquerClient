using OpenConquer.Client.Startup.LoginFlow;
using OpenConquer.Client.UI.Login;
using OpenConquer.Client.UI.Login.Account;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.Startup.LoginFlow;

public sealed class LoginAttemptCoordinatorTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("oc-login-flow-").FullName;

    [Fact]
    public async Task NoSignedRealmCannotStartOrConsumeCredentials()
    {
        using LoginScreen login = new(LoginStageLayout.Create(new LogicalRenderSize(1024, 768)),
            new LoginAccountHistory(_folder), "5517", _ => { });
        await using LoginAttemptCoordinator attempts = new(_folder);
        Assert.False(attempts.TryStart(login, null));
        Assert.Equal(LoginAttemptStatus.SelectRealm, attempts.Status);
        attempts.Update(null);
        Assert.Equal(LoginAttemptStatus.SelectRealm, attempts.Status);
    }

    [Fact]
    public async Task DisposedAttemptCoordinatorRejectsNewSubmissions()
    {
        using LoginScreen login = new(LoginStageLayout.Create(new LogicalRenderSize(1024, 768)),
            new LoginAccountHistory(_folder), "5517", _ => { });
        LoginAttemptCoordinator attempts = new(_folder);
        await attempts.DisposeAsync();
        await attempts.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => attempts.TryStart(login, null));
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
