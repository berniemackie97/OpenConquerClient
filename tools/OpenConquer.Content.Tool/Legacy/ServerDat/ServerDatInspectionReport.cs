using System.Text.Json;

namespace OpenConquer.Content.Tool.Legacy.ServerDat;

/// <summary>
/// Human-readable inspection report for one decoded retail 5517 <c>Server.dat</c> file.
/// </summary>
/// <remarks>
/// Source strings are JSON-escaped before rendering so malformed or adversarial legacy values cannot
/// inject additional terminal report lines.
/// </remarks>
internal sealed class ServerDatInspectionReport
{
    private ServerDatInspectionReport(string filePath, ServerDatCatalog catalog)
    {
        FilePath = filePath;
        Catalog = catalog;
    }

    public string FilePath
    {
        get;
    }

    public ServerDatCatalog Catalog
    {
        get;
    }

    /// <summary>
    /// Reads <paramref name="filePath"/> with the verified retail key and constructs its inspection
    /// report.
    /// </summary>
    public static ServerDatInspectionReport Create(string filePath)
    {
        return Create(filePath, ServerDatNativePublicKey.Modulus);
    }

    /// <summary>
    /// Reads <paramref name="filePath"/> with an explicitly supplied public modulus and constructs
    /// its inspection report.
    /// </summary>
    /// <remarks>
    /// The modulus override exists for tests that exercise the complete inspection pipeline with
    /// independently generated RSA fixtures.
    /// </remarks>
    internal static ServerDatInspectionReport Create(
        string filePath,
        ReadOnlySpan<byte> publicModulus
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string absoluteFilePath = Path.GetFullPath(filePath);

        ServerDatCatalog catalog = ServerDatFileReader.Read(absoluteFilePath, publicModulus);

        return new ServerDatInspectionReport(absoluteFilePath, catalog);
    }

    /// <summary>
    /// Renders the decoded catalog as deterministic console lines.
    /// </summary>
    public IEnumerable<string> ToReportLines()
    {
        int serverCount = Catalog.Groups.Sum(static group => group.Servers.Count);

        yield return $"File: {FormatValue(FilePath)}";
        yield return $"Groups: {Catalog.Groups.Count}";
        yield return $"Servers: {serverCount}";

        foreach (ServerDatGroup group in Catalog.Groups)
        {
            yield return $"Group {group.Id}: "
                + $"FlashName={FormatValue(group.FlashName)}, "
                + $"FlashIcon={FormatValue(group.FlashIcon)}, "
                + $"Servers={group.Servers.Count}";

            foreach (ServerDatServer server in group.Servers)
            {
                yield return $"  Server {server.Id}: "
                    + $"FlashName={FormatValue(server.FlashName)}, "
                    + $"FlashIcon={FormatValue(server.FlashIcon)}, "
                    + $"FlashHint={FormatValue(server.FlashHint)}, "
                    + $"ServerName={FormatValue(server.ServerName)}, "
                    + $"ServerIP={FormatValue(server.ServerIp)}, "
                    + $"ServerPort={FormatValue(server.ServerPort)}";
            }
        }
    }

    private static string FormatValue(string? value)
    {
        return value is null ? "null" : JsonSerializer.Serialize(value);
    }
}
