namespace OpenConquer.Content.Tool.Legacy.ServerDat;

/// <summary>
/// One server group row decoded from the retail 5517 <c>outenserver</c> table.
/// </summary>
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

    public int Id
    {
        get;
    }

    public string FlashName
    {
        get;
    }

    public string? FlashIcon
    {
        get;
    }

    public IReadOnlyList<ServerDatServer> Servers
    {
        get;
    }
}
