namespace OpenConquer.Content.Tool.Legacy.ServerDat;

/// <summary>
/// One server-group row decoded from the retail 5517 <c>outenserver</c> table.
/// </summary>
/// <remarks>
/// Property names intentionally preserve the corresponding retail field names. This type describes
/// historical <c>Server.dat</c> data and is not a modern realm-group model.
/// </remarks>
internal sealed class ServerDatGroup
{
    public ServerDatGroup(int id, string flashName, string? flashIcon, IEnumerable<ServerDatServer> servers)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        ArgumentException.ThrowIfNullOrEmpty(flashName);
        ArgumentNullException.ThrowIfNull(servers);

        Id = id;
        FlashName = flashName;
        FlashIcon = flashIcon;
        Servers = Array.AsReadOnly(servers.ToArray());
    }

    /// <summary>
    /// Value of the retail <c>id</c> field for this group row.
    /// </summary>
    public int Id
    {
        get;
    }

    /// <summary>
    /// Value of the retail <c>FlashName</c> field.
    /// </summary>
    public string FlashName
    {
        get;
    }

    /// <summary>
    /// Value of the retail <c>FlashIcon</c> field after normalization of the retail
    /// <c>NULL</c> sentinel to <see langword="null"/>.
    /// </summary>
    public string? FlashIcon
    {
        get;
    }

    /// <summary>
    /// Server rows assigned to this group in native row-index order.
    /// </summary>
    public IReadOnlyList<ServerDatServer> Servers
    {
        get;
    }
}
