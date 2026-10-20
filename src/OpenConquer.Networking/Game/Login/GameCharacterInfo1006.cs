using System.Buffers.Binary;
using OpenConquer.Networking.Game.Framing;

namespace OpenConquer.Networking.Game.Login;

/// <summary>
/// Native 5517 inbound MsgUserInfo (1006) snapshot. Wire identities are
/// retained without mutating hero state, granting world admission or sending
/// the native EnterMap action. Historical compatibility names are evidence
/// attributions, not claims about active game mechanics.
/// </summary>
internal sealed record GameCharacterInfo1006
{
    internal const ushort PacketType = 1006;
    private const int StringCountOffset = 110;
    private const int MaximumStringLength = 15;
    private const int NativeNameBufferCapacity = MaximumStringLength + 1;

    // Native plaintext wire offset +4.
    internal uint EntityId
    {
        get;
        init;
    }
    // Native plaintext wire offset +8.
    internal ushort TransformLookSourceId
    {
        get;
        init;
    }
    // Native plaintext wire offset +10.
    internal uint PackedAppearance
    {
        get;
        init;
    }
    // Native plaintext wire offset +14.
    internal ushort HairComposite
    {
        get;
        init;
    }
    // Native plaintext wire offset +16.
    internal uint Silver
    {
        get;
        init;
    }
    // Native plaintext wire offset +20.
    internal uint ConquerPoints
    {
        get;
        init;
    }
    // Native plaintext wire offset +24.
    internal ulong Experience
    {
        get;
        init;
    }
    // Native plaintext wire offset +32.
    internal uint LegacyDeed
    {
        get;
        init;
    }
    // Native plaintext wire offset +36.
    internal uint LegacyMedal
    {
        get;
        init;
    }
    // Native plaintext wire offset +40.
    internal uint LegacyMedalSelect
    {
        get;
        init;
    }
    // Native plaintext wire offset +44.
    internal uint VirtuePoints
    {
        get;
        init;
    }
    // Native plaintext wire offset +48.
    internal uint EncodedPreRebirthLevel
    {
        get;
        init;
    }
    // Native plaintext wire offset +52.
    internal ushort Strength
    {
        get;
        init;
    }
    // Native plaintext wire offset +54.
    internal ushort Agility
    {
        get;
        init;
    }
    // Native plaintext wire offset +56.
    internal ushort Vitality
    {
        get;
        init;
    }
    // Native plaintext wire offset +58.
    internal ushort Spirit
    {
        get;
        init;
    }
    // Native plaintext wire offset +60.
    internal ushort UnspentAttributePoints
    {
        get;
        init;
    }
    // Native plaintext wire offset +62.
    internal ushort CurrentLife
    {
        get;
        init;
    }
    // Native plaintext wire offset +64.
    internal ushort CurrentMana
    {
        get;
        init;
    }
    // Native plaintext wire offset +66.
    internal short PkPoints
    {
        get;
        init;
    }
    // Native plaintext wire offset +68.
    internal byte Level
    {
        get;
        init;
    }
    // Native plaintext wire offset +69.
    internal byte CurrentProfession
    {
        get;
        init;
    }
    // Native plaintext wire offset +70.
    internal byte FirstProfession
    {
        get;
        init;
    }
    // Native plaintext wire offset +71.
    internal byte PreviousProfession
    {
        get;
        init;
    }
    // Native plaintext wire offset +72.
    internal byte LegacyNobility
    {
        get;
        init;
    }
    // Native plaintext wire offset +73.
    internal byte RebirthCount
    {
        get;
        init;
    }
    // Native plaintext wire offset +74.
    internal byte LegacyAutoAllot
    {
        get;
        init;
    }
    // Native plaintext wire offset +75.
    internal uint AuraTierScore
    {
        get;
        init;
    }
    // Native plaintext wire offset +79.
    internal ushort CoachPointsHundredths
    {
        get;
        init;
    }
    // Native plaintext wire offset +81.
    internal ushort CoachExperienceShareCount
    {
        get;
        init;
    }
    // Native plaintext wire offset +85.
    internal ushort CoachSessionState
    {
        get;
        init;
    }
    // Native plaintext wire offset +87.
    internal uint FlowerStatusTier
    {
        get;
        init;
    }
    // Native plaintext wire offset +91.
    internal ushort TitleId
    {
        get;
        init;
    }
    // Native plaintext wire offset +93.
    internal uint BoundConquerPoints
    {
        get;
        init;
    }
    // Native plaintext wire offset +97.
    internal byte ActiveSubProfessionId
    {
        get;
        init;
    }
    // Native plaintext wire offset +98.
    internal ulong PackedSubProfessionPhases
    {
        get;
        init;
    }
    // Native plaintext wire offset +106.
    internal uint RacePoints
    {
        get;
        init;
    }
    // Raw owned bytes. Native setters retain C-string prefixes and defer
    // any code-page interpretation until a separate rendering consumer.
    internal ReadOnlyMemory<byte> PlayerNameBytes
    {
        get;
        init;
    }
    internal ReadOnlyMemory<byte> SpouseNameBytes
    {
        get;
        init;
    }

