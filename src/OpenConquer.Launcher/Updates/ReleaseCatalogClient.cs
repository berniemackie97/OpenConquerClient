namespace OpenConquer.Launcher.Updates;

internal sealed class ReleaseCatalogLocation
{
    public ReleaseCatalogLocation(Uri catalogUri, Uri signatureUri)
    {
        ArgumentNullException.ThrowIfNull(catalogUri);
        ArgumentNullException.ThrowIfNull(signatureUri);
        if (!ReleaseUriPolicy.IsValidHttps(catalogUri) ||
            !ReleaseUriPolicy.IsValidHttps(signatureUri) ||
            ReleaseUriPolicy.EqualsExact(catalogUri, signatureUri))
        {
            throw new ArgumentException(
                "Release catalog locations must be distinct absolute HTTPS URIs.");
        }

        CatalogUri = catalogUri;
        SignatureUri = signatureUri;
    }

    public Uri CatalogUri
    {
        get;
    }

    public Uri SignatureUri
    {
        get;
    }
}

internal abstract record ReleaseCatalogFetchResult
{
    private ReleaseCatalogFetchResult()
    {
    }

    internal sealed record Selected(ReleaseCatalogEntry Release) : ReleaseCatalogFetchResult;

    internal sealed record Rejected(
        ReleaseTransferIssue? TransferIssue,
        ReleaseCatalogIssue? CatalogIssue) : ReleaseCatalogFetchResult;
}

/// <summary>Retrieves and authenticates the configured release catalog and detached signature.</summary>
internal sealed class ReleaseCatalogClient
{
    private const int MaximumSignatureLength = 4 * 1024;

    private readonly ReleaseCatalogLocation _location;
    private readonly ReleaseHttpTransport _transport;
    private readonly ReleaseCatalog _catalog;

    public ReleaseCatalogClient(
        ReleaseCatalogLocation location,
        ReleaseHttpTransport transport,
        ReleaseCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(catalog);
        _location = location;
        _transport = transport;
        _catalog = catalog;
    }

    public async Task<ReleaseCatalogFetchResult> FetchLatestAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_catalog.IsAuthorityConfigured)
        {
            return new ReleaseCatalogFetchResult.Rejected(TransferIssue: null,
                ReleaseCatalogIssue.AuthorityUnavailable);
        }

        ReleaseTransferResult<byte[]> catalogTransfer = await _transport.DownloadBytesAsync(
            _location.CatalogUri, ReleaseCatalog.MaximumLength, cancellationToken)
            .ConfigureAwait(false);
        if (catalogTransfer is ReleaseTransferResult<byte[]>.Rejected catalogRejected)
        {
            return new ReleaseCatalogFetchResult.Rejected(catalogRejected.Issue,
                CatalogIssue: null);
        }

        ReleaseTransferResult<byte[]> signatureTransfer = await _transport.DownloadBytesAsync(
            _location.SignatureUri, MaximumSignatureLength, cancellationToken)
            .ConfigureAwait(false);
        if (signatureTransfer is ReleaseTransferResult<byte[]>.Rejected signatureRejected)
        {
            return new ReleaseCatalogFetchResult.Rejected(signatureRejected.Issue,
                CatalogIssue: null);
        }

        ReleaseCatalogResult result = _catalog.Read(
            ((ReleaseTransferResult<byte[]>.Downloaded)catalogTransfer).Value,
            ((ReleaseTransferResult<byte[]>.Downloaded)signatureTransfer).Value);
        return result switch
        {
            ReleaseCatalogResult.Selected selected =>
                new ReleaseCatalogFetchResult.Selected(selected.Release),
            ReleaseCatalogResult.Rejected rejected =>
                new ReleaseCatalogFetchResult.Rejected(TransferIssue: null, rejected.Issue),
            _ => throw new InvalidOperationException("Unknown release catalog result."),
        };
    }
}
