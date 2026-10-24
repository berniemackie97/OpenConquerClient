namespace OpenConquer.Networking.Game.Handshake;

/// <summary>Independent ceilings for connecting and completing one native DH exchange.</summary>
internal readonly record struct GameServerHandshakeTimeouts(TimeSpan Connect, TimeSpan Exchange)
{
    // Native redirect continuation 0x754DFB passes 30 seconds to the connect call.
    internal static GameServerHandshakeTimeouts Default
    {
        get;
    } = new(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5));

    internal void Validate()
    {
        if (Connect <= TimeSpan.Zero || Connect > TimeSpan.FromSeconds(30) ||
            Exchange <= TimeSpan.Zero || Exchange > TimeSpan.FromSeconds(30))
        {
            throw new ArgumentOutOfRangeException(nameof(GameServerHandshakeTimeouts),
                "GameServer handshake timeouts must be positive and at most 30 seconds.");
        }
    }
}
