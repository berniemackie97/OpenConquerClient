namespace OpenConquer.Content.Tool.Legacy.ServerDat;

/// <summary>
/// Immutable representation of the server groups decoded from a retail 5517
/// <c>Server.dat</c> file.
/// </summary>
/// <remarks>
/// This model preserves the historical file's structure for inspection, compatibility research,
/// and parity testing. It is tooling data and is not the modern client's runtime realm model.
/// </remarks>
internal sealed class ServerDatCatalog
{
    public ServerDatCatalog(IEnumerable<ServerDatGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        Groups = Array.AsReadOnly(groups.ToArray());
    }

    /// <summary>
    /// Groups in the order declared by the retail <c>outenserver</c> table.
    /// </summary>
    public IReadOnlyList<ServerDatGroup> Groups
    {
        get;
    }
}
