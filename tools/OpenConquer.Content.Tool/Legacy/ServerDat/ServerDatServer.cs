namespace OpenConquer.Content.Tool.Legacy.ServerDat;

/// <summary>
/// One server row decoded from the retail 5517 <c>outenserver</c> table.
/// </summary>
/// <remarks>
/// Property names preserve the semantics of the corresponding retail fields rather than projecting
/// them into modern realm, presentation, or networking concepts.
/// </remarks>
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

    /// <summary>
    /// Value of the retail <c>id</c> field for this server row.
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
    /// Value of the retail <c>FlashHint</c> field, or an empty string when the field is absent.
    /// </summary>
    public string FlashHint
    {
        get;
    }

    /// <summary>
    /// Value of the retail <c>ServerName</c> field.
    /// </summary>
    /// <remarks>
    /// Native 5517 treats this as a protocol-significant server identifier. It is distinct from
    /// <see cref="FlashName"/>.
    /// </remarks>
    public string ServerName
    {
        get;
    }

    /// <summary>
    /// Value of the retail <c>ServerIP</c> field.
    /// </summary>
    /// <remarks>
    /// The value is preserved as source text. Tooling does not reinterpret it as a modern runtime
    /// endpoint.
    /// </remarks>
    public string ServerIp
    {
        get;
    }

    /// <summary>
    /// Value of the retail <c>ServerPort</c> field.
    /// </summary>
    /// <remarks>
    /// The value is preserved as source text rather than parsed into a modern networking type.
    /// </remarks>
    public string ServerPort
    {
        get;
    }
}
