using System.Security.Cryptography;
using OpenConquer.Networking.Game.Handshake;

namespace OpenConquer.Networking.Game.Cryptography;

/// <summary>
/// Two independent directional CAST5 CFB-64 streams for one completed native
/// GameServer DH exchange. Offline only; this type does not own a socket or
/// authenticate the remote endpoint.
/// </summary>
internal sealed class GameSessionCipher : IDisposable
{
    internal const int SessionKeyMaterialLength = GameClientDhExchange.SessionKeyMaterialLength;

    private readonly Lock _inboundGate = new();
    private readonly Lock _outboundGate = new();
    private readonly GameCast5Cfb64Cipher _inbound;
    private readonly GameCast5Cfb64Cipher _outbound;

    private bool _disposed;

    internal GameSessionCipher(ReadOnlySpan<byte> sessionKeyMaterial,
        ReadOnlySpan<byte> clientInboundIv, ReadOnlySpan<byte> clientOutboundIv)
    {
        if (sessionKeyMaterial.Length != SessionKeyMaterialLength)
        {
            throw new ArgumentException("The negotiated session key material must be exactly 64 bytes.",
                nameof(sessionKeyMaterial));
        }

        if (clientInboundIv.Length != GameCast5Cfb64Cipher.InitializationVectorLength ||
            clientOutboundIv.Length != GameCast5Cfb64Cipher.InitializationVectorLength)
        {
            throw new ArgumentException("Both negotiated directional IVs must contain eight bytes.");
        }

        _inbound = new GameCast5Cfb64Cipher(sessionKeyMaterial, clientInboundIv, encrypt: false);
        try
        {
            _outbound = new GameCast5Cfb64Cipher(sessionKeyMaterial, clientOutboundIv, encrypt: true);
        }
        catch
        {
            _inbound.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Takes one copy of the AUTH-001I result's native session material after
    /// its encrypted response has been issued. This is an OFFLINE construction
    /// step, not an acknowledgement that a GameServer received that response.
    /// </summary>
    internal static GameSessionCipher CreateFromExchange(GameClientDhExchange exchange)
    {
        ArgumentNullException.ThrowIfNull(exchange);

        Span<byte> key = stackalloc byte[SessionKeyMaterialLength];
        Span<byte> inboundIv = stackalloc byte[GameCast5Cfb64Cipher.InitializationVectorLength];
        Span<byte> outboundIv = stackalloc byte[GameCast5Cfb64Cipher.InitializationVectorLength];
        try
        {
            exchange.TransferSessionParameters(key, inboundIv, outboundIv);
            return new GameSessionCipher(key, inboundIv, outboundIv);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(inboundIv);
            CryptographicOperations.ZeroMemory(outboundIv);
        }
    }

    internal void DecryptInbound(ReadOnlySpan<byte> ciphertext, Span<byte> plaintext)
    {
        lock (_inboundGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _inbound.Transform(ciphertext, plaintext);
        }
    }

    internal void EncryptOutbound(ReadOnlySpan<byte> plaintext, Span<byte> ciphertext)
    {
        lock (_outboundGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _outbound.Transform(plaintext, ciphertext);
        }
    }

    public void Dispose()
    {
        // Consistent lock order: inbound before outbound. Disposal cannot
        // race either stream transform or release provider state mid-block.
        lock (_inboundGate)
        {
            lock (_outboundGate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _inbound.Dispose();
                _outbound.Dispose();
            }
        }
    }
}
