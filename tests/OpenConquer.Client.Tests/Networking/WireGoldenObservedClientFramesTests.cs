using System.Buffers.Binary;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Networking.Game.World;
using static OpenConquer.Client.Tests.Networking.WireGoldenObservedClientFrames;

namespace OpenConquer.Client.Tests.Networking;

public sealed class WireGoldenObservedClientFramesTests
{

    [Fact]
    public void WalkSelectorFrames_PreservePackedDirectionAndExactFields()
    {
        Sample[] samples = Read("10005-walk.observed.json", GameWalk10005.PacketType, EntryEvidence);
        Assert.Equal(23, samples.Length);
        Assert.Equal(12, samples.Count(static sample => sample.Capture == 1 && sample.Subtype == 1));
        Assert.Equal(11, samples.Count(static sample => sample.Capture == 2 && sample.Subtype == 0));
        Assert.Equal((1, 44), (samples[0].Capture, samples[0].Sequence));
        Assert.Equal((2, 68), (samples[^1].Capture, samples[^1].Sequence));

        foreach (Sample sample in samples)
        {
            GameWalk10005 walk = GameWalk10005.Read(GameMessages.Load(sample.Frame));
            Assert.Equal(1_000_000u, walk.EntityId);
            Assert.Equal(1010u, walk.MapState);
            Assert.Equal((uint)sample.Subtype, walk.Selector);
            Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(sample.Frame.AsSpan(4)), walk.PackedDirection);
            Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(sample.Frame.AsSpan(16)), walk.MovementTick);
            Assert.Equal((byte)((byte)walk.PackedDirection % 8), walk.DirectionIndex);
            Assert.InRange(walk.PackedDirection, 0u, byte.MaxValue);
            Assert.InRange(walk.Selector, 0u, GameWalk10005.RunSelector);

