using System.Buffers;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using OpenConquer.Content.Login;

namespace OpenConquer.Client.UI.Login.Selection;

/// <summary>Display identity only. AccountServer routing and catalog authority are separate concerns.</summary>
internal sealed class LoginServer
{
    public string Id
    {
        get;
    }
    public string DisplayName
    {
        get;
    }
    public string ProtocolServerName
    {
        get;
    }
    public string? IconId
    {
        get;
    }

    public LoginServer(string id, string displayName, string protocolServerName, string? iconId = null)
    {
        Id = LoginCatalogText.ValidateId(id, nameof(id));
        DisplayName = LoginCatalogText.ValidateDisplayName(displayName, nameof(displayName));
        ProtocolServerName = LoginCatalogText.ValidateProtocolServerName(protocolServerName, nameof(protocolServerName));
        IconId = iconId is null or "none" ? null : LoginServerIconArtwork.ValidateId(iconId);
    }
}

internal sealed class LoginRegion
{
    private readonly ReadOnlyCollection<LoginServer> _servers;

    public string Id
    {
        get;
    }
    public string DisplayName
    {
        get;
    }
    public IReadOnlyList<LoginServer> Servers => _servers;

    public LoginRegion(string id, string displayName, IEnumerable<LoginServer> servers)
    {
        Id = LoginCatalogText.ValidateId(id, nameof(id));
        DisplayName = LoginCatalogText.ValidateDisplayName(displayName, nameof(displayName));
        ArgumentNullException.ThrowIfNull(servers);

        List<LoginServer> copy = new();
        HashSet<string> serverIds = new(StringComparer.Ordinal);
        HashSet<string> protocolNames = new(StringComparer.Ordinal);

        foreach (LoginServer server in servers)
        {
            ArgumentNullException.ThrowIfNull(server);

            if (copy.Count >= LoginServerCatalog.MaximumServersPerRegion)
            {
                throw new ArgumentException("Region exceeds the supported server count.", nameof(servers));
            }

            if (!serverIds.Add(server.Id) || !protocolNames.Add(server.ProtocolServerName))
            {
                throw new ArgumentException("Region contains a duplicate server identity or protocol server name.", nameof(servers));
            }

            copy.Add(server);
        }

        _servers = copy.AsReadOnly();
    }
}

/// <summary>
/// Immutable, bounded presentation snapshot accepted only from an explicitly trusted catalog producer.
/// An empty catalog does not authorize selection, networking, or account login.
/// </summary>
internal sealed class LoginServerCatalog
{
    internal const int MaximumRegions = 64;
    internal const int MaximumServersPerRegion = 128;
    internal const int MaximumTotalServers = 1024;

    private readonly ReadOnlyCollection<LoginRegion> _regions;

    public static LoginServerCatalog Empty { get; } = new(Array.Empty<LoginRegion>());
    public IReadOnlyList<LoginRegion> Regions => _regions;

    public LoginServerCatalog(IEnumerable<LoginRegion> regions)
    {
        ArgumentNullException.ThrowIfNull(regions);

        List<LoginRegion> copy = new();
        HashSet<string> ids = new(StringComparer.Ordinal);
        int totalServers = 0;

        foreach (LoginRegion region in regions)
        {
            ArgumentNullException.ThrowIfNull(region);

            if (copy.Count >= MaximumRegions)
            {
                throw new ArgumentException("Catalog exceeds the supported region count.", nameof(regions));
            }

            if (!ids.Add(region.Id))
            {
                throw new ArgumentException("Catalog contains duplicate region identities.", nameof(regions));
            }

            totalServers = checked(totalServers + region.Servers.Count);

            if (totalServers > MaximumTotalServers)
            {
                throw new ArgumentException("Catalog exceeds the supported total server count.", nameof(regions));
            }

            copy.Add(region);
        }

        _regions = copy.AsReadOnly();
    }
}

internal static class LoginCatalogText
{
    public static string ValidateId(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        if (value.Length > 64)
        {
            throw new ArgumentException("Catalog identities must contain no more than 64 ASCII characters.", parameterName);
        }

        foreach (char character in value)
        {
            bool isLetter = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
            bool isDigit = character is >= '0' and <= '9';

            if (!isLetter && !isDigit && character is not ('_' or '-' or '.'))
            {
                throw new ArgumentException("Catalog identity contains a prohibited character.", parameterName);
            }
        }

        return value;
    }

    public static string ValidateDisplayName(string value, string parameterName) => ValidateName(value, parameterName, maxLength: 48);

    public static string ValidateProtocolServerName(string value, string parameterName)
    {
        ValidateName(value, parameterName, maxLength: 64);

        foreach (char character in value)
        {
            if (char.IsSurrogate(character))
            {
                throw new ArgumentException("The native protocol server name does not support supplementary characters.", parameterName);
            }
        }

        return value;
    }

    public static string ValidateName(string value, string parameterName, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        if (value.Length > maxLength || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Catalog name has an invalid length or surrounding whitespace.", parameterName);
        }

        ReadOnlySpan<char> text = value.AsSpan();

        for (int index = 0; index < text.Length;)
        {
            if (Rune.DecodeFromUtf16(text[index..], out Rune rune, out int consumed) != OperationStatus.Done)
            {
                throw new ArgumentException("Catalog name contains malformed UTF-16.", parameterName);
            }

            UnicodeCategory category = Rune.GetUnicodeCategory(rune);

            if (category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
            {
                throw new ArgumentException("Catalog name contains a prohibited control or directional character.", parameterName);
            }

            index += consumed;
        }

        return value;
    }
}
