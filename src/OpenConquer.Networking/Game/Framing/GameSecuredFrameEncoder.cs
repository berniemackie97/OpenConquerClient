using OpenConquer.Networking.Game.Cryptography;

namespace OpenConquer.Networking.Game.Framing;

/// <summary>
/// Encrypts client-to-GameServer packet frames under an already negotiated
/// session cipher. The owner is responsible for transport ordering and lifetime.
/// </summary>
internal sealed class GameSecuredFrameEncoder : IDisposable
{
    private readonly Lock _gate = new();
    private readonly GameSessionCipher _sessionCipher;
    private bool _faulted;
    private bool _disposed;

    internal GameSecuredFrameEncoder(GameSessionCipher sessionCipher)
    {
        _sessionCipher = sessionCipher ?? throw new ArgumentNullException(nameof(sessionCipher));
    }

    /// <summary>
    /// Validates a complete packet before mutating the destination or advancing
    /// outbound feedback. Returns exactly the number of encrypted wire bytes.
    /// </summary>
    internal int Write(ReadOnlySpan<byte> packet, Span<byte> destination)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_faulted)
            {
                throw new InvalidOperationException("The secured frame encoder is terminal after a cipher failure.");
            }

            int wireLength = GameSecuredFrameProtocol.GetWireLength(packet);
            if (destination.Length < wireLength)
            {
                throw new ArgumentException("The destination is smaller than the encrypted wire frame.", nameof(destination));
            }

            Span<byte> wire = destination[..wireLength];
            packet.CopyTo(wire);
            GameSecuredFrameProtocol.ClientSignature.CopyTo(wire[packet.Length..]);
            try
            {
                _sessionCipher.EncryptOutbound(wire, wire);
                return wireLength;
            }
            catch
            {
                _faulted = true;
                wire.Clear();
                throw;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }
    }
}
