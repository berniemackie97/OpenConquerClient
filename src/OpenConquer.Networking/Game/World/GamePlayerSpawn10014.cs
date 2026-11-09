using OpenConquer.Networking.Game.Framing;

namespace OpenConquer.Networking.Game.World;

internal sealed record GamePlayerSpawn10014(
    uint EntityId, ReadOnlyMemory<byte> NameBytes, uint PackedAppearance, ushort HairComposite,
    ushort X, ushort Y, byte Direction, uint Action, uint Life, ushort Level,
    uint HeadwearItemType, uint ArmorItemType, uint LeftHandItemType, uint RightHandItemType,
    ulong StatusMaskLow, ulong StatusMaskHigh)
{
    internal const ushort PacketType = 10014;
    private const int StringCountOffset = 218;
    private const int FirstStringLengthOffset = StringCountOffset + 1;
    private const int MaximumNameLength = 15;
    private const int MinimumPacketLength = FirstStringLengthOffset + 1;

    internal static bool TryRead(GameMessageBuffer message, out GamePlayerSpawn10014? spawn)
    {
        spawn = null;
        if (message.Length < MinimumPacketLength || message.Type != PacketType)
        {
            return false;
        }

        int count = message.ReadByte(StringCountOffset);
        if (count is < 1 or > 4)
        {
            return false;
        }

        int offset = FirstStringLengthOffset;
        int nameStart = 0;
        int nameLength = 0;
        for (int index = 0; index < count; index++)
        {
            if (offset >= message.Length)
            {
                return false;
            }

            int length = message.ReadByte(offset++);
            if (length > message.Length - offset)
            {
                return false;
            }

            if (index == 0)
            {
                if (length is < 1 or > MaximumNameLength)
                {
                    return false;
                }

                nameStart = offset;
                nameLength = length;
            }

            offset += length;
        }

        if (offset != message.Length || message.ReadByte(90) > 7)
        {
            return false;
        }

        byte[] name = new byte[nameLength];
        for (int index = 0; index < name.Length; index++)
        {
            name[index] = message.ReadByte(nameStart + index);
        }

        spawn = new GamePlayerSpawn10014(
            message.ReadUInt32(8), name, message.ReadUInt32(4), message.ReadUInt16(84),
            message.ReadUInt16(86), message.ReadUInt16(88), message.ReadByte(90),
            message.ReadUInt32(91), message.ReadUInt32(80), message.ReadUInt16(99),
            message.ReadUInt32(182), message.ReadUInt32(186), message.ReadUInt32(190),
            message.ReadUInt32(194), message.ReadUInt64(22), message.ReadUInt64(30));
        return true;
    }
}
