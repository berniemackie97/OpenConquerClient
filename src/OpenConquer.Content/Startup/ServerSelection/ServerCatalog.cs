namespace OpenConquer.Content.Startup.ServerSelection;

/// <summary>
/// Immutable startup server-selection catalog decoded from retail client data.
/// </summary>
public sealed class ServerCatalog
{
    internal ServerCatalog(IEnumerable<ServerGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        Groups = Array.AsReadOnly(groups.ToArray());
    }

    /// <summary>
    /// Server groups in the order declared by the retail startup table.
    /// </summary>
    public IReadOnlyList<ServerGroup> Groups
    {
        get;
    }
}
