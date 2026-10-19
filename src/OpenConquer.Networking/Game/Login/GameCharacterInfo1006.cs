using System.Buffers.Binary;
using System.Text;
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
    private const int StringCount = 3;
    private const int MaximumStringLength = 15;
    private const int MinimumPacketLength = StringCountOffset + 1 + StringCount;
    private static readonly Encoding s_ansi = CreateAnsiEncoding();

    // Native plaintext wire offset +4.
    internal uint EntityId { get; init; }
    // Native plaintext wire offset +8.
    internal ushort TransformLookSourceId { get; init; }
    // Native plaintext wire offset +10.
    internal uint PackedAppearance { get; init; }
    // Native plaintext wire offset +14.
    internal ushort HairComposite { get; init; }
    // Native plaintext wire offset +16.
    internal uint Silver { get; init; }
    // Native plaintext wire offset +20.
    internal uint ConquerPoints { get; init; }
    // Native plaintext wire offset +24.
    internal ulong Experience { get; init; }
    // Native plaintext wire offset +32.
    internal uint LegacyDeed { get; init; }
    // Native plaintext wire offset +36.
    internal uint LegacyMedal { get; init; }
    // Native plaintext wire offset +40.
    internal uint LegacyMedalSelect { get; init; }
    // Native plaintext wire offset +44.
    internal uint VirtuePoints { get; init; }
    // Native plaintext wire offset +48.
    internal uint EncodedPreRebirthLevel { get; init; }
    // Native plaintext wire offset +52.
    internal ushort Strength { get; init; }
    // Native plaintext wire offset +54.
    internal ushort Agility { get; init; }
    // Native plaintext wire offset +56.
    internal ushort Vitality { get; init; }
    // Native plaintext wire offset +58.
    internal ushort Spirit { get; init; }
    // Native plaintext wire offset +60.
    internal ushort UnspentAttributePoints { get; init; }
    // Native plaintext wire offset +62.
    internal ushort CurrentLife { get; init; }
    // Native plaintext wire offset +64.
    internal ushort CurrentMana { get; init; }
    // Native plaintext wire offset +66.
    internal short PkPoints { get; init; }
    // Native plaintext wire offset +68.
    internal byte Level { get; init; }
    // Native plaintext wire offset +69.
    internal byte CurrentProfession { get; init; }
    // Native plaintext wire offset +70.
    internal byte FirstProfession { get; init; }
    // Native plaintext wire offset +71.
    internal byte PreviousProfession { get; init; }
    // Native plaintext wire offset +72.
    internal byte LegacyNobility { get; init; }
    // Native plaintext wire offset +73.
    internal byte RebirthCount { get; init; }
    // Native plaintext wire offset +74.
    internal byte LegacyAutoAllot { get; init; }
    // Native plaintext wire offset +75.
    internal uint AuraTierScore { get; init; }
    // Native plaintext wire offset +79.
    internal ushort CoachPointsHundredths { get; init; }
    // Native plaintext wire offset +81.
    internal ushort CoachExperienceShareCount { get; init; }
    // Native plaintext wire offset +85.
    internal ushort CoachSessionState { get; init; }
    // Native plaintext wire offset +87.
    internal uint FlowerStatusTier { get; init; }
    // Native plaintext wire offset +91.
    internal ushort TitleId { get; init; }
    // Native plaintext wire offset +93.
    internal uint BoundConquerPoints { get; init; }
    // Native plaintext wire offset +97.
    internal byte ActiveSubProfessionId { get; init; }
    // Native plaintext wire offset +98.
    internal ulong PackedSubProfessionPhases { get; init; }
    // Native plaintext wire offset +106.
    internal uint RacePoints { get; init; }
    internal string PlayerName { get; init; } = string.Empty;
    internal string SpouseName { get; init; } = string.Empty;

    internal static GameCharacterInfo1006 Read(ReadOnlySpan<byte> packet)
    {
        if (packet.Length is < MinimumPacketLength or > GameSecuredFrameProtocol.MaximumPacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != packet.Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != PacketType)
        {
            throw new InvalidDataException("GameServer character information is not a valid 1006 packet.");
        }

        if (packet[StringCountOffset] != StringCount)
        {
            throw new InvalidDataException("GameServer character information requires exactly three string entries.");
        }

        int offset = StringCountOffset + 1;
        string playerName = ReadNameEntry(packet, ref offset) ??
            throw new InvalidDataException("GameServer character name is absent.");
        _ = ReadNameEntry(packet, ref offset, retain: false);
        string spouseName = ReadNameEntry(packet, ref offset) ??
            throw new InvalidDataException("GameServer character spouse name is absent.");

        if (offset != packet.Length)
        {
            throw new InvalidDataException("GameServer character information has trailing string-list bytes.");
        }

        return new GameCharacterInfo1006
        {
            EntityId = BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]),
            TransformLookSourceId = BinaryPrimitives.ReadUInt16LittleEndian(packet[8..]),
            PackedAppearance = BinaryPrimitives.ReadUInt32LittleEndian(packet[10..]),
            HairComposite = BinaryPrimitives.ReadUInt16LittleEndian(packet[14..]),
            Silver = BinaryPrimitives.ReadUInt32LittleEndian(packet[16..]),
            ConquerPoints = BinaryPrimitives.ReadUInt32LittleEndian(packet[20..]),
            Experience = BinaryPrimitives.ReadUInt64LittleEndian(packet[24..]),
            LegacyDeed = BinaryPrimitives.ReadUInt32LittleEndian(packet[32..]),
            LegacyMedal = BinaryPrimitives.ReadUInt32LittleEndian(packet[36..]),
            LegacyMedalSelect = BinaryPrimitives.ReadUInt32LittleEndian(packet[40..]),
            VirtuePoints = BinaryPrimitives.ReadUInt32LittleEndian(packet[44..]),
            EncodedPreRebirthLevel = BinaryPrimitives.ReadUInt32LittleEndian(packet[48..]),
            Strength = BinaryPrimitives.ReadUInt16LittleEndian(packet[52..]),
            Agility = BinaryPrimitives.ReadUInt16LittleEndian(packet[54..]),
            Vitality = BinaryPrimitives.ReadUInt16LittleEndian(packet[56..]),
            Spirit = BinaryPrimitives.ReadUInt16LittleEndian(packet[58..]),
            UnspentAttributePoints = BinaryPrimitives.ReadUInt16LittleEndian(packet[60..]),
            CurrentLife = BinaryPrimitives.ReadUInt16LittleEndian(packet[62..]),
            CurrentMana = BinaryPrimitives.ReadUInt16LittleEndian(packet[64..]),
            PkPoints = BinaryPrimitives.ReadInt16LittleEndian(packet[66..]),
            Level = packet[68],
            CurrentProfession = packet[69],
            FirstProfession = packet[70],
            PreviousProfession = packet[71],
            LegacyNobility = packet[72],
            RebirthCount = packet[73],
            LegacyAutoAllot = packet[74],
            AuraTierScore = BinaryPrimitives.ReadUInt32LittleEndian(packet[75..]),
            CoachPointsHundredths = BinaryPrimitives.ReadUInt16LittleEndian(packet[79..]),
            CoachExperienceShareCount = BinaryPrimitives.ReadUInt16LittleEndian(packet[81..]),
            CoachSessionState = BinaryPrimitives.ReadUInt16LittleEndian(packet[85..]),
            FlowerStatusTier = BinaryPrimitives.ReadUInt32LittleEndian(packet[87..]),
            TitleId = BinaryPrimitives.ReadUInt16LittleEndian(packet[91..]),
            BoundConquerPoints = BinaryPrimitives.ReadUInt32LittleEndian(packet[93..]),
            ActiveSubProfessionId = packet[97],
            PackedSubProfessionPhases = BinaryPrimitives.ReadUInt64LittleEndian(packet[98..]),
            RacePoints = BinaryPrimitives.ReadUInt32LittleEndian(packet[106..]),
            PlayerName = playerName,
            SpouseName = spouseName
        };
    }

    private static string? ReadNameEntry(ReadOnlySpan<byte> packet, ref int offset, bool retain = true)
    {
        if (offset >= packet.Length)
        {
            throw new InvalidDataException("GameServer character information has a truncated string list.");
        }

        int length = packet[offset++];
        if (length > MaximumStringLength || length > packet.Length - offset)
        {
            throw new InvalidDataException("GameServer character information has an invalid name-entry length.");
        }

        ReadOnlySpan<byte> entry = packet.Slice(offset, length);
        offset += length;
        foreach (byte character in entry)
        {
            // A Windows-1252 provider may substitute control codes instead of
            // raising DecoderFallbackException for these undefined byte slots.
            if (character is 0x81 or 0x8D or 0x8F or 0x90 or 0x9D)
            {
                throw new InvalidDataException("GameServer character information contains an undefined ANSI byte.");
            }
        }

        // String entry 1 is demonstrably discarded by the native 1006 handler,
        // but its framing remains essential to locate the third (spouse) entry.
        if (!retain)
        {
            return null;
        }

        if (entry.Contains((byte)0))
        {
            throw new InvalidDataException("GameServer character information contains an embedded name NUL.");
        }

        try
        {
            return s_ansi.GetString(entry);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("GameServer character information contains invalid ANSI name text.", exception);
        }
    }

    private static Encoding CreateAnsiEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }
}
