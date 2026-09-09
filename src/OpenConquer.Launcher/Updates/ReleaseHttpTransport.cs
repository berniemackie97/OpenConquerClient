using System.Buffers;
using System.Net;
using System.Security.Cryptography;
using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher.Updates;

internal enum ReleaseTransferIssue
{
    Unavailable,
    InvalidResponse,
    ContentTooLarge,
    IntegrityFailure,
    FileSystemFailure,
}

internal abstract record ReleaseTransferResult<T>
{
    private ReleaseTransferResult()
    {
    }

    internal sealed record Downloaded(T Value) : ReleaseTransferResult<T>;

    internal sealed record Rejected(ReleaseTransferIssue Issue) : ReleaseTransferResult<T>;
}

/// <summary>Performs bounded HTTPS transfers with an explicitly owned HTTP lifetime.</summary>
internal sealed class ReleaseHttpTransport : IDisposable
{
    private const int BufferSize = 128 * 1024;
    private static readonly TimeSpan s_defaultNetworkIdleTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan s_defaultMetadataOperationTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan s_defaultPackageOperationTimeout = TimeSpan.FromHours(12);
    private static readonly UnixFileMode s_privateFileMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly HttpClient _httpClient;
    private readonly TimeSpan _networkIdleTimeout;
    private readonly TimeSpan _metadataOperationTimeout;
    private readonly TimeSpan _packageOperationTimeout;

    public ReleaseHttpTransport()
        : this(CreateHandler(), s_defaultNetworkIdleTimeout,
            s_defaultMetadataOperationTimeout, s_defaultPackageOperationTimeout)
    {
    }

    internal ReleaseHttpTransport(HttpMessageHandler handler)
        : this(handler, s_defaultNetworkIdleTimeout,
            s_defaultMetadataOperationTimeout, s_defaultPackageOperationTimeout)
    {
    }

