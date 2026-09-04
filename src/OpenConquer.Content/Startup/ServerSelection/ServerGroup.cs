namespace OpenConquer.Content.Startup.ServerSelection;

/// <summary>
/// One startup server group and the servers assigned to it.
/// </summary>
public sealed class ServerGroup
{
    internal ServerGroup(int id, string displayName, string? iconToken, IEnumerable<ServerDefinition> servers)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        ArgumentException.ThrowIfNullOrEmpty(displayName);
        ArgumentNullException.ThrowIfNull(servers);

        Id = id;
        DisplayName = displayName;
        IconToken = iconToken;
        Servers = Array.AsReadOnly(servers.ToArray());
    }

    /// <summary>
    /// Retail group row identifier.
    /// </summary>
    public int Id
    {
        get;
    }

    /// <summary>
    /// Display name sourced from the retail <c>FlashName</c> field.
    /// </summary>
    public string DisplayName
    {
        get;
    }

    /// <summary>
    /// Optional opaque presentation token sourced from the retail <c>FlashIcon</c> field.
    /// </summary>
    /// <remarks>
    /// The literal retail sentinel <c>NULL</c> is represented as <see langword="null"/>.
    /// No asset-path interpretation is performed by the content layer.
    /// </remarks>
    public string? IconToken
    {
        get;
    }

    /// <summary>
    /// Servers assigned to this group in native row-index order.
    /// </summary>
    public IReadOnlyList<ServerDefinition> Servers
    {
        get;
    }
}
