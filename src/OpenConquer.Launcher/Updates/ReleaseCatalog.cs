using System.Text.Json;
using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher.Updates;

internal sealed record ReleaseCatalogEntry(
    ulong ReleaseSequence,
    string ReleaseVersion,
    int MinimumLauncherVersion,
    string TargetRuntime,
    Uri PackageUri,
    long PackageLength,
    byte[] PackageSha256);

internal enum ReleaseCatalogIssue
{
    AuthorityUnavailable,
    SignatureInvalid,
    Invalid,
    Unsupported,
    Expired,
    NoCompatibleRelease,
    LauncherUpdateRequired,
}

internal abstract record ReleaseCatalogResult
{
    private ReleaseCatalogResult()
    {
    }

    internal sealed record Selected(ReleaseCatalogEntry Release) : ReleaseCatalogResult;

    internal sealed record Rejected(ReleaseCatalogIssue Issue) : ReleaseCatalogResult;
}

/// <summary>Authenticates a bounded publisher catalog and selects the newest compatible release.</summary>
internal sealed class ReleaseCatalog
{
    public const int CurrentSchemaVersion = 1;
    public const string ExpectedProductId = "OpenConquer";
    public const string FileName = "openconquer.catalog.json";
    public const string SignatureFileName = "openconquer.catalog.sig";
    public const int MaximumLength = 256 * 1024;
    public const int MaximumReleaseCount = 128;
    public const long MaximumPackageLength = 16L * 1024 * 1024 * 1024;
    public static readonly TimeSpan MaximumRemainingLifetime = TimeSpan.FromDays(31);

    private const int MaximumVersionLength = 64;
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    private readonly TrustedReleaseKeys _trustedKeys;
    private readonly string? _currentRuntime;
    private readonly TimeProvider _timeProvider;

    public ReleaseCatalog(
        TrustedReleaseKeys trustedKeys,
        string? currentRuntime = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(trustedKeys);
        if (currentRuntime is not null && !ReleaseTargetRuntime.IsSupported(currentRuntime))
        {
            throw new ArgumentException("The release target runtime is unsupported.",
                nameof(currentRuntime));
        }

        _trustedKeys = trustedKeys;
        _currentRuntime = currentRuntime ?? ReleaseTargetRuntime.Current;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsAuthorityConfigured => _trustedKeys.IsConfigured;

    public ReleaseCatalogResult Read(
        ReadOnlyMemory<byte> catalogBytes,
        ReadOnlyMemory<byte> signatureBytes)
    {
        if (!_trustedKeys.IsConfigured)
        {
            return new ReleaseCatalogResult.Rejected(
                ReleaseCatalogIssue.AuthorityUnavailable);
        }

        if (catalogBytes.IsEmpty || catalogBytes.Length > MaximumLength ||
            !TryReadSignature(signatureBytes, out ManagedReleaseSignature? signature) ||
            signature is null || !_trustedKeys.Verify(signature, catalogBytes.Span))
        {
            return new ReleaseCatalogResult.Rejected(ReleaseCatalogIssue.SignatureInvalid);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(catalogBytes,
                new JsonDocumentOptions { MaxDepth = 8 });
            JsonElement root = document.RootElement;
            if (HasUnsupportedSchema(root))
            {
                return new ReleaseCatalogResult.Rejected(ReleaseCatalogIssue.Unsupported);
            }

            if (!TryRead(root, out DateTimeOffset expiresAt,
                    out IReadOnlyList<ReleaseCatalogEntry>? releases) ||
                releases is null)
            {
                return new ReleaseCatalogResult.Rejected(ReleaseCatalogIssue.Invalid);
            }

            DateTimeOffset now = _timeProvider.GetUtcNow();
            if (expiresAt <= now)
            {
                return new ReleaseCatalogResult.Rejected(ReleaseCatalogIssue.Expired);
            }

            if (expiresAt - now > MaximumRemainingLifetime)
            {
                return new ReleaseCatalogResult.Rejected(ReleaseCatalogIssue.Invalid);
            }

            ReleaseCatalogEntry? selected = releases
                .Where(release => string.Equals(release.TargetRuntime, _currentRuntime,
                    StringComparison.Ordinal))
                .MaxBy(release => release.ReleaseSequence);
            if (selected is null)
            {
                return new ReleaseCatalogResult.Rejected(
                    ReleaseCatalogIssue.NoCompatibleRelease);
            }

            return selected.MinimumLauncherVersion >
                ManagedReleaseManifest.CurrentLauncherVersion
                ? new ReleaseCatalogResult.Rejected(
                    ReleaseCatalogIssue.LauncherUpdateRequired)
                : new ReleaseCatalogResult.Selected(selected);
        }
        catch (JsonException)
        {
            return new ReleaseCatalogResult.Rejected(ReleaseCatalogIssue.Invalid);
        }
    }

    private static bool TryRead(
        JsonElement root,
        out DateTimeOffset expiresAt,
        out IReadOnlyList<ReleaseCatalogEntry>? releases)
    {
        expiresAt = default;
        releases = null;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 4 ||
            !root.TryGetProperty("schemaVersion", out JsonElement schema) ||
            !schema.TryGetInt32(out int schemaVersion) || schemaVersion != CurrentSchemaVersion ||
            !root.TryGetProperty("productId", out JsonElement productId) ||
            productId.ValueKind != JsonValueKind.String ||
            !string.Equals(productId.GetString(), ExpectedProductId, StringComparison.Ordinal) ||
            !root.TryGetProperty("expiresUtc", out JsonElement expiresElement) ||
            expiresElement.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParseExact(expiresElement.GetString(), TimestampFormat,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal |
                System.Globalization.DateTimeStyles.AdjustToUniversal, out expiresAt) ||
            !root.TryGetProperty("releases", out JsonElement entries) ||
            entries.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        HashSet<string> rootProperties = root.EnumerateObject()
            .Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        if (!rootProperties.SetEquals(
                ["schemaVersion", "productId", "expiresUtc", "releases"]))
        {
            return false;
        }

        List<ReleaseCatalogEntry> parsed = [];
        HashSet<(string Runtime, ulong Sequence)> identities = [];
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            if (parsed.Count == MaximumReleaseCount ||
                !TryReadEntry(entry, out ReleaseCatalogEntry? release) || release is null ||
                !identities.Add((release.TargetRuntime, release.ReleaseSequence)))
            {
                return false;
            }

            parsed.Add(release);
        }

        if (parsed.Count == 0)
        {
            return false;
        }

        releases = parsed;
        return true;
    }

