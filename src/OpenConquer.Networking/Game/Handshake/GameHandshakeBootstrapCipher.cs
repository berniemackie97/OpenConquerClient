using OpenConquer.Networking.Game.Cryptography;

namespace OpenConquer.Networking.Game.Handshake;

/// <summary>
/// Fixed-key native 5517 bootstrap CAST5 CFB-64 compatibility adapter.
/// An instance owns one directional envelope; no shared mutable cipher state.
/// </summary>
internal sealed class GameHandshakeBootstrapCipher : IDisposable
{
    internal const int InitializationVectorLength = GameCast5Cfb64Cipher.InitializationVectorLength;

    private static ReadOnlySpan<byte> BootstrapKey => "BC234xs45nme7HU9"u8;

    private readonly Lock _gate = new();
    private readonly GameCast5Cfb64Cipher _cipher;

    private bool _disposed;

    private GameHandshakeBootstrapCipher(bool encrypt)
    {
        Span<byte> initializationVector = stackalloc byte[InitializationVectorLength];
        initializationVector.Clear();
        _cipher = new GameCast5Cfb64Cipher(BootstrapKey, initializationVector, encrypt);
    }

    internal static GameHandshakeBootstrapCipher CreateDecryptor() => new(encrypt: false);

    internal static GameHandshakeBootstrapCipher CreateEncryptor() => new(encrypt: true);

    /// <summary>
    /// Preserves CFB feedback between fragments. Identical source/destination
    /// spans are allowed; partial overlap and undersized outputs fail atomically.
    /// </summary>
    internal void Transform(ReadOnlySpan<byte> input, Span<byte> output)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _cipher.Transform(input, output);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cipher.Dispose();
        }
    }
}
