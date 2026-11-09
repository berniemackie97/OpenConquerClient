using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.Networking;

public sealed class WireGoldenObservedClientFramesTests
{
    private const string EntryEvidence = "20_notes/Network/evidence/cp32-world-entry-2026-11-08/capture.json";
    private const string CorrectionEvidence = "20_notes/Network/evidence/cp32-correction-2026-11-08/capture.json";

    [Fact]
    public void WalkSelectorFrames_PreservePackedDirectionAndExactFields()
    {
        Sample[] samples = Read("10005-walk.observed.json", GameWalk10005.PacketType, EntryEvidence);
        Assert.Equal(new ushort[] { 1, 0 }, samples.Select(static sample => sample.Subtype));
        Assert.Equal(new[] { 44, 46 }, samples.Select(static sample => sample.Sequence));

        foreach (Sample sample in samples)
        {
            GameWalk10005 walk = GameWalk10005.Read(GameMessages.Load(sample.Frame));
            Assert.Equal(1_000_000u, walk.EntityId);
            Assert.Equal(1010u, walk.MapState);
            Assert.Equal((uint)sample.Subtype, walk.Selector);
            Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(sample.Frame.AsSpan(4)), walk.PackedDirection);
            Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(sample.Frame.AsSpan(16)), walk.MovementTick);
            Assert.Equal((byte)6, walk.DirectionIndex);
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
    public void CorrectionReplyFrames_MatchTheSecuredReplyWriter()
    {
        Sample[] samples = Read("10010-correction.observed.json", GameWorldAction10010.PacketType, CorrectionEvidence);
        Assert.Equal(new[] { 43, 47, 51 }, samples.Select(static sample => sample.Sequence));
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

    private static Sample[] Read(string fileName, ushort packetId, string evidence)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "WireGolden", fileName)));
        JsonElement root = document.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("observed", root.GetProperty("classification").GetString());
        Assert.True(root.GetProperty("runtimeCaptured").GetBoolean());
        Assert.Equal("game", root.GetProperty("protocol").GetString());
        Assert.Equal("client-to-server", root.GetProperty("direction").GetString());
        Assert.Equal(packetId, root.GetProperty("packetId").GetUInt16());
        Assert.Equal("b6ba5ab8573ec073ea271f57b16fa14de1c67bfb44efd5aa7cfbfc0d3b488a62",
            root.GetProperty("referenceImageSha256").GetString());
        Assert.Equal(evidence, root.GetProperty("sourceEvidence").GetString());
        Assert.Equal("peer after game decrypt and client trailer validation", root.GetProperty("capturePoint").GetString());

        return root.GetProperty("samples").EnumerateArray().Select(row =>
        {
            byte[] frame = Convert.FromHexString(row.GetProperty("frameHex").GetString()!);
            Assert.Equal(row.GetProperty("frameSha256").GetString(),
                Convert.ToHexString(SHA256.HashData(frame)).ToLowerInvariant());
            Assert.Equal(frame.Length, BinaryPrimitives.ReadUInt16LittleEndian(frame));
            Assert.Equal(packetId, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)));
            ushort subtype = row.GetProperty("subtype").GetUInt16();
            uint decodedSubtype = packetId == GameWalk10005.PacketType
                ? BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(12))
                : BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(20));
            Assert.Equal((uint)subtype, decodedSubtype);
            return new Sample(row.GetProperty("capture").GetInt32(),
                row.GetProperty("sequence").GetInt32(), subtype, frame);
        }).ToArray();
    }

    private readonly record struct Sample(int Capture, int Sequence, ushort Subtype, byte[] Frame);
}
