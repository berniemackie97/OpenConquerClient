namespace OpenConquer.Networking.Game.Login;

/// <summary>
/// Typed observation of the companion server's current 2078/2079 prelude.
/// Does not assert world-entry readiness or completion of character bootstrap.
/// </summary>
internal sealed record GameExistingCharacterPrelude(
    GameLoginHistory2078 LoginHistory,
    GameServerState2079 ServerState);
