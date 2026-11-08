using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.Networking;

public sealed class WireGoldenHeartbeat1009Tests
{
    [Fact]
    public void HeartbeatEncoder_MatchesCapturedClientFrame()
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
        Assert.Equal("20_notes/Network/evidence/cp32-world-entry-2026-11-08/capture.json#capture-1-record-40",
            source.GetProperty("sourceEvidence").GetString());
        Assert.Equal("peer after CAST5-CFB decrypt and client trailer validation",
            source.GetProperty("capturePoint").GetString());

        byte[] observed = Convert.FromHexString(source.GetProperty("frameHex").GetString()!);
        const string digest = "aa2d5b328c0f90b9b80eea69f1e648ee33653fc9092ff633d72e4c9e05d4c922";
        Assert.Equal(digest, source.GetProperty("frameSha256").GetString());
        Assert.Equal(digest, Convert.ToHexString(SHA256.HashData(observed)).ToLowerInvariant());
        Assert.Equal(GameItem1009.FixedLength, observed.Length);
        Assert.Equal(observed.Length, BinaryPrimitives.ReadUInt16LittleEndian(observed));
        Assert.Equal(GameItem1009.PacketType, BinaryPrimitives.ReadUInt16LittleEndian(observed.AsSpan(2)));
        Assert.Equal(GameLatencyTick1009.Subtype, BinaryPrimitives.ReadUInt16LittleEndian(observed.AsSpan(12)));

        uint heroId = BinaryPrimitives.ReadUInt32LittleEndian(observed.AsSpan(4));
        uint milliseconds = BinaryPrimitives.ReadUInt32LittleEndian(observed.AsSpan(16));
        Assert.Equal(1_000_000u, heroId);
        Assert.Equal(68_952_244u, milliseconds);

        byte[] written = new byte[observed.Length];
        GameLatencyTick1009.Write(written, heroId, milliseconds);
        Assert.Equal(observed, written);
    }
}
