using System.Net;

namespace OpenConquer.Client.Startup.Catalog;

/// <summary>Fetches the signed catalog over HTTPS with size limits, without cookies or default credentials.</summary>
internal sealed class LoginCatalogClient : IDisposable
{
    private static readonly TimeSpan s_operationTimeout = TimeSpan.FromSeconds(12);
    private readonly HttpClient _client;
    private bool _disposed;

    public LoginCatalogClient() : this(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        ConnectTimeout = TimeSpan.FromSeconds(5),
        Credentials = null,
        DefaultProxyCredentials = null,
        UseCookies = false,
        MaxConnectionsPerServer = 2,
    })
    {
    }

    internal LoginCatalogClient(HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _client = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<SignedLoginCatalog?> FetchAsync(LoginCatalogAuthority authority, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(authority);
        cancellationToken.ThrowIfCancellationRequested();

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(s_operationTimeout);

        try
        {
            byte[]? payload = await DownloadAsync(authority.CatalogUri, SignedLoginCatalog.MaximumCatalogBytes, timeout.Token).ConfigureAwait(false);

            if (payload is null)
            {
                return null;
            }

            byte[]? signature = await DownloadAsync(authority.SignatureUri, SignedLoginCatalog.SignatureBytes, timeout.Token).ConfigureAwait(false);

            if (signature is null)
            {
                return null;
            }

            return SignedLoginCatalog.Read(payload, signature, authority, now);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException or InvalidDataException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _client.Dispose();
    }

    private async Task<byte[]?> DownloadAsync(Uri uri, int maximumLength, CancellationToken token)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        request.Headers.AcceptEncoding.Clear();
        using HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.OK || response.RequestMessage?.RequestUri != uri ||
            response.Content.Headers.ContentEncoding.Count != 0 ||
            response.Content.Headers.ContentLength is <= 0 or > 131072 ||
            response.Content.Headers.ContentLength > maximumLength)
        {
            return null;
        }

        await using Stream source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using MemoryStream output = new();
        byte[] buffer = new byte[4096];

        while (true)
        {
            int count = await source.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, maximumLength + 1 - (int)output.Length)), token).ConfigureAwait(false);

            if (count == 0)
            {
                break;
            }

            output.Write(buffer, 0, count);

            if (output.Length > maximumLength)
            {
                return null;
            }
        }

        return output.Length is 0 ? null : output.ToArray();
    }
}
