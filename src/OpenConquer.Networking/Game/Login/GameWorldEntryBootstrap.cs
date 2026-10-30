using System.Buffers.Binary;
using OpenConquer.Networking.Game.Framing;
using OpenConquer.Networking.Game.Handshake;

namespace OpenConquer.Networking.Game.Login;

/// <summary>
/// Completes the existing-character world-entry request ladder on the authenticated socket.
/// Initial map and hydration frames are owned, bounded copies for the world-state consumer.
/// </summary>
internal static class GameWorldEntryBootstrap
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(10);
    private const int MaximumHydrationFrames = 4096;
    private const int MaximumHydrationBytes = 4 * 1024 * 1024;

    internal static async Task<GameWorldEntrySnapshot> RunAsync(GameServerHandshakeSession session,
        GameCharacterInfo1006 character, Action? onMapEntered = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(character);
        if (character.EntityId == 0)
        {
            throw new InvalidDataException("World entry requires an authenticated nonzero hero ID.");
        }

        List<byte[]> hydration = [];
        int byteCount = 0;
        await session.SendWorldActionAsync(GameWorldAction10010.EnterMap, character.EntityId, 0,
            s_timeout, cancellationToken).ConfigureAwait(false);

        uint mapId;
        uint mapDataId;
        ulong flags;
        uint weatherKind;
        ushort x;
        ushort y;

        using (GameSecuredInboundFrame map = await ReadRequiredAsync(session, cancellationToken).ConfigureAwait(false))
        {
            if (map.PacketType != 1110 || map.PacketLength != 20)
            {
                throw new InvalidDataException("World entry expected the 1110 map information packet.");
            }

            ReadOnlySpan<byte> bytes = map.Packet.Span;
            mapId = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
            mapDataId = BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]);
            flags = BinaryPrimitives.ReadUInt64LittleEndian(bytes[12..]);
            if (mapId == 0 || mapDataId == 0)
            {
                throw new InvalidDataException("World entry supplied an invalid map identity.");
            }
        }

        using (GameSecuredInboundFrame weather = await ReadRequiredAsync(session, cancellationToken).ConfigureAwait(false))
        {
            if (weather.PacketType != 1016 || weather.PacketLength != 20)
            {
                throw new InvalidDataException("World entry expected the 1016 weather packet.");
            }

            weatherKind = BinaryPrimitives.ReadUInt32LittleEndian(weather.Packet.Span[4..]);
            if (weatherKind is < 1 or > 11 or 6)
            {
                throw new InvalidDataException("World entry received an unsupported weather kind.");
            }
        }

        using (GameSecuredInboundFrame entered = await ReadRequiredAsync(session, cancellationToken).ConfigureAwait(false))
        {
            if (!GameWorldAction10010.TryRead(entered.Packet.Span, out ushort action, out uint entity,
                    out uint selectedMapDataId, out x, out y) ||
                action != GameWorldAction10010.EnterMap || entity != character.EntityId ||
                selectedMapDataId != mapDataId)
            {
                throw new InvalidDataException("World entry received a mismatched EnterMap acknowledgement.");
            }
        }

        onMapEntered?.Invoke();

        // The native client emits action 0x198 once the server EnterMap state has been applied.
        await session.SendWorldActionAsync(GameWorldAction10010.ClientStateApplied, 0, 0,
            s_timeout, cancellationToken).ConfigureAwait(false);

        await ExchangeAsync(GameWorldAction10010.GetItems, character.EntityId, [1008, 1009]).ConfigureAwait(false);
        await ExchangeAsync(GameWorldAction10010.GetFriends, character.EntityId, [1019]).ConfigureAwait(false);
        await ExchangeAsync(GameWorldAction10010.GetWeaponSkills, character.EntityId, [1025]).ConfigureAwait(false);
        await ExchangeAsync(GameWorldAction10010.GetMagic, character.EntityId, [1103, 1104]).ConfigureAwait(false);
        await ExchangeAsync(GameWorldAction10010.GetSyndicate, character.EntityId, [1106]).ConfigureAwait(false);
        await ExchangeAsync(GameWorldAction10010.ReportSilentInfo, 0, []).ConfigureAwait(false);

        // This last request has no acknowledgement. Server admission follows it.
        await session.SendWorldActionAsync(GameWorldAction10010.GetStatistics, character.EntityId, 0,
            s_timeout, cancellationToken).ConfigureAwait(false);
        return new GameWorldEntrySnapshot(character, mapId, mapDataId, flags, weatherKind, x, y, hydration);

        async Task ExchangeAsync(ushort expectedAction, uint entityId, ushort[] acceptedPayloads)
        {
            await session.SendWorldActionAsync(expectedAction, entityId, 0, s_timeout, cancellationToken)
                .ConfigureAwait(false);

            while (true)
            {
                using GameSecuredInboundFrame frame = await ReadRequiredAsync(session, cancellationToken).ConfigureAwait(false);
                if (frame.PacketType == GameWorldAction10010.PacketType)
                {
                    if (!GameWorldAction10010.TryRead(frame.Packet.Span, out ushort action, out uint acknowledged,
                            out uint parameter, out _, out _) ||
                        action != expectedAction || acknowledged != character.EntityId || parameter != 0)
                    {
                        throw new InvalidDataException($"World entry received a mismatched acknowledgement for action {expectedAction}.");
                    }

                    break;
                }

                if (!acceptedPayloads.AsSpan().Contains(frame.PacketType))
                {
                    throw new InvalidDataException($"World entry action {expectedAction} received an unexpected packet {frame.PacketType}.");
                }

                byteCount = checked(byteCount + frame.PacketLength);
                if (hydration.Count >= MaximumHydrationFrames || byteCount > MaximumHydrationBytes)
                {
                    throw new InvalidDataException("World entry exceeded its bounded hydration budget.");
                }

                hydration.Add(frame.Packet.ToArray());
            }
        }
    }

    private static async Task<GameSecuredInboundFrame> ReadRequiredAsync(GameServerHandshakeSession session,
        CancellationToken cancellationToken) =>
        await session.ReadFrameAsync(s_timeout, cancellationToken).ConfigureAwait(false)
        ?? throw new EndOfStreamException("GameServer closed during world entry.");
}

internal sealed record GameWorldEntrySnapshot(GameCharacterInfo1006 Character, uint MapId, uint MapDataId,
    ulong MapFlags, uint WeatherKind, ushort X, ushort Y, IReadOnlyList<byte[]> HydrationPackets);
