namespace OpenConquer.Content.Tool.Legacy.ServerDat;

/// <summary>
/// The server groups decoded from a retail 5517 <c>Server.dat</c> file.
/// </summary>
internal sealed class ServerDatCatalog
{
    public ServerDatCatalog(IEnumerable<ServerDatGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        Groups = Array.AsReadOnly(groups.ToArray());
    }

    public IReadOnlyList<ServerDatGroup> Groups
    {
        get;
    }
}
