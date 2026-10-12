using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using OpenConquer.Client.UI.Login;

namespace OpenConquer.Client.Startup.Catalog;

/// <summary>A separately authenticated endpoint directory and its read-only login view.</summary>
internal sealed class SignedLoginCatalog
{
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    public const int MaximumCatalogBytes = 128 * 1024;
    public const int SignatureBytes = 64;
    public static readonly TimeSpan MaximumLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan s_clockSkew = TimeSpan.FromMinutes(5);
    private readonly ReadOnlyDictionary<(string RegionId, string ServerId), LoginAccountEndpoint> _endpoints;

    public LoginServerCatalog View
    {
        get;
    }
    public long Sequence
    {
        get;
    }
    public DateTimeOffset ExpiresAt
    {
        get;
    }

    private SignedLoginCatalog(LoginServerCatalog view, Dictionary<(string, string), LoginAccountEndpoint> endpoints, long sequence, DateTimeOffset expiresAt)
    {
        View = view;
        _endpoints = new ReadOnlyDictionary<(string, string), LoginAccountEndpoint>(endpoints);
        Sequence = sequence;
        ExpiresAt = expiresAt;
    }

    public bool IsCurrent(DateTimeOffset now) => now < ExpiresAt;

    public bool TryGetAccountEndpoint(LoginRegion region, LoginServer server, out LoginAccountEndpoint? endpoint)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(server);
        endpoint = null;

        if (!View.Regions.Any(candidate => ReferenceEquals(candidate, region)) ||
            !region.Servers.Any(candidate => ReferenceEquals(candidate, server)))
        {
            return false;
        }

        return _endpoints.TryGetValue((region.Id, server.Id), out endpoint);
    }

    public static SignedLoginCatalog Read(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> detachedSignature, LoginCatalogAuthority authority, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(authority);

        if (payload.Length is 0 or > MaximumCatalogBytes || detachedSignature.Length != SignatureBytes ||
            !authority.Verify(payload, detachedSignature))
        {
            throw new InvalidDataException("The realm catalog signature is invalid.");
        }

        try
        {
            using JsonDocument document = LoginCatalogJson.Parse(payload);
            JsonElement root = document.RootElement;
            LoginCatalogJson.RequireObject(root, "schemaVersion", "productId", "sequence", "issuedAt", "expiresAt", "regions");

            int schemaVersion = LoginCatalogJson.RequiredInteger(root, "schemaVersion");

            if (schemaVersion is not (1 or 2) ||
                LoginCatalogJson.RequiredString(root, "productId", 32) != "OpenConquer")
            {
                throw new InvalidDataException("The realm catalog schema or product is unsupported.");
            }

            long sequence = LoginCatalogJson.RequiredPositiveLong(root, "sequence");
            DateTimeOffset issuedAt = ReadTimestamp(root, "issuedAt");
            DateTimeOffset expiresAt = ReadTimestamp(root, "expiresAt");

            if (issuedAt > now + s_clockSkew || expiresAt <= now || expiresAt <= issuedAt ||
                expiresAt - issuedAt > MaximumLifetime)
            {
                throw new InvalidDataException("The realm catalog is expired or outside its permitted validity interval.");
            }

            List<LoginRegion> regions = new();
            Dictionary<(string, string), LoginAccountEndpoint> endpoints = new();
            HashSet<string> protocolNames = new(StringComparer.Ordinal);
            int totalServers = 0;

            foreach (JsonElement regionElement in LoginCatalogJson.RequiredArray(root, "regions").EnumerateArray())
            {
                if (regions.Count >= LoginServerCatalog.MaximumRegions)
                {
                    throw new InvalidDataException("The realm catalog exceeds the region limit.");
                }

                LoginCatalogJson.RequireObject(regionElement, "id", "name", "servers");
                string regionId = LoginCatalogJson.RequiredString(regionElement, "id", 64);
                string regionName = LoginCatalogJson.RequiredString(regionElement, "name", 48);
                List<LoginServer> servers = new();

                foreach (JsonElement serverElement in LoginCatalogJson.RequiredArray(regionElement, "servers").EnumerateArray())
                {
                    if (servers.Count >= LoginServerCatalog.MaximumServersPerRegion ||
                        totalServers >= LoginServerCatalog.MaximumTotalServers)
                    {
                        throw new InvalidDataException("The realm catalog exceeds the server limit.");
                    }

                    if (schemaVersion == 2)
                    {
                        LoginCatalogJson.RequireObject(serverElement, "id", "name", "protocolServerName", "accountHost", "accountPort", "iconId");
                    }
                    else
                    {
                        LoginCatalogJson.RequireObject(serverElement, "id", "name", "protocolServerName", "accountHost", "accountPort");
                    }
                    string serverId = LoginCatalogJson.RequiredString(serverElement, "id", 64);
                    string displayName = LoginCatalogJson.RequiredString(serverElement, "name", 48);
                    string protocolName = LoginCatalogJson.RequiredString(serverElement, "protocolServerName", 64);
                    string accountHost = LoginCatalogJson.RequiredString(serverElement, "accountHost", 253);
                    int accountPort = LoginCatalogJson.RequiredInteger(serverElement, "accountPort");
                    string? iconId = schemaVersion == 2 ? LoginCatalogJson.RequiredString(serverElement, "iconId", 8) : null;
                    LoginServer server = new(serverId, displayName, protocolName, iconId);

                    if (!protocolNames.Add(server.ProtocolServerName) ||
                        !endpoints.TryAdd((regionId, server.Id), new LoginAccountEndpoint(accountHost, accountPort)))
                    {
                        throw new InvalidDataException("The realm catalog contains ambiguous server routing identities.");
                    }

                    servers.Add(server);
                    totalServers++;
                }

                regions.Add(new LoginRegion(regionId, regionName, servers));
            }

            return new SignedLoginCatalog(new LoginServerCatalog(regions), endpoints, sequence, expiresAt);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or OverflowException)
        {
            throw new InvalidDataException("The realm catalog is invalid.", exception);
        }
    }

    private static DateTimeOffset ReadTimestamp(JsonElement root, string property)
    {
        string value = LoginCatalogJson.RequiredString(root, property, TimestampFormat.Length);

        if (!DateTimeOffset.TryParseExact(value, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset result))
        {
            throw new InvalidDataException($"The realm catalog '{property}' timestamp is invalid.");
        }

        return result;
    }
}

/// <summary>Trusted account-ingress routing metadata, never derived from UI labels or legacy Server.dat.</summary>
internal sealed class LoginAccountEndpoint
{
    public string Host
    {
        get;
    }
    public int Port
    {
        get;
    }

    public LoginAccountEndpoint(string host, int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        if (host.Length > 253 || host != host.Trim() || host.Any(static character => character > 127 || char.IsWhiteSpace(character)) ||
            Uri.CheckHostName(host) is not (UriHostNameType.Dns or UriHostNameType.IPv4) ||
            port is < 1 or > 65535)
        {
            throw new ArgumentException("The account ingress host or port is invalid.", nameof(host));
        }

        Host = host;
        Port = port;
    }
}
