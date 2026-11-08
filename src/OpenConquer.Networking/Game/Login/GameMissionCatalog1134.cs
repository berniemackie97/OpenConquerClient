using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace OpenConquer.Networking.Game.Login;

internal static class GameMissionCatalog1134
{
    internal const ushort PacketType = 1134;
    internal const ushort CatalogSubtype = 3;
    internal const int MaximumMissionsPerFrame = 80;

    private const int HeaderLength = 8;
    private const int RecordLength = 12;

    internal static int GetPacketLength(int missionCount) => GetPacketLength(missionCount, nameof(missionCount));

    private static int GetPacketLength(int missionCount, string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(missionCount, 1, parameterName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(missionCount, MaximumMissionsPerFrame, parameterName);
        return HeaderLength + missionCount * RecordLength;
    }

    internal static void Write(Span<byte> destination, ReadOnlySpan<int> missionIds)
    {
        int length = GetPacketLength(missionIds.Length, nameof(missionIds));
        if (destination.Length != length)
        {
            throw new ArgumentException("The mission catalog frame length does not match its record count.", nameof(destination));
        }

        if (MemoryMarshal.AsBytes(missionIds).Overlaps(destination))
        {
            throw new ArgumentException("Mission IDs cannot overlap destination storage.", nameof(missionIds));
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
