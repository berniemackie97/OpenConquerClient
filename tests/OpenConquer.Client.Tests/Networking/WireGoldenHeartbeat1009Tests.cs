using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.Networking;

public sealed class WireGoldenHeartbeat1009Tests
{
    [Fact]
    public void HeartbeatEncoder_MatchesBothCapturedSequences()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "WireGolden", "1009-27-heartbeat.observed.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement source = document.RootElement;

        Assert.Equal(1, source.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("observed", source.GetProperty("classification").GetString());
        Assert.True(source.GetProperty("runtimeCaptured").GetBoolean());
        Assert.Equal("game", source.GetProperty("protocol").GetString());
        Assert.Equal("client-to-server", source.GetProperty("direction").GetString());
        Assert.Equal((ushort)1009, source.GetProperty("packetId").GetUInt16());
        Assert.Equal((ushort)27, source.GetProperty("subtype").GetUInt16());
        Assert.Equal("b6ba5ab8573ec073ea271f57b16fa14de1c67bfb44efd5aa7cfbfc0d3b488a62",
            source.GetProperty("referenceImageSha256").GetString());
        Assert.Equal("20_notes/Network/evidence/cp32-world-entry-2026-11-08/capture.json",
            source.GetProperty("sourceEvidence").GetString());
        Assert.Equal("peer after game decrypt and client trailer validation",
            source.GetProperty("capturePoint").GetString());

        JsonElement samples = source.GetProperty("samples");
        Assert.Equal(30, samples.GetArrayLength());

        int[] counts = new int[2];
        int[] previousSequences = new int[2];
        uint[] previousTicks = new uint[2];
        foreach (JsonElement sample in samples.EnumerateArray())
        {
            int capture = sample.GetProperty("capture").GetInt32();
            Assert.InRange(capture, 1, 2);
            int index = capture - 1;
            int sequence = sample.GetProperty("sequence").GetInt32();
            Assert.True(sequence > previousSequences[index]);
            previousSequences[index] = sequence;
            counts[index]++;

            byte[] observed = Convert.FromHexString(sample.GetProperty("frameHex").GetString()!);
            Assert.Equal(sample.GetProperty("frameSha256").GetString(),
                Convert.ToHexString(SHA256.HashData(observed)).ToLowerInvariant());
            Assert.Equal(GameItem1009.FixedLength, observed.Length);
            Assert.Equal(observed.Length, BinaryPrimitives.ReadUInt16LittleEndian(observed));
            Assert.Equal(GameItem1009.PacketType, BinaryPrimitives.ReadUInt16LittleEndian(observed.AsSpan(2)));
            Assert.Equal(GameLatencyTick1009.Subtype, BinaryPrimitives.ReadUInt16LittleEndian(observed.AsSpan(12)));

            uint heroId = BinaryPrimitives.ReadUInt32LittleEndian(observed.AsSpan(4));
            uint milliseconds = BinaryPrimitives.ReadUInt32LittleEndian(observed.AsSpan(16));
            Assert.Equal(1_000_000u, heroId);
            Assert.True(milliseconds > previousTicks[index]);
            previousTicks[index] = milliseconds;

            byte[] written = new byte[observed.Length];
            GameLatencyTick1009.Write(written, heroId, milliseconds);
            Assert.Equal(observed, written);
        }

        Assert.Equal((24, 6), (counts[0], counts[1]));
        Assert.Equal((155, 72), (previousSequences[0], previousSequences[1]));
    }
}
