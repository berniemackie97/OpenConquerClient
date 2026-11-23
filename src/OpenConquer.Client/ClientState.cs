namespace OpenConquer.Client;

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