    private static bool TryReadEntry(
        JsonElement entry,
        out ReleaseCatalogEntry? release)
    {
        release = null;
        if (entry.ValueKind != JsonValueKind.Object || entry.EnumerateObject().Count() != 7)
        {
            return false;
        }

        HashSet<string> properties = entry.EnumerateObject()
            .Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        if (!properties.SetEquals([
                "releaseSequence", "releaseVersion", "minimumLauncherVersion",
                "targetRuntime", "packageUri", "packageLength", "packageSha256",
            ]) ||
            !entry.TryGetProperty("releaseSequence", out JsonElement sequenceElement) ||
            !sequenceElement.TryGetUInt64(out ulong sequence) || sequence == 0 ||
            !entry.TryGetProperty("releaseVersion", out JsonElement versionElement) ||
            versionElement.ValueKind != JsonValueKind.String ||
            !IsValidVersion(versionElement.GetString()) ||
            !entry.TryGetProperty("minimumLauncherVersion", out JsonElement launcherElement) ||
            !launcherElement.TryGetInt32(out int minimumLauncherVersion) ||
            minimumLauncherVersion <= 0 ||
            !entry.TryGetProperty("targetRuntime", out JsonElement runtimeElement) ||
            runtimeElement.ValueKind != JsonValueKind.String ||
            !ReleaseTargetRuntime.IsSupported(runtimeElement.GetString() ?? string.Empty) ||
            !entry.TryGetProperty("packageUri", out JsonElement uriElement) ||
            uriElement.ValueKind != JsonValueKind.String ||
            !ReleaseUriPolicy.TryParseHttps(uriElement.GetString(), out Uri? packageUri) ||
            !entry.TryGetProperty("packageLength", out JsonElement lengthElement) ||
            !lengthElement.TryGetInt64(out long packageLength) ||
            packageLength is <= 0 or > MaximumPackageLength ||
            !entry.TryGetProperty("packageSha256", out JsonElement digestElement) ||
            digestElement.ValueKind != JsonValueKind.String ||
            !TryReadDigest(digestElement.GetString(), out byte[]? digest))
        {
            return false;
        }

        release = new ReleaseCatalogEntry(sequence, versionElement.GetString()!,
            minimumLauncherVersion, runtimeElement.GetString()!, packageUri, packageLength,
            digest!);
        return true;
    }

    private static bool TryReadSignature(
        ReadOnlyMemory<byte> bytes,
        out ManagedReleaseSignature? signature)
    {
        signature = null;
        if (bytes.IsEmpty || bytes.Length > 4 * 1024)
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes,
                new JsonDocumentOptions { MaxDepth = 4 });
            return ManagedReleaseSignature.TryRead(document.RootElement, out signature);
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            return false;
        }
    }

    private static bool HasUnsupportedSchema(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        HashSet<string> properties = new(StringComparer.Ordinal);
        if (root.EnumerateObject().Any(property => !properties.Add(property.Name)))
        {
            return false;
        }

        return
            root.TryGetProperty("schemaVersion", out JsonElement schema) &&
            schema.TryGetInt32(out int schemaVersion) && schemaVersion > CurrentSchemaVersion &&
            root.TryGetProperty("productId", out JsonElement productId) &&
            productId.ValueKind == JsonValueKind.String &&
            string.Equals(productId.GetString(), ExpectedProductId, StringComparison.Ordinal);
    }

    private static bool TryReadDigest(string? value, out byte[]? digest)
    {
        digest = null;
        if (value is not { Length: 64 } || value.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            return false;
        }

        digest = Convert.FromHexString(value);
        return true;
    }

    private static bool IsValidVersion(string? version)
    {
        return version is { Length: > 0 and <= MaximumVersionLength } &&
            char.IsAsciiLetterOrDigit(version[0]) && char.IsAsciiLetterOrDigit(version[^1]) &&
            version.All(character => char.IsAsciiLetterOrDigit(character) ||
                character is '.' or '-');
    }
}
