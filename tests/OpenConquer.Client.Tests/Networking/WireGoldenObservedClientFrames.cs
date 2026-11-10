using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.Networking;

internal static class WireGoldenObservedClientFrames
{
    internal const string EntryEvidence = "20_notes/Network/evidence/cp32-world-entry-2026-11-08/capture.json";
    internal const string CorrectionEvidence = "20_notes/Network/evidence/cp32-correction-2026-11-08/capture.json";
    internal const string ConditionalEvidence = "20_notes/Network/evidence/cp32-conditional-world-2026-11-08/capture.json";
    internal const ushort TradeQuitPacketId = 1056;

    internal static Sample[] Read(string fileName, ushort packetId, string evidence)
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
            uint decodedSubtype = packetId switch
            {
                GameWalk10005.PacketType => BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(12)),
                GameFlowerRankRequest1151.PacketType => frame[4],
                TradeQuitPacketId => BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(8)),
                _ => BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(20))
            };
            Assert.Equal((uint)subtype, decodedSubtype);
            return new Sample(row.GetProperty("capture").GetInt32(),
                row.GetProperty("sequence").GetInt32(), subtype, frame);
        }).ToArray();
    }

    internal readonly record struct Sample(int Capture, int Sequence, ushort Subtype, byte[] Frame);
}
