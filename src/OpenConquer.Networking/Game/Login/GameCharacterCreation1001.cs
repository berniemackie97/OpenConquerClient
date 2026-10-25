using System.Buffers.Binary;

namespace OpenConquer.Networking.Game.Login;

/// <summary>
/// Native 5517 create-role request and cancel (<c>CMsgCreatePlayer1001__Build</c> <c>0x7200BF</c>), 124 bytes.
/// </summary>
internal static class GameCharacterCreation1001
{
    internal const int PacketLength = 124;
    internal const ushort PacketType = 1001;

    /// <summary>The native builder refuses a name of 16 bytes or more.</summary>
    internal const int MaximumNameLength = 15;

    private const int CancelOffset = 4;
    private const int NameOffset = 24;
    private const int BodyOffset = 72;
    private const int ProfessionOffset = 74;
    private const int SessionOffset = 76;

    /// <summary>
    /// Writes a create request. The 40-byte MAC text at +80 stays empty, which is what the native builder sends
    /// when its NetBIOS adapter lookup fails; no hardware identifier leaves this client.
    /// </summary>
    internal static void WriteCreate(Span<byte> destination, ReadOnlySpan<byte> name, ushort body, ushort profession,
        uint sessionToken)
    {
        ValidateName(name);
        Write(destination, cancel: false, name, body, profession, sessionToken);
    }

    /// <summary>The structural limit the native builder enforces; the create-role screen applies the name rules.</summary>
    internal static void ValidateName(ReadOnlySpan<byte> name)
    {
        if (name.IsEmpty || name.Length > MaximumNameLength || name.Contains((byte)0))
        {
            throw new ArgumentException("A character name must be 1 to 15 bytes without NUL.", nameof(name));
        }
    }

    /// <summary>The native cancel sends an empty name, zero body and profession, and the session token.</summary>
    internal static void WriteCancel(Span<byte> destination, uint sessionToken) =>
        Write(destination, cancel: true, ReadOnlySpan<byte>.Empty, 0, 0, sessionToken);

    private static void Write(Span<byte> destination, bool cancel, ReadOnlySpan<byte> name, ushort body,
        ushort profession, uint sessionToken)
    {
        if (destination.Length != PacketLength)
        {
            throw new ArgumentException("A 1001 request must be exactly 124 bytes.", nameof(destination));
        }

        if (sessionToken == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sessionToken),
                "A 1001 request requires a nonzero AccountServer session token.");
        }

        destination.Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(destination, PacketLength);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], PacketType);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[CancelOffset..], cancel ? 1u : 0u);
        name.CopyTo(destination[NameOffset..]);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[BodyOffset..], body);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[ProfessionOffset..], profession);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[SessionOffset..], sessionToken);
    }
}