    internal static GameCharacterInfo1006 Read(ReadOnlySpan<byte> packet)
    {
        if (packet.Length is < GameSecuredFrameProtocol.HeaderLength or >
                GameSecuredFrameProtocol.MaximumPacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != packet.Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != PacketType)
        {
            throw new InvalidDataException("GameServer character information is not a valid 1006 packet.");
        }

        // Native constructor zeroes 1024 bytes before copying the received
        // packet. Missing fixed fields or string bytes consequently read as
        // zeros. Reproduce the defined in-buffer behavior without emulating
        // unchecked native reads or exposing pooled network memory.
        Span<byte> snapshot = stackalloc byte[GameSecuredFrameProtocol.MaximumPacketLength];
        snapshot.Clear();
        packet.CopyTo(snapshot);

        int offset = StringCountOffset + 1;
        byte count = snapshot[StringCountOffset];
        byte[] playerName = count >= 1 ? ReadNameEntry(snapshot, ref offset, retain: true) : [];
        if (count >= 2)
        {
            _ = ReadNameEntry(snapshot, ref offset, retain: false);
        }

        byte[] spouseName = count >= 3 ? ReadNameEntry(snapshot, ref offset, retain: true) : [];

        return new GameCharacterInfo1006
        {
            EntityId = BinaryPrimitives.ReadUInt32LittleEndian(snapshot[4..]),
            TransformLookSourceId = BinaryPrimitives.ReadUInt16LittleEndian(snapshot[8..]),
            PackedAppearance = BinaryPrimitives.ReadUInt32LittleEndian(snapshot[10..]),
            HairComposite = BinaryPrimitives.ReadUInt16LittleEndian(snapshot[14..]),
            Silver = BinaryPrimitives.ReadUInt32LittleEndian(snapshot[16..]),
            ConquerPoints = BinaryPrimitives.ReadUInt32LittleEndian(snapshot[20..]),
            Experience = BinaryPrimitives.ReadUInt64LittleEndian(snapshot[24..]),
            LegacyDeed = BinaryPrimitives.ReadUInt32LittleEndian(snapshot[32..]),
            LegacyMedal = BinaryPrimitives.ReadUInt32LittleEndian(snapshot[36..]),
            LegacyMedalSelect = BinaryPrimitives.ReadUInt32LittleEndian(snapshot[40..]),
            VirtuePoints = BinaryPrimitives.ReadUInt32LittleEndian(snapshot[44..]),
            EncodedPreRebirthLevel = BinaryPrimitives.ReadUInt32LittleEndian(snapshot[48..]),
            Strength = BinaryPrimitives.ReadUInt16LittleEndian(snapshot[52..]),
            Agility = BinaryPrimitives.ReadUInt16LittleEndian(snapshot[54..]),
            Vitality = BinaryPrimitives.ReadUInt16LittleEndian(snapshot[56..]),
            Spirit = BinaryPrimitives.ReadUInt16LittleEndian(snapshot[58..]),
            UnspentAttributePoints = BinaryPrimitives.ReadUInt16LittleEndian(snapshot[60..]),
            CurrentLife = BinaryPrimitives.ReadUInt16LittleEndian(snapshot[62..]),
            CurrentMana = BinaryPrimitives.ReadUInt16LittleEndian(snapshot[64..]),
            PkPoints = BinaryPrimitives.ReadInt16LittleEndian(snapshot[66..]),
            Level = snapshot[68],
            CurrentProfession = snapshot[69],
            FirstProfession = snapshot[70],
            PreviousProfession = snapshot[71],
            LegacyNobility = snapshot[72],
            RebirthCount = snapshot[73],
            LegacyAutoAllot = snapshot[74],
            AuraTierScore = BinaryPrimitives.ReadUInt32LittleEndian(snapshot[75..]),
            CoachPointsHundredths = BinaryPrimitives.ReadUInt16LittleEndian(snapshot[79..]),
            CoachExperienceShareCount = BinaryPrimitives.ReadUInt16LittleEndian(snapshot[81..]),
            CoachSessionState = BinaryPrimitives.ReadUInt16LittleEndian(snapshot[85..]),
            FlowerStatusTier = BinaryPrimitives.ReadUInt32LittleEndian(snapshot[87..]),
            TitleId = BinaryPrimitives.ReadUInt16LittleEndian(snapshot[91..]),
            BoundConquerPoints = BinaryPrimitives.ReadUInt32LittleEndian(snapshot[93..]),
            ActiveSubProfessionId = snapshot[97],
            PackedSubProfessionPhases = BinaryPrimitives.ReadUInt64LittleEndian(snapshot[98..]),
            RacePoints = BinaryPrimitives.ReadUInt32LittleEndian(snapshot[106..]),
            PlayerNameBytes = playerName,
            SpouseNameBytes = spouseName
        };
    }

    private static byte[] ReadNameEntry(ReadOnlySpan<byte> snapshot, ref int offset, bool retain)
    {
        if (offset >= snapshot.Length)
        {
            // Bounded native indices 0..2 never reach this on 1024-byte storage.
            throw new InvalidDataException("GameServer character string offset exceeds bounded storage.");
        }

        int length = snapshot[offset++];
        if (length > snapshot.Length - offset)
        {
            throw new InvalidDataException("GameServer character string exceeds bounded storage.");
        }

        ReadOnlySpan<byte> entry = snapshot.Slice(offset, length);
        offset += length;

        // The reader zeros its local destination when the selected entry is
        // missing or length >= 16. Index 1 is discarded, but its declared
        // length still determines the position of index 2.
        if (!retain || length >= NativeNameBufferCapacity)
        {
            return [];
        }

        int terminator = entry.IndexOf((byte)0);
        if (terminator >= 0)
        {
            entry = entry[..terminator];
        }

        return entry.ToArray();
    }
}
