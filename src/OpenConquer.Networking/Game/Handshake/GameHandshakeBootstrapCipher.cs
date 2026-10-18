using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace OpenConquer.Networking.Game.Handshake;

/// <summary>
/// The native 5517 GameServer's fixed-key CAST5 CFB-64 bootstrap transform.
/// One instance maintains feedback state for exactly one direction of one envelope.
/// This is protocol obfuscation, not peer authentication or authenticated encryption.
/// </summary>
internal sealed class GameHandshakeBootstrapCipher : IDisposable
{
    internal const int InitializationVectorLength = 8;

    private static ReadOnlySpan<byte> BootstrapKey => "BC234xs45nme7HU9"u8;

    private readonly object _gate = new();
    private readonly Cast5Engine _engine = new();
    private readonly byte[] _feedback = new byte[InitializationVectorLength];
    private readonly byte[] _keystream = new byte[InitializationVectorLength];
    private readonly bool _encrypt;

    private int _position;
    private bool _disposed;

    private GameHandshakeBootstrapCipher(bool encrypt)
    {
        byte[] key = BootstrapKey.ToArray();
        try
        {
            _engine.Init(true, new KeyParameter(key));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        _encrypt = encrypt;
    }

    internal static GameHandshakeBootstrapCipher CreateDecryptor() => new(encrypt: false);

    internal static GameHandshakeBootstrapCipher CreateEncryptor() => new(encrypt: true);

    /// <summary>
    /// Transforms a fragment while retaining the CFB-64 feedback position.
    /// Equal input/output buffers are permitted; partially overlapping buffers are rejected.
    /// Never reuse an instance for another envelope or after disposal.
    /// </summary>
    internal void Transform(ReadOnlySpan<byte> input, Span<byte> output)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (output.Length < input.Length)
            {
                throw new ArgumentException("Output is smaller than the source fragment.", nameof(output));
            }

            if (input.Overlaps(output, out int elementOffset) && elementOffset != 0)
            {
                throw new ArgumentException("Source and destination must be identical or non-overlapping.", nameof(output));
            }

            for (int index = 0; index < input.Length; index++)
            {
                if (_position == 0)
                {
                    _engine.ProcessBlock(_feedback, 0, _keystream, 0);
                }

                byte inputByte = input[index];
                if (_encrypt)
                {
                    byte encrypted = (byte)(inputByte ^ _keystream[_position]);
                    output[index] = encrypted;
                    _feedback[_position] = encrypted;
                }
                else
                {
                    output[index] = (byte)(inputByte ^ _keystream[_position]);
                    _feedback[_position] = inputByte;
                }

                _position = (_position + 1) & (InitializationVectorLength - 1);
            }
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
            CryptographicOperations.ZeroMemory(_feedback);
            CryptographicOperations.ZeroMemory(_keystream);
            _position = 0;
        }
    }
}
