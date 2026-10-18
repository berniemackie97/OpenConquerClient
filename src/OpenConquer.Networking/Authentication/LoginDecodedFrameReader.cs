using System.Buffers;
using System.Buffers.Binary;

namespace OpenConquer.Networking.Authentication;

/// <summary>
/// Parses account-login frames that are already decrypted, without copying. The caller is responsible for the
/// PipeReader or sequence, the buffer limit, the cipher and how long returned slices stay valid.
/// </summary>
internal static class LoginDecodedFrameReader
{
    internal const int HeaderLength = 4;
    internal const int MaximumFrameLength = 1024;

    /// <summary>
    /// Takes one complete [length:u16][type:u16][body] frame and leaves any later bytes in <paramref name="input"/>.
    /// Incomplete input is left unchanged. The returned frame points into the caller's memory and is valid only
    /// until that memory is reused or released.
    /// </summary>
    /// <returns><see langword="true"/> for a complete, valid frame; <see langword="false"/> only
    /// when more decoded bytes are required.</returns>
    /// <exception cref="InvalidDataException">The length is out of range or the packet type is zero.
    /// The caller must disconnect: this isn't incomplete data, and the stream can't be resynchronized.</exception>
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
