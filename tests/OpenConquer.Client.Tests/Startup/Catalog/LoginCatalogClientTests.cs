using System.Net;
using System.Security.Cryptography;
using OpenConquer.Client.Startup.Catalog;

namespace OpenConquer.Client.Tests.Startup.Catalog;

public sealed class LoginCatalogClientTests
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 11, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FetchAsync_AuthenticatesBothResponsesAndParsesTrustedEndpoints()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] payload = SignedLoginCatalogTests.ValidPayload();
        byte[] signature = SignedLoginCatalogTests.Sign(signer, payload);
        LoginCatalogAuthority authority = SignedLoginCatalogTests.Authority(signer);
        using LoginCatalogClient client = new(new TestHandler((request) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new ByteArrayContent(request.RequestUri == authority.CatalogUri ? payload : signature),
        }));

        SignedLoginCatalog? result = await client.FetchAsync(authority, s_now, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Single(result.View.Regions);
    }

    [Theory]
    [InlineData(HttpStatusCode.Redirect)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task FetchAsync_RejectsNonSuccessWithoutFallback(HttpStatusCode status)
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        LoginCatalogAuthority authority = SignedLoginCatalogTests.Authority(signer);
        using LoginCatalogClient client = new(new TestHandler(request => new HttpResponseMessage(status)
        {
            RequestMessage = request,
            Content = new ByteArrayContent(SignedLoginCatalogTests.ValidPayload()),
        }));

        Assert.Null(await client.FetchAsync(authority, s_now, CancellationToken.None));
    }

    [Fact]
    public async Task FetchAsync_RejectsOversizedAndCompressedResponses()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        LoginCatalogAuthority authority = SignedLoginCatalogTests.Authority(signer);
        using LoginCatalogClient oversized = new(new TestHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new ByteArrayContent(new byte[SignedLoginCatalog.MaximumCatalogBytes + 1]),
        }));

        Assert.Null(await oversized.FetchAsync(authority, s_now, CancellationToken.None));

        using LoginCatalogClient compressed = new(new TestHandler(request =>
        {
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(SignedLoginCatalogTests.ValidPayload()),
            };
            response.Content.Headers.ContentEncoding.Add("gzip");
            return response;
        }));

        Assert.Null(await compressed.FetchAsync(authority, s_now, CancellationToken.None));
    }

    private sealed class TestHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
