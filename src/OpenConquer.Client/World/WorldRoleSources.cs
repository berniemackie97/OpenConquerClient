using System.Buffers.Binary;

namespace OpenConquer.Client.World;

internal readonly record struct WorldRoleSources(uint BodyModelId, uint ArmetModelId)
{
    private const uint ModelFamilyScale = 1_000_000;
    private const uint LinkedHairFamily = 0x1D0D8;
    private const ushort ItemPacketType = 1008;
    private const ushort EquipmentPacketType = 1009;
    private const ushort ActiveEquipmentSubtype = 46;

    internal static WorldRoleSources Resolve(uint packedAppearance, ushort hairComposite,
        IReadOnlyList<byte[]> hydrationPackets)
    {
        uint bodyModel = packedAppearance % 1000 * ModelFamilyScale;
        uint hairStyle = (uint)hairComposite % 1000;
        uint armetModel = hairStyle % 100 == 0 ? 0 : bodyModel + LinkedHairFamily + hairStyle;

        Dictionary<uint, (uint TypeId, byte Placement)> items = [];
        byte[]? activeEquipment = null;

        foreach (byte[] packet in hydrationPackets)
        {
            if (packet.Length < 4)
            {
                throw new InvalidDataException("World-entry hydration contains a truncated packet header.");
            }

            ushort packetType = BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2));
            if (packetType == ItemPacketType)
            {
                if (packet.Length != 68)
                {
                    throw new InvalidDataException("World-entry item snapshot has an invalid length.");
                }

                if (packet[16] == 1)
                {
                    uint itemId = BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(4));
                    uint typeId = BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(8));
                    if (itemId == 0 || typeId == 0)
                    {
                        throw new InvalidDataException("World-entry item snapshot has an invalid item identity.");
                    }

                    items[itemId] = (typeId, packet[18]);
                }
            }
            else if (packetType == EquipmentPacketType)
            {
                if (packet.Length < 14)
                {
                    throw new InvalidDataException("World-entry equipment packet is truncated.");
                }

                if (BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(12)) != ActiveEquipmentSubtype)
                {
                    continue;
                }

                if (packet.Length != 88)
                {
                    throw new InvalidDataException("World-entry active-equipment snapshot has an invalid length.");
                }

                if (BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(8)) == 0)
                {
                    activeEquipment = packet;
                }
            }
        }

        if (activeEquipment is not null)
        {
            uint headwearId = BinaryPrimitives.ReadUInt32LittleEndian(activeEquipment.AsSpan(32));
            uint armorId = BinaryPrimitives.ReadUInt32LittleEndian(activeEquipment.AsSpan(40));

            if (headwearId != 0)
            {
                armetModel = bodyModel + NormalizeItemSource(FindEquippedType(items, headwearId, 1));
            }

            if (armorId != 0)
            {
                bodyModel += NormalizeItemSource(FindEquippedType(items, armorId, 3));
            }
        }

        return new WorldRoleSources(bodyModel, armetModel);
    }

    private static uint FindEquippedType(Dictionary<uint, (uint TypeId, byte Placement)> items,
        uint itemId, byte placement)
    {
        if (!items.TryGetValue(itemId, out (uint TypeId, byte Placement) item) ||
            item.Placement != placement)
        {
            throw new InvalidDataException("World-entry active equipment references an unavailable item slot.");
        }

        return item.TypeId;
    }

    private static uint NormalizeItemSource(uint itemTypeId) =>
        (itemTypeId % ModelFamilyScale / 10) * 10;
}
