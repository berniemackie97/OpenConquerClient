using OpenConquer.Networking.Game.Framing;
using OpenConquer.Networking.Game.Handshake;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Networking.Game.Login;

/// <summary>
/// Drives the existing-character world-entry request ladder on the authenticated socket the way the 5517 client does:
/// each server acknowledgement triggers the next request, and nothing else gates order. Frames that are not the ladder,
/// map, weather or a hero acknowledgement are kept, in arrival order and within a bounded budget, for the world-state
/// consumer. Evidence: 20_notes Network/bootstrap-tolerance-cp24d.md.
/// </summary>
internal static class GameWorldEntryBootstrap
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_maximumDuration = TimeSpan.FromMinutes(2);
    private const int MaximumHydrationFrames = 4096;
    private const int MaximumHydrationBytes = 4 * 1024 * 1024;
    private const ulong ClientStateAppliedMapFlag = 1UL << 38;

    // Main-dialog init sends PK mode 3 (sub_66066a(hero, 3, 1), 0x4BDD30).
    private const uint InitialPkMode = 3;

    internal static async Task<GameWorldEntrySnapshot> RunAsync(GameServerHandshakeSession session,
        GameCharacterInfo1006 character, Action? onMapEntered = null, TimeSpan? completionTimeout = null,
        TimeProvider? clock = null, CancellationToken cancellationToken = default)
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

        cancellationToken.ThrowIfCancellationRequested();
        clock ??= TimeProvider.System;
        using CancellationTokenSource deadline = new(duration, clock);
        using CancellationTokenSource lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        CancellationToken operation = lifetime.Token;
        uint hero = character.EntityId;
        try
        {
            List<byte[]> hydration = [];
            int byteCount = 0;
            GameMapInfo1110? map = null;
            ulong mapFlags = 0;
            uint weatherKind = 0;
            GameWorldAction10010? entered = null;
            int enteredAt = 0;
            uint heartbeatStartedAt = 0;
            bool statisticsRequested = false;

            // CMsgUserInfo__Process sends 74 (0x764873), then 1151 subtype 2 for body digit 2 (0x7648DB).
            await SendActionAsync(GameWorldAction10010.EnterMap, hero).ConfigureAwait(false);
            if (GameFlowerRankRequest1151.IsSentAfterEnterMap(character.PackedAppearance))
            {
                await session.SendWorldEntryFlowerRankAsync(s_timeout, operation).ConfigureAwait(false);
            }

            while (!statisticsRequested || entered is null || map is null)
            {
                using GameSecuredInboundFrame frame = await ReadRequiredAsync(session, operation).ConfigureAwait(false);
                ReadOnlySpan<byte> packet = frame.Packet.Span;
                if (GameMapInfo1110.TryRead(packet, out GameMapInfo1110 info))
                {
                    // CMsgMapInfo1110__Process sends 408 when map-flag bit38 changes (0x732CFD/0x732D4C); the native
                    // flags start at zero (CGameMap ctor 0x61B875).
                    bool changed = ((mapFlags ^ info.MapFlags) & ClientStateAppliedMapFlag) != 0;
                    (map, mapFlags) = (info, info.MapFlags);
                    if (changed)
                    {
                        await session.SendWorldActionAsync(GameWorldAction10010.ClientStateApplied, 0, 0, s_timeout,
                            operation).ConfigureAwait(false);
                    }

                    continue;
                }

                if (GameWeather1016.TryRead(packet, out GameWeather1016 weather))
                {
                    // CMsgWeatherUpdate__ValidateAndNormalize 0x7606CD: an unsupported value is a silent no-op.
                    weatherKind = weather.HasSupportedValues ? weather.Kind : weatherKind;
                    continue;
                }

                if (GameWorldAction10010.TryRead(packet, out GameWorldAction10010 action) &&
                    await TryAnswerAsync(action).ConfigureAwait(false))
                {
                    continue;
                }

                byteCount = checked(byteCount + frame.PacketLength);
                if (hydration.Count >= MaximumHydrationFrames || byteCount > MaximumHydrationBytes)
                {
                    throw new InvalidDataException("World entry exceeded its bounded hydration budget.");
                }

                hydration.Add(frame.Packet.ToArray());
            }

            operation.ThrowIfCancellationRequested();
            session.ActivateWorld();
            GameWorldAction10010 position = entered.Value;
            return new GameWorldEntrySnapshot(character, map.Value.MapId, position.ParameterPair, mapFlags, weatherKind,
                position.X, position.Y, hydration, (byte)(position.Direction % 8), enteredAt, heartbeatStartedAt,
                session.LocalAddress);

            // CMsgAction__Process reads only the entity and the action of an acknowledgement (common entity gate
            // 0x6FF15D..0x6FF18A; parameters, coordinates and timestamp are ignored) and answers every accepted one.
            // Only the hero resolves before world entry. Returns false for an action this ladder does not consume.
            async Task<bool> TryAnswerAsync(GameWorldAction10010 action)
            {
                switch (action.Action)
                {
                    case GameWorldAction10010.EnterMap:
                        // Entity overwritten with the hero (0x6FEE14); the map comes from +8 (0x6FF478). Map creation
                        // closes the booth (114, 0x61DB1E -> 0x70AC3D), then 75 is sent (0x6FF540).
                        bool first = entered is null;
                        entered = action;
                        enteredAt = hydration.Count;
                        if (first)
                        {
                            operation.ThrowIfCancellationRequested();
                            onMapEntered?.Invoke();
                        }

                        await SendActionAsync(GameWorldAction10010.BoothClose, hero).ConfigureAwait(false);
                        await SendActionAsync(GameWorldAction10010.GetItems, hero).ConfigureAwait(false);

                        // The heartbeat timer starts at 10000 ms after 75 is sent (0x6FF57A..0x6FF587).
                        heartbeatStartedAt = GameClientClock.Milliseconds(clock);
                        return true;
                    case GameWorldAction10010.GetItems or GameWorldAction10010.GetFriends or
                        GameWorldAction10010.GetWeaponSkills or GameWorldAction10010.GetMagic or
                        GameWorldAction10010.GetSyndicate or GameWorldAction10010.ReportSilentInfo:
                        if (action.EntityId != hero)
                        {
                            return true;
                        }

                        if (GameWorldActionResponses.TryForAcknowledgement(action.Action, hero,
                            session.SilentInfo, out GameWorldActionResponse primaryResponse,
                            out GameWorldActionResponse additionalResponse))
                        {
                            await SendResponseAsync(primaryResponse).ConfigureAwait(false);
                            if (additionalResponse.Action != 0)
                            {
                                await SendResponseAsync(additionalResponse).ConfigureAwait(false);
                            }

                            if (action.Action == GameWorldAction10010.ReportSilentInfo)
                            {
                                statisticsRequested = true;
                            }
                        }

                        return true;
                    default:
                        return false;
                }
            }

            // CMsgAction__Create (0x6FE95F) stamps every request with timeGetTime at +16 (0x6FE99C).
            Task SendActionAsync(ushort action, uint entityId, uint parameter = 0) =>
                session.SendWorldActionAsync(action, entityId, parameter, GameClientClock.Milliseconds(clock), s_timeout, operation);

            Task SendResponseAsync(GameWorldActionResponse reply) =>
                session.SendWorldActionAsync(reply.Action, reply.EntityId, reply.ParameterPair,
                    reply.TimestampOverride ?? GameClientClock.Milliseconds(clock), s_timeout, operation);
        }
        catch (OperationCanceledException exception) when (operation.IsCancellationRequested)
        {
            // Cancellation can land between socket operations. A partial request ladder cannot be resumed.
            await session.DisposeAsync().ConfigureAwait(false);
            if (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("GameServer world-entry bootstrap exceeded its overall deadline.", exception);
            }

            throw;
        }
    }

    private static async Task<GameSecuredInboundFrame> ReadRequiredAsync(GameServerHandshakeSession session,
        CancellationToken cancellationToken) =>
        await session.ReadFrameAsync(s_timeout, cancellationToken).ConfigureAwait(false)
        ?? throw new EndOfStreamException("GameServer closed during world entry.");
}

/// <summary>
/// World-entry result. <c>HydrationEnteredAt</c> counts the hydration frames that arrived before the last EnterMap
/// reply. <c>HeartbeatStartedAt</c> is the client millisecond counter when the heartbeat timer started, and
/// <c>LocalAddress</c> the IPv4 address resolved at account connect (0 when unknown).
/// </summary>
internal sealed record GameWorldEntrySnapshot(GameCharacterInfo1006 Character, uint MapId, uint MapDataId,
    ulong MapFlags, uint WeatherKind, ushort X, ushort Y, IReadOnlyList<byte[]> HydrationPackets,
    byte DirectionIndex, int HydrationEnteredAt = 0, uint HeartbeatStartedAt = 0, uint LocalAddress = 0)
{
    public override string ToString() => nameof(GameWorldEntrySnapshot);
}
