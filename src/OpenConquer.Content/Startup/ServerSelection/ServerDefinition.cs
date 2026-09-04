namespace OpenConquer.Content.Startup.ServerSelection;

/// <summary>
/// One server definition from the retail startup catalog.
/// </summary>
public sealed class ServerDefinition
{
    internal ServerDefinition(int id, string displayName, string? iconToken, string hint, string serverName, string host, string port)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        ArgumentException.ThrowIfNullOrEmpty(displayName);
        ArgumentNullException.ThrowIfNull(hint);
        ArgumentException.ThrowIfNullOrEmpty(serverName);
        ArgumentException.ThrowIfNullOrEmpty(host);
        ArgumentException.ThrowIfNullOrEmpty(port);

        Id = id;
        DisplayName = displayName;
        IconToken = iconToken;
        Hint = hint;
        ServerName = serverName;
        Host = host;
        Port = port;
    }

    /// <summary>
    /// Retail server row identifier.
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
    public string? IconToken
    {
        get;
    }

    /// <summary>
    /// Optional descriptive text sourced from the retail <c>FlashHint</c> field.
    /// </summary>
    public string Hint
    {
        get;
    }

    /// <summary>
    /// Native server-name identifier sourced from the retail <c>ServerName</c> field.
    /// </summary>
    public string ServerName
    {
        get;
    }

    /// <summary>
    /// Host value sourced from the retail <c>ServerIP</c> field.
    /// </summary>
    /// <remarks>
    /// This layer preserves the source value as text. Endpoint parsing and connection policy belong
    /// to the networking/application boundary rather than the content decoder.
    /// </remarks>
    public string Host
    {
        get;
    }

    /// <summary>
    /// Port value sourced from the retail <c>ServerPort</c> field.
    /// </summary>
    /// <remarks>
    /// The value remains textual at this boundary. Networking performs endpoint validation when a
    /// selected server is actually used.
    /// </remarks>
    public string Port
    {
        get;
    }
}
