namespace OpenConquer.Content.Tool.Legacy.ServerDat;

/// <summary>
/// One server row decoded from the retail 5517 <c>outenserver</c> table.
/// </summary>
internal sealed class ServerDatServer
{
    public ServerDatServer(int id, string flashName, string? flashIcon, string flashHint, string serverName, string serverIp, string serverPort)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        ArgumentException.ThrowIfNullOrEmpty(flashName);
        ArgumentNullException.ThrowIfNull(flashHint);
        ArgumentException.ThrowIfNullOrEmpty(serverName);
        ArgumentException.ThrowIfNullOrEmpty(serverIp);
        ArgumentException.ThrowIfNullOrEmpty(serverPort);

        Id = id;
        FlashName = flashName;
        FlashIcon = flashIcon;
        FlashHint = flashHint;
        ServerName = serverName;
        ServerIp = serverIp;
        ServerPort = serverPort;
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

    public string FlashHint
    {
        get;
    }

    public string ServerName
    {
        get;
    }

    public string ServerIp
    {
        get;
    }

    public string ServerPort
    {
        get;
    }
}