            byte[] encoded = new byte[GameWalk10005.PacketLength];
            GameWalk10005.WriteOrdinary(encoded, walk.EntityId, (byte)walk.PackedDirection,
                walk.MovementTick, walk.MapState, (byte)walk.Selector);
            Assert.Equal(sample.Frame, encoded);
        }
    }

    [Fact]
    public void WorldEntryActionFrames_MatchTheExistingWriter()
    {
        Sample[] samples = Read("10010-world-entry.observed.json", GameWorldAction10010.PacketType, EntryEvidence);
        Assert.Equal(new ushort[] { 74, 114, 75, 76, 96, 77, 78, 97, 251, 132 },
            samples.Select(static sample => sample.Subtype));

        foreach (Sample sample in samples)
        {
            Assert.True(GameWorldAction10010.TryRead(sample.Frame, out GameWorldAction10010 action));
            Assert.Equal(sample.Subtype, action.Action);
            Assert.Equal(0u, action.ActionParameter);
            Assert.Equal((ushort)0, action.Direction);
            Assert.Equal((ushort)0, action.X);
            Assert.Equal((ushort)0, action.Y);
            Assert.Equal(0u, action.Data1);
            Assert.Equal(0u, action.Data2);
            Assert.Equal((byte)0, action.Flag);
            Assert.Equal(sample.Subtype == 96 ? 3u : 0u, action.ParameterPair);
            Assert.Equal(sample.Subtype == 251 ? 0u : 1_000_000u, action.EntityId);
            Assert.Equal(sample.Subtype == 251 ? 1u : BinaryPrimitives.ReadUInt32LittleEndian(sample.Frame.AsSpan(16)),
                action.Timestamp);

            byte[] encoded = new byte[GameWorldAction10010.PacketLength];
            GameWorldAction10010.Write(encoded, action.Action, action.EntityId, action.ParameterPair, action.Timestamp);
            Assert.Equal(sample.Frame, encoded);
        }
    }

    [Fact]
    public void ReplayedWorldEntryActions_MatchCapturedFramesAndResponseOrder()
    {
        Sample[] samples = Read("10010-world-replay.observed.json",
            GameWorldAction10010.PacketType, EntryEvidence);
        int[] records = [112, 114, 115, 117, 119, 121, 123, 133, 135, 136, 138, 140, 142, 144];
        ushort[] actions =
        [
            GameWorldAction10010.GetFriends, GameWorldAction10010.SetPkMode,
            GameWorldAction10010.GetWeaponSkills, GameWorldAction10010.GetMagic,
            GameWorldAction10010.GetSyndicate, GameWorldAction10010.ReportSilentInfo,
            GameWorldAction10010.GetStatistics
        ];
        ushort[] acknowledgements =
        [
            GameWorldAction10010.GetItems, GameWorldAction10010.GetFriends,
            GameWorldAction10010.GetWeaponSkills, GameWorldAction10010.GetMagic,
            GameWorldAction10010.GetSyndicate, GameWorldAction10010.ReportSilentInfo
        ];
        Assert.Equal(records.Length, samples.Length);

        uint previousTick = 0;
        for (int index = 0; index < samples.Length; index++)
        {
            Sample sample = samples[index];
            ushort expectedAction = actions[index % actions.Length];
            Assert.Equal((1, records[index]), (sample.Capture, sample.Sequence));
            Assert.Equal(expectedAction, sample.Subtype);
            Assert.True(GameWorldAction10010.TryRead(sample.Frame, out GameWorldAction10010 action));
            Assert.Equal(expectedAction, action.Action);
            Assert.Equal(expectedAction == GameWorldAction10010.ReportSilentInfo ? 0u : 1_000_000u,
                action.EntityId);
            Assert.Equal(expectedAction == GameWorldAction10010.SetPkMode ? 3u : 0u, action.ParameterPair);
            Assert.Equal(0u, action.ActionParameter);
            Assert.Equal((ushort)0, action.Direction);
            Assert.Equal((ushort)0, action.X);
            Assert.Equal((ushort)0, action.Y);
            Assert.Equal(0u, action.Data1);
            Assert.Equal(0u, action.Data2);
            Assert.Equal((byte)0, action.Flag);
            Assert.Equal((byte)0, sample.Frame[37]);

            if (expectedAction == GameWorldAction10010.ReportSilentInfo)
            {
                Assert.Equal(1u, action.Timestamp);
            }
            else
            {
                Assert.True(action.Timestamp > previousTick);
                previousTick = action.Timestamp;
            }

            byte[] encoded = new byte[GameWorldAction10010.PacketLength];
            GameWorldAction10010.Write(encoded, action.Action, action.EntityId,
                action.ParameterPair, action.Timestamp);
            Assert.Equal(sample.Frame, encoded);
        }

        for (int window = 0; window < 2; window++)
        {
            List<GameWorldActionResponse> responses = [];
            foreach (ushort acknowledgement in acknowledgements)
            {
                Assert.True(GameWorldActionResponses.TryForAcknowledgement(acknowledgement, 1_000_000,
                    GameSilentInfoReport.WithoutFiles, out GameWorldActionResponse first,
                    out GameWorldActionResponse second));
                responses.Add(first);
                if (second.Action != 0)
                {
                    responses.Add(second);
                }
            }

            Assert.Equal(actions, responses.Select(static reply => reply.Action));
            Assert.Equal(1u, responses[5].TimestampOverride);
            for (int index = 0; index < actions.Length; index++)
            {
                Assert.Equal(samples[window * actions.Length + index].Subtype, responses[index].Action);
            }
        }
    }

    [Fact]
    public void CorrectionReplyFrames_MatchTheSecuredReplyWriter()
    {
        Sample[] samples = Read("10010-correction.observed.json", GameWorldAction10010.PacketType, CorrectionEvidence);
        Assert.Equal(3, samples.Length);
        Assert.Equal((1, 43), (samples[0].Capture, samples[0].Sequence));
        Assert.Equal((1, 47), (samples[1].Capture, samples[1].Sequence));
        Assert.Equal((1, 51), (samples[2].Capture, samples[2].Sequence));
        Assert.Equal(new ushort[] { 146, 146, 146 }, samples.Select(static sample => sample.Subtype));

        uint lastTick = 0;
        ushort[] expectedX = [61, 62, 62];
        for (int index = 0; index < samples.Length; index++)
        {
            Sample sample = samples[index];
            Assert.True(GameWorldAction10010.TryRead(sample.Frame, out GameWorldAction10010 action));
            Assert.Equal(1_000_000u, action.EntityId);
            Assert.Equal(expectedX[index], unchecked((ushort)action.ParameterPair));
            Assert.Equal((ushort)109, unchecked((ushort)(action.ParameterPair >> 16)));
            Assert.True(action.Timestamp > lastTick);
            Assert.Equal((ushort)0, action.Direction);
            Assert.Equal((ushort)0, action.X);
            Assert.Equal((ushort)0, action.Y);
            Assert.Equal(1010u, action.Data1);
            Assert.Equal(uint.MaxValue, action.Data2);
            Assert.Equal((byte)0, action.Flag);
            lastTick = action.Timestamp;

            byte[] encoded = new byte[GameWorldAction10010.PacketLength];
            GameWorldAction10010.WriteCorrectionReply(encoded, action.EntityId,
                unchecked((ushort)action.ParameterPair),
                unchecked((ushort)(action.ParameterPair >> 16)),
                checked((byte)action.Direction), action.Data1, action.Timestamp);
            Assert.Equal(sample.Frame, encoded);
        }
    }

    [Fact]
    public void FlowerRankEntryRequests_MatchCapturedFrames()
    {
        Sample[] samples = Read("1151-flower-rank.observed.json", GameFlowerRankRequest1151.PacketType, EntryEvidence);
        Assert.Equal(2, samples.Length);
        Assert.Equal((1, 21), (samples[0].Capture, samples[0].Sequence));
        Assert.Equal((2, 20), (samples[1].Capture, samples[1].Sequence));

        foreach (Sample sample in samples)
        {
            Assert.Equal((ushort)GameFlowerRankRequest1151.EnterMapSubtype, sample.Subtype);
            byte[] encoded = new byte[GameFlowerRankRequest1151.PacketLength];
            GameFlowerRankRequest1151.Write(encoded, GameFlowerRankRequest1151.EnterMapSubtype);
            Assert.Equal(sample.Frame, encoded);
        }
    }

    [Fact]
    public void GhostTransitionAction_ParsesCapturedClientRequest()
    {
        Sample sample = Assert.Single(Read("10010-ghost-transition.observed.json",
            GameWorldAction10010.PacketType, EntryEvidence));
        Assert.Equal((2, 45), (sample.Capture, sample.Sequence));
        Assert.True(GameWorldAction10010.TryRead(sample.Frame, out GameWorldAction10010 action));
        Assert.Equal((ushort)145, action.Action);
        Assert.Equal(1_000_000u, action.EntityId);
        Assert.Equal(0u, action.ParameterPair);
        Assert.Equal(0u, action.ActionParameter);
        Assert.Equal(0u, action.Data1);
        Assert.Equal(0u, action.Data2);
        Assert.Equal((ushort)0, action.Direction);
        Assert.Equal((byte)0, action.Flag);
        byte[] encoded = new byte[GameWorldAction10010.PacketLength];
        GameWorldAction10010.Write(encoded, action.Action, action.EntityId, action.ParameterPair, action.Timestamp);
        Assert.Equal(sample.Frame, encoded);
    }

    [Fact]
    public void ClientStateAppliedReport_MatchesTheCapturedZeroFieldAction()
    {
        Sample sample = Assert.Single(Read("10010-client-state.observed.json",
            GameWorldAction10010.PacketType, ConditionalEvidence));
        Assert.Equal((1, 23), (sample.Capture, sample.Sequence));
        Assert.Equal(GameWorldAction10010.ClientStateApplied, sample.Subtype);
        Assert.True(GameWorldAction10010.TryRead(sample.Frame, out GameWorldAction10010 action));
        Assert.Equal(0u, action.EntityId);
        Assert.Equal(0u, action.ParameterPair);
        Assert.Equal(0u, action.ActionParameter);
        Assert.Equal(0u, action.Timestamp);
        Assert.Equal((ushort)0, action.Direction);
        Assert.Equal((ushort)0, action.X);
        Assert.Equal((ushort)0, action.Y);
        Assert.Equal(0u, action.Data1);
        Assert.Equal(0u, action.Data2);
        Assert.Equal((byte)0, action.Flag);
        Assert.Equal((byte)0, sample.Frame[37]);

        byte[] encoded = new byte[GameWorldAction10010.PacketLength];
        GameWorldAction10010.Write(encoded, GameWorldAction10010.ClientStateApplied, 0);
        Assert.Equal(sample.Frame, encoded);
    }

    [Fact]
    public void GroundJumpRequests_UseTheObservedTargetAndOriginFieldLayout()
    {
        Sample[] samples = Read("10010-ground-jump.observed.json",
            GameWorldAction10010.PacketType, ConditionalEvidence);
        int[] sequences = [46, 47, 48, 51, 52, 53, 54, 55, 56, 57, 58, 59];
        Assert.Equal(sequences, samples.Select(static sample => sample.Sequence));
        (ushort TargetX, ushort TargetY, ushort FromX, ushort FromY)[] coordinates =
        [
            (62, 110, 63, 106),
            (59, 109, 62, 110),
            (64, 109, 59, 109),
            (60, 111, 64, 109),
            (62, 106, 60, 111),
            (60, 112, 62, 106),
            (66, 110, 60, 112),
            (56, 109, 66, 110),
            (50, 104, 56, 109),
            (49, 95, 50, 104),
            (58, 104, 49, 95),
            (63, 106, 58, 104)
        ];
        for (int index = 0; index < samples.Length; index++)
        {
            Sample sample = samples[index];
            Assert.Equal(1, sample.Capture);
            Assert.Equal((ushort)137, sample.Subtype);
            Assert.True(GameWorldAction10010.TryRead(sample.Frame, out GameWorldAction10010 action));
            Assert.Equal(1_000_000u, action.EntityId);
            Assert.Equal(0u, action.ActionParameter);
            Assert.Equal((ushort)0, action.Direction);
            Assert.Equal(1010u, action.Data1);
            Assert.Equal(uint.MaxValue, action.Data2);
            Assert.Equal((byte)0, action.Flag);
            Assert.Equal((byte)0, sample.Frame[37]);
            Assert.Equal((coordinates[index].TargetX, coordinates[index].TargetY),
                (unchecked((ushort)action.ParameterPair), unchecked((ushort)(action.ParameterPair >> 16))));
            Assert.Equal((coordinates[index].FromX, coordinates[index].FromY), (action.X, action.Y));
        }
    }

    [Fact]
    public void GroundJumpWriterMatchesAllCapturedClientRequests()
    {
        Sample[] samples = Read("10010-ground-jump.observed.json",
            GameWorldAction10010.PacketType, ConditionalEvidence);
        Assert.Equal(12, samples.Length);

        foreach (Sample sample in samples)
        {
            Assert.True(GameWorldAction10010.TryRead(sample.Frame, out GameWorldAction10010 action));
            Assert.Equal(GameWorldAction10010.GroundJump, action.Action);
            Assert.Equal(0u, action.ActionParameter);
            Assert.Equal((byte)0, action.Flag);

            byte[] encoded = new byte[GameWorldAction10010.PacketLength];
            GameWorldAction10010.WriteGroundJump(encoded, action.EntityId,
                (ushort)action.ParameterPair, (ushort)(action.ParameterPair >> 16),
                action.X, action.Y, action.Data1, action.Timestamp);
            Assert.Equal(sample.Frame, encoded);
        }
    }

    [Fact]
    public void TradeQuitRequest_RetainsTheObservedFrameWithoutInventingClientEmission()
    {
        Sample sample = Assert.Single(Read("1056-trade-quit.observed.json",
            TradeQuitPacketId, ConditionalEvidence));
        Assert.Equal((1, 68), (sample.Capture, sample.Sequence));
        Assert.Equal((ushort)2, sample.Subtype);
        Assert.Equal(12, sample.Frame.Length);
        Assert.Equal(1_000_000u, BinaryPrimitives.ReadUInt32LittleEndian(sample.Frame.AsSpan(4)));
        Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(sample.Frame.AsSpan(8)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(sample.Frame.AsSpan(10)));
    }

}
