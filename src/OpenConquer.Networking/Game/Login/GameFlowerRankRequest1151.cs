using System.Buffers.Binary;

namespace OpenConquer.Networking.Game.Login;

/// <summary>
/// Client flower-rank request (1151), as <c>CMsgFlowerRank__BuildSize80</c> 0x706D7D writes it: size 80, subtype byte at
/// +4, u32 parameter at +8, byte at +12, everything else zero.
/// </summary>
internal static class GameFlowerRankRequest1151
{
    internal const ushort PacketType = 1151;
    internal const int PacketLength = 80;

    /// <summary>
    /// Subtype <c>CMsgUserInfo__Process</c> sends right after its EnterMap action when the hero's body digit
    /// (appearance / 1000 % 10, <c>hero+0x178</c>) is 2 (0x7648B4..0x7648E6), with both parameters zero.
    /// </summary>
    internal const byte EnterMapSubtype = 2;

    internal static void Write(Span<byte> destination, byte subtype, uint parameter = 0, byte parameter2 = 0)
    {
        if (destination.Length != PacketLength)
        {
            throw new ArgumentException("A 1151 request must occupy exactly 80 bytes.", nameof(destination));
        }

        destination.Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(destination, PacketLength);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], PacketType);
        destination[4] = subtype;
        BinaryPrimitives.WriteUInt32LittleEndian(destination[8..], parameter);
        destination[12] = parameter2;
    }

    /// <summary>True when native sends <see cref="EnterMapSubtype"/> after EnterMap for this packed appearance.</summary>
    internal static bool IsSentAfterEnterMap(uint packedAppearance) => packedAppearance / 1000 % 10 == 2;
}
