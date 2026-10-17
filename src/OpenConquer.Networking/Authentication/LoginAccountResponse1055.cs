using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OpenConquer.Networking.Authentication;

/// <summary>
/// Parsed fields of a 5517 AccountServer authentication response. This is untrusted wire data;
/// parsing does not authorize world admission, credential success, or endpoint connection.
/// </summary>
internal readonly record struct LoginAccountResponse1055(
    uint SessionToken, uint AuthenticationKeyOrFailureCode, uint GamePort,
    uint AdditionalSessionField, string? GameHost)
{
    internal const ushort PacketType = 1055;
    internal const int FrameLength = 36;
    internal bool IsSuccess => SessionToken != 0;

    internal static bool TryRead(ReadOnlySpan<byte> frame, out LoginAccountResponse1055 response)
    {
        response = default;
        if (frame.Length != FrameLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(frame) != FrameLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(frame[2..]) != PacketType)
        {
            return false;
        }

        uint token = BinaryPrimitives.ReadUInt32LittleEndian(frame[4..]);
        uint keyOrCode = BinaryPrimitives.ReadUInt32LittleEndian(frame[8..]);
        if (token == 0)
        {
            // The native failure path reads only its code; ignore all unused success fields.
            response = new LoginAccountResponse1055(0, keyOrCode, 0, 0, null);
            return true;
        }

        uint port = BinaryPrimitives.ReadUInt32LittleEndian(frame[12..]);
        uint additional = BinaryPrimitives.ReadUInt32LittleEndian(frame[16..]);
        ReadOnlySpan<byte> hostBytes = frame.Slice(20, 16);
        int terminator = hostBytes.IndexOf((byte)0);
        if (port is < 1 or > 65535 || terminator is < 7 or > 15)
        {
            return false;
        }

        // Reject malformed C-strings, non-ASCII, shorthand and unusual IPv4 spellings.
        foreach (byte trailing in hostBytes[(terminator + 1)..])
        {
            if (trailing != 0)
            {
                return false;
            }
        }
        ReadOnlySpan<byte> addressBytes = hostBytes[..terminator];
        foreach (byte character in addressBytes)
        {
            if (character != (byte)'.' && (character < (byte)'0' || character > (byte)'9'))
            {
                return false;
            }
        }
        string host = Encoding.ASCII.GetString(addressBytes);
        if (!IPAddress.TryParse(host, out IPAddress? ip) || ip.AddressFamily != AddressFamily.InterNetwork ||
            !string.Equals(ip.ToString(), host, StringComparison.Ordinal))
        {
            return false;
        }

        response = new LoginAccountResponse1055(token, keyOrCode, port, additional, host);
        return true;
    }
}
