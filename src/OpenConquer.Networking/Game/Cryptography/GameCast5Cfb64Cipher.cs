using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace OpenConquer.Networking.Game.Cryptography;

/// <summary>
/// One-direction native 5517 CAST5 CFB-64 stream. The owner serializes calls,
/// supplies the IV, and disposes this primitive when the envelope or session ends.
/// </summary>
internal sealed class GameCast5Cfb64Cipher : IDisposable
{
    internal const int InitializationVectorLength = 8;
    internal const int MaximumKeyLength = 16;

    private readonly Cast5Engine _engine = new();
    private readonly byte[] _feedback = new byte[InitializationVectorLength];
    private readonly byte[] _keystream = new byte[InitializationVectorLength];
    private readonly bool _encrypt;

    private int _position;
    private bool _disposed;

    internal GameCast5Cfb64Cipher(ReadOnlySpan<byte> key, ReadOnlySpan<byte> initializationVector, bool encrypt)
    {
        if (key.IsEmpty)
        {
            throw new ArgumentException("CAST5 key material cannot be empty.", nameof(key));
        }

        if (initializationVector.Length != InitializationVectorLength)
        {
            throw new ArgumentException("CAST5 initialization vector must be exactly eight bytes.", nameof(initializationVector));
        }

        byte[] normalizedKey = key[..Math.Min(key.Length, MaximumKeyLength)].ToArray();
        try
        {
            _engine.Init(true, new KeyParameter(normalizedKey));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(normalizedKey);
        }

        initializationVector.CopyTo(_feedback);
        _encrypt = encrypt;
    }

    internal void Transform(ReadOnlySpan<byte> input, Span<byte> output)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output must be at least as large as input.", nameof(output));
        }

        if (input.Overlaps(output, out int offset) && offset != 0)
        {
            throw new ArgumentException("Input and output may match exactly but must not partially overlap.", nameof(output));
        }

        for (int index = 0; index < input.Length; index++)
        {
            if (_position == 0)
            {
                _engine.ProcessBlock(_feedback, 0, _keystream, 0);
            }

            byte sourceByte = input[index];
            if (_encrypt)
            {
                byte encrypted = (byte)(sourceByte ^ _keystream[_position]);
                output[index] = encrypted;
                _feedback[_position] = encrypted;
            }
            else
            {
                output[index] = (byte)(sourceByte ^ _keystream[_position]);
                _feedback[_position] = sourceByte;
            }

            _position = (_position + 1) & (InitializationVectorLength - 1);
        }
    }

    public void Dispose()
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
