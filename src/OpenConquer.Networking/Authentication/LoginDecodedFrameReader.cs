using System.Buffers;
using System.Buffers.Binary;

namespace OpenConquer.Networking.Authentication;

/// <summary>
/// Zero-copy parser for already-decrypted native account-login frames. The caller owns the
/// PipeReader/sequence, buffering limit, cipher stream, and lifetime of returned slices.
/// </summary>
internal static class LoginDecodedFrameReader
{
    internal const int HeaderLength = 4;
    internal const int MaximumFrameLength = 1024;

    /// <summary>
    /// Takes exactly one complete [length:u16][type:u16][body] frame, leaving any later bytes
    /// in <paramref name="input"/>. Incomplete input is unchanged; invalid headers fail closed.
    /// </summary>
    internal static bool TryRead(ref ReadOnlySequence<byte> input, out ReadOnlySequence<byte> frame)
    {
        frame = default;
        if (input.Length < sizeof(ushort))
        {
            return false;
        }

        Span<byte> header = stackalloc byte[HeaderLength];
        input.Slice(0, sizeof(ushort)).CopyTo(header);
        ushort length = BinaryPrimitives.ReadUInt16LittleEndian(header);
        if (length is < HeaderLength or > MaximumFrameLength)
        {
            throw new InvalidDataException("AccountServer frame length is outside the native bounds.");
        }

        if (input.Length < HeaderLength)
        {
            return false;
        }

        input.Slice(0, HeaderLength).CopyTo(header);
        if (BinaryPrimitives.ReadUInt16LittleEndian(header[sizeof(ushort)..]) == 0)
        {
            throw new InvalidDataException("AccountServer frame type zero is invalid.");
        }

        if (input.Length < length)
        {
            return false;
        }

        frame = input.Slice(0, length);
        input = input.Slice(length);
        return true;
    }
}
