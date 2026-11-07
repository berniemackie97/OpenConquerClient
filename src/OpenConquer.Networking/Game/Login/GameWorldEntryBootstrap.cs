using OpenConquer.Networking.Game.Framing;
using OpenConquer.Networking.Game.Handshake;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Networking.Game.Login;

/// <summary>
/// Completes the existing-character world-entry request ladder on the authenticated socket.
/// Initial map and hydration frames are owned, bounded copies for the world-state consumer.
/// </summary>
internal static class GameWorldEntryBootstrap
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_maximumDuration = TimeSpan.FromMinutes(2);
    private const int MaximumHydrationFrames = 4096;
    private const int MaximumHydrationBytes = 4 * 1024 * 1024;
    private const ulong ClientStateAppliedMapFlag = 1UL << 38;

    internal static async Task<GameWorldEntrySnapshot> RunAsync(GameServerHandshakeSession session,
        GameCharacterInfo1006 character, Action? onMapEntered = null, TimeSpan? completionTimeout = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(character);
        if (character.EntityId == 0)
        {
            throw new InvalidDataException("World entry requires an authenticated nonzero hero ID.");
        }

        TimeSpan duration = completionTimeout ?? s_maximumDuration;
        if (duration <= TimeSpan.Zero || duration > s_maximumDuration)
        {
            throw new ArgumentOutOfRangeException(nameof(completionTimeout));
        }

        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(duration);
        CancellationToken operation = deadline.Token;
        try
        {
            List<byte[]> hydration = [];
            int byteCount = 0;
            await session.SendWorldActionAsync(GameWorldAction10010.EnterMap, character.EntityId, 0,
                s_timeout, operation).ConfigureAwait(false);
            if (GameFlowerRankRequest1151.IsSentAfterEnterMap(character.PackedAppearance))
            {
                await session.SendWorldEntryFlowerRankAsync(s_timeout, operation).ConfigureAwait(false);
            }

            uint mapId;
            uint mapDataId;
            ulong flags;
            uint weatherKind;
            ushort x;
            ushort y;
            byte directionIndex;

            using (GameSecuredInboundFrame map = await ReadRequiredAsync(session, operation).ConfigureAwait(false))
            {
                if (!GameMapInfo1110.TryRead(map.Packet.Span, out GameMapInfo1110 info))
                {
                    throw new InvalidDataException("World entry expected the 1110 map information packet.");
                }

                mapId = info.MapId;
                mapDataId = info.MapDataId;
                flags = info.MapFlags;
                if (mapId == 0 || mapDataId == 0)
                {
                    throw new InvalidDataException("World entry supplied an invalid map identity.");
                }
            }

            // CMsgMapInfo1110__Process sends action 408 only when map-flag bit38 changes (0x732CFD/0x732D4C). The
            // native map flags start at zero (CGameMap ctor 0x61B875), so first entry sends it only for a bit38 map.
            if ((flags & ClientStateAppliedMapFlag) != 0)
            {
                await session.SendWorldActionAsync(GameWorldAction10010.ClientStateApplied, 0, 0,
                    s_timeout, operation).ConfigureAwait(false);
            }

            using (GameSecuredInboundFrame weather = await ReadRequiredAsync(session, operation).ConfigureAwait(false))
            {
                if (!GameWeather1016.TryRead(weather.Packet.Span, out GameWeather1016 info))
                {
                    throw new InvalidDataException("World entry expected the 1016 weather packet.");
                }

                weatherKind = info.Kind;
                if (weatherKind is < 1 or > 11 or 6)
                {
                    throw new InvalidDataException("World entry received an unsupported weather kind.");
                }
            }

            using (GameSecuredInboundFrame entered = await ReadRequiredAsync(session, operation).ConfigureAwait(false))
            {
                if (!GameWorldAction10010.TryRead(entered.Packet.Span, out GameWorldAction10010 action) ||
                    action.Action != GameWorldAction10010.EnterMap || action.EntityId != character.EntityId ||
                    action.ParameterPair != mapDataId)
                {
                    throw new InvalidDataException("World entry received a mismatched EnterMap acknowledgement.");
                }

                (x, y) = (action.X, action.Y);
                directionIndex = (byte)(action.Direction % 8);
            }

            operation.ThrowIfCancellationRequested();
            onMapEntered?.Invoke();

            // Native map creation for the 74 reply closes the booth, which always sends action 114 for the hero before
            // the 75 request (CGameMap_CreateFromDataId 0x61DB1E -> CBoothManager_Close 0x70AC3D).
            await session.SendWorldActionAsync(GameWorldAction10010.BoothClose, character.EntityId, 0,
                s_timeout, operation).ConfigureAwait(false);

            await ExchangeAsync(GameWorldAction10010.GetItems, character.EntityId, [1008, 1009]).ConfigureAwait(false);
            await ExchangeAsync(GameWorldAction10010.GetFriends, character.EntityId, [1019]).ConfigureAwait(false);
            await ExchangeAsync(GameWorldAction10010.GetWeaponSkills, character.EntityId, [1025]).ConfigureAwait(false);
            await ExchangeAsync(GameWorldAction10010.GetMagic, character.EntityId, [1103, 1104]).ConfigureAwait(false);
            await ExchangeAsync(GameWorldAction10010.GetSyndicate, character.EntityId, [1106]).ConfigureAwait(false);
            await ExchangeAsync(GameWorldAction10010.ReportSilentInfo, 0, []).ConfigureAwait(false);

            // This last request has no acknowledgement. Server admission follows it.
            await session.SendWorldActionAsync(GameWorldAction10010.GetStatistics, character.EntityId, 0,
                s_timeout, operation).ConfigureAwait(false);
            operation.ThrowIfCancellationRequested();
            session.ActivateWorld();
            return new GameWorldEntrySnapshot(character, mapId, mapDataId, flags, weatherKind, x, y,
                hydration, directionIndex);

            async Task ExchangeAsync(ushort expectedAction, uint entityId, ushort[] acceptedPayloads)
            {
                await session.SendWorldActionAsync(expectedAction, entityId, 0, s_timeout, operation)
                    .ConfigureAwait(false);

                while (true)
                {
                    using GameSecuredInboundFrame frame = await ReadRequiredAsync(session, operation).ConfigureAwait(false);
                    if (frame.PacketType == GameWorldAction10010.PacketType)
                    {
                        if (!GameWorldAction10010.TryRead(frame.Packet.Span, out GameWorldAction10010 action) ||
                            action.Action != expectedAction || action.EntityId != character.EntityId ||
                            action.ParameterPair != 0)
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
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("GameServer world-entry bootstrap exceeded its overall deadline.");
        }
    }

    private static async Task<GameSecuredInboundFrame> ReadRequiredAsync(GameServerHandshakeSession session,
        CancellationToken cancellationToken) =>
        await session.ReadFrameAsync(s_timeout, cancellationToken).ConfigureAwait(false)
        ?? throw new EndOfStreamException("GameServer closed during world entry.");
}

internal sealed record GameWorldEntrySnapshot(GameCharacterInfo1006 Character, uint MapId, uint MapDataId,
    ulong MapFlags, uint WeatherKind, ushort X, ushort Y, IReadOnlyList<byte[]> HydrationPackets,
    byte DirectionIndex);
