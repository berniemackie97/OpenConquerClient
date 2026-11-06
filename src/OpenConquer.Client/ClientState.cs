namespace OpenConquer.Client;

/// <summary>The active client phase; returning retains old resources until their close/load is reaped.</summary>
internal enum ClientState
{
    Login,
    RoleCreation,
    WorldLoading,
    World,
    ReturningToLogin,
    Closing,
    Closed,
}