    internal ReleaseHttpTransport(
        HttpMessageHandler handler,
        TimeSpan networkIdleTimeout,
        TimeSpan metadataOperationTimeout,
        TimeSpan packageOperationTimeout)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (networkIdleTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(networkIdleTimeout),
                "The release network-idle timeout must be positive.");
        }

        if (metadataOperationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(metadataOperationTimeout),
                "The release metadata timeout must be positive.");
        }

        if (packageOperationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(packageOperationTimeout),
                "The release package timeout must be positive.");
        }

        _networkIdleTimeout = networkIdleTimeout;
        _metadataOperationTimeout = metadataOperationTimeout;
        _packageOperationTimeout = packageOperationTimeout;
        _httpClient = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"OpenConquer.Launcher/{ManagedReleaseManifest.CurrentLauncherVersion}");
    }

    public void Dispose() => _httpClient.Dispose();

    public async Task<ReleaseTransferResult<byte[]>> DownloadBytesAsync(
        Uri uri,
        int maximumLength,
        CancellationToken cancellationToken)
    {
        RequireUri(uri);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLength);
        if (maximumLength > ReleaseCatalog.MaximumLength)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLength),
                "Release metadata exceeds the supported bound.");
        }

        using CancellationTokenSource operationTimeout =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operationTimeout.CancelAfter(_metadataOperationTimeout);
        CancellationToken operationToken = operationTimeout.Token;
        try
        {
            using HttpResponseMessage response = await SendAsync(uri, operationToken)
                .ConfigureAwait(false);
            ReleaseTransferIssue? rejection = ValidateResponse(response, uri, maximumLength);
            if (rejection is not null)
            {
                return new ReleaseTransferResult<byte[]>.Rejected(rejection.Value);
            }

            await using Stream source = await response.Content.ReadAsStreamAsync(operationToken)
                .ConfigureAwait(false);
            using MemoryStream destination = response.Content.Headers.ContentLength is long length
                ? new MemoryStream((int)length)
                : new MemoryStream();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(Math.Min(BufferSize, maximumLength));
            try
            {
                while (destination.Length <= maximumLength)
                {
                    int available = destination.Length == maximumLength
                        ? 1
                        : Math.Min(buffer.Length, maximumLength - (int)destination.Length);
                    int read = await ReadWithIdleTimeoutAsync(source,
                        buffer.AsMemory(0, available), operationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read), operationToken)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            if (destination.Length is 0 || destination.Length > maximumLength)
            {
                return new ReleaseTransferResult<byte[]>.Rejected(
                    ReleaseTransferIssue.ContentTooLarge);
            }

            return new ReleaseTransferResult<byte[]>.Downloaded(destination.ToArray());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or
            OperationCanceledException)
        {
            return new ReleaseTransferResult<byte[]>.Rejected(
                ReleaseTransferIssue.Unavailable);
        }
    }

    public async Task<ReleaseTransferResult<string>> DownloadFileAsync(
        Uri uri,
        string destinationPath,
        long expectedLength,
        ReadOnlyMemory<byte> expectedSha256,
        CancellationToken cancellationToken)
    {
        RequireUri(uri);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        if (!Path.IsPathFullyQualified(destinationPath))
        {
            throw new ArgumentException("The download destination must be fully qualified.",
                nameof(destinationPath));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedLength);
        if (expectedLength > ReleaseCatalog.MaximumPackageLength)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedLength),
                "The expected package length exceeds the supported bound.");
        }

        if (expectedSha256.Length != SHA256.HashSizeInBytes)
        {
            throw new ArgumentException("The expected package digest is invalid.",
                nameof(expectedSha256));
        }

        using CancellationTokenSource operationTimeout =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operationTimeout.CancelAfter(_packageOperationTimeout);
        CancellationToken operationToken = operationTimeout.Token;
        bool destinationCreated = false;
        bool completed = false;
        try
        {
            using HttpResponseMessage response = await SendAsync(uri, operationToken)
                .ConfigureAwait(false);
            ReleaseTransferIssue? rejection = ValidateResponse(response, uri, expectedLength);
            if (rejection is not null)
            {
                return new ReleaseTransferResult<string>.Rejected(rejection.Value);
            }

            if (response.Content.Headers.ContentLength is long contentLength &&
                contentLength != expectedLength)
            {
                return new ReleaseTransferResult<string>.Rejected(
                    ReleaseTransferIssue.IntegrityFailure);
            }

            FileStreamOptions options = new()
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = BufferSize,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = s_privateFileMode;
            }

            {
                await using Stream source = await response.Content.ReadAsStreamAsync(
                    operationToken).ConfigureAwait(false);
                await using FileStream destination = new(destinationPath, options);
                destinationCreated = true;
                using IncrementalHash hash = IncrementalHash.CreateHash(
                    HashAlgorithmName.SHA256);
                byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
                try
                {
                    long remaining = expectedLength;
                    while (remaining > 0)
                    {
                        int read = await ReadWithIdleTimeoutAsync(source, buffer.AsMemory(0,
                            (int)Math.Min(buffer.Length, remaining)), operationToken)
                            .ConfigureAwait(false);
                        if (read == 0)
                        {
                            return new ReleaseTransferResult<string>.Rejected(
                                ReleaseTransferIssue.IntegrityFailure);
                        }

                        hash.AppendData(buffer, 0, read);
                        await destination.WriteAsync(buffer.AsMemory(0, read), operationToken)
                            .ConfigureAwait(false);
                        remaining -= read;
                    }

                    if (await ReadWithIdleTimeoutAsync(source, buffer.AsMemory(0, 1),
                            operationToken)
                            .ConfigureAwait(false) != 0)
                    {
                        return new ReleaseTransferResult<string>.Rejected(
                            ReleaseTransferIssue.IntegrityFailure);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                byte[] actualHash = hash.GetHashAndReset();
                if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedSha256.Span))
                {
                    return new ReleaseTransferResult<string>.Rejected(
                        ReleaseTransferIssue.IntegrityFailure);
                }

                await destination.FlushAsync(operationToken).ConfigureAwait(false);
                destination.Flush(flushToDisk: true);
            }

            completed = true;
            return new ReleaseTransferResult<string>.Downloaded(destinationPath);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or
            OperationCanceledException)
        {
            return new ReleaseTransferResult<string>.Rejected(ReleaseTransferIssue.Unavailable);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ReleaseTransferResult<string>.Rejected(
                ReleaseTransferIssue.FileSystemFailure);
        }
        finally
        {
            if (destinationCreated && !completed)
            {
                TryDelete(destinationPath);
            }
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        request.Headers.AcceptEncoding.Clear();
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(_networkIdleTimeout);
        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
            timeout.Token).ConfigureAwait(false);
    }

    private async ValueTask<int> ReadWithIdleTimeoutAsync(
        Stream source,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(_networkIdleTimeout);
        return await source.ReadAsync(buffer, timeout.Token).ConfigureAwait(false);
    }

    private static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        ConnectTimeout = TimeSpan.FromSeconds(15),
        Credentials = null,
        DefaultProxyCredentials = null,
        MaxConnectionsPerServer = 2,
        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        UseCookies = false,
    };

    private static ReleaseTransferIssue? ValidateResponse(
        HttpResponseMessage response,
        Uri expectedUri,
        long maximumLength)
    {
        if (response.StatusCode != HttpStatusCode.OK ||
            response.RequestMessage?.RequestUri is not Uri actualUri ||
            !ReleaseUriPolicy.EqualsExact(expectedUri, actualUri) ||
            response.Content.Headers.ContentEncoding.Count != 0)
        {
            return ReleaseTransferIssue.InvalidResponse;
        }

        long? contentLength = response.Content.Headers.ContentLength;
        return contentLength is <= 0 || contentLength > maximumLength
            ? ReleaseTransferIssue.ContentTooLarge
            : null;
    }

    private static void RequireUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!ReleaseUriPolicy.IsValidHttps(uri))
        {
            throw new ArgumentException("Release transfers require an absolute HTTPS URI.",
                nameof(uri));
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
