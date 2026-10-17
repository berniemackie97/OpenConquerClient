using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using OpenConquer.Client.Startup.Catalog;
using OpenConquer.Client.UI.Login.Selection;
using OpenConquer.Networking.Authentication;

namespace OpenConquer.Client.Tests.Startup.Catalog;

public sealed class LoginSignedAccountSessionTests
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 17, 22, 0, 0, TimeSpan.Zero);
    private static readonly LoginAccountTimeouts s_timeouts =
        new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

    [Fact]
    public void NoCatalogNoSelectionAndExpiredAuthorityNeverOpenSockets()
    {
        LoginServerSelection selection = new();
        Assert.Throws<InvalidOperationException>(() =>
            LoginSignedAccountSession.OpenAsync(null, selection, s_now, s_timeouts));

        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog signed = CreateCatalog(signer, "127.0.0.1", 9958);
        Assert.Throws<InvalidOperationException>(() =>
            LoginSignedAccountSession.OpenAsync(signed, selection, s_now, s_timeouts));

        selection.ReplaceCatalog(signed.View);
        selection.SelectRegionSlot(0);
        selection.SelectServerSlot(0);
        Assert.Throws<InvalidOperationException>(() =>
            LoginSignedAccountSession.OpenAsync(signed, selection, s_now.AddHours(2), s_timeouts));

        LoginServerSelection unrelated = new();
        unrelated.ReplaceCatalog(new LoginServerCatalog(signed.View.Regions));
        unrelated.SelectRegionSlot(0);
        unrelated.SelectServerSlot(0);
        Assert.Throws<InvalidOperationException>(() =>
            LoginSignedAccountSession.OpenAsync(signed, unrelated, s_now, s_timeouts));
    }

    [Fact]
    public async Task CurrentSignedSelectionOpensExactlyTheSelectedAccountEndpoint()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog signed = CreateCatalog(signer, "127.0.0.1", port);
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(signed.View);
        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(0));

        Task peer = SendSeedAsync(listener);
        await using (LoginAccountSession session = await LoginSignedAccountSession.OpenAsync(
                         signed, selection, s_now, s_timeouts))
        {
            Assert.Equal(0x12345678u, session.Seed);
        }

        await peer.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static SignedLoginCatalog CreateCatalog(ECDsa signer, string host, int port)
    {
        byte[] payload = Encoding.UTF8.GetBytes(
            $"{{\"schemaVersion\":1,\"productId\":\"OpenConquer\",\"sequence\":7," +
            $"\"issuedAt\":\"2026-10-17T21:00:00Z\",\"expiresAt\":\"2026-10-17T23:00:00Z\"," +
            $"\"regions\":[{{\"id\":\"north\",\"name\":\"North\",\"servers\":[" +
            $"{{\"id\":\"realm-1\",\"name\":\"Realm One\",\"protocolServerName\":\"NativeRealm1\"," +
            $"\"accountHost\":\"{host}\",\"accountPort\":{port}}}]}}]}}");
        byte[] signature = signer.SignData(payload, HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return SignedLoginCatalog.Read(payload, signature, SignedLoginCatalogTests.Authority(signer), s_now);
    }

    private static async Task SendSeedAsync(TcpListener listener)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync();
        await client.GetStream().WriteAsync(Convert.FromHexString("C54869128E317C0F"));
    }
}
