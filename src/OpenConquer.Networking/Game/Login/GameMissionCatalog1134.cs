using System.Buffers.Binary;

namespace OpenConquer.Networking.Game.Login;

internal static class GameMissionCatalog1134
{
    internal const ushort PacketType = 1134;
    internal const ushort CatalogSubtype = 3;
    internal const int MaximumMissionsPerFrame = 80;

    private const int HeaderLength = 8;
    private const int RecordLength = 12;

    internal static int GetPacketLength(int missionCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(missionCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(missionCount, MaximumMissionsPerFrame);
        return HeaderLength + missionCount * RecordLength;
    }

    internal static void Write(Span<byte> destination, ReadOnlySpan<int> missionIds)
    {
        int length = GetPacketLength(missionIds.Length);
        if (destination.Length != length)
        {
            throw new ArgumentException("The mission catalog frame length does not match its record count.", nameof(destination));
        }

        int previousId = 0;
        foreach (int missionId in missionIds)
        {
            if (missionId <= previousId)
            {
                throw new ArgumentException("Mission IDs must be positive and strictly ascending.", nameof(missionIds));
            }

            previousId = missionId;
        }

        destination.Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(destination, checked((ushort)length));
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], PacketType);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[4..], CatalogSubtype);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[6..], checked((ushort)missionIds.Length));

        for (int index = 0; index < missionIds.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination[(HeaderLength + index * RecordLength)..], missionIds[index]);
        }
    }
}
