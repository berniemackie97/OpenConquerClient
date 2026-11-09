using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace OpenConquer.Client.Tests.World;

internal static class ManagedPeerFrames
{
    internal static byte[][] Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "ManagedWorld", "peer-movement.observed.json");
        byte[] source = File.ReadAllBytes(path);
        Assert.Equal("ce20afe2bcbbb0e7219567754d856e23e4cd3e68c2fd954481db20054889697b",
            Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant());

        using JsonDocument document = JsonDocument.Parse(source);
        JsonElement records = document.RootElement;
        Assert.Equal(4, records.GetArrayLength());
        byte[][] frames = new byte[records.GetArrayLength()][];
        for (int i = 0; i < frames.Length; i++)
        {
            JsonElement record = records[i];
            Assert.Equal(i + 1, record.GetProperty("sequence").GetInt32());
            Assert.Equal("managed-ocs", record.GetProperty("provenance").GetString());
            Assert.Equal("server-to-client", record.GetProperty("direction").GetString());
            Assert.Equal("OCC after CAST5-CFB decryption and TQServer validation",
                record.GetProperty("capturePoint").GetString());

            byte[] frame = Convert.FromHexString(record.GetProperty("hex").GetString()!);
            Assert.Equal(record.GetProperty("length").GetInt32(), frame.Length);
            Assert.Equal(frame.Length, BinaryPrimitives.ReadUInt16LittleEndian(frame));
            Assert.Equal(record.GetProperty("packetId").GetUInt16(),
                BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)));
            Assert.Equal(record.GetProperty("sha256").GetString(),
                Convert.ToHexString(SHA256.HashData(frame)).ToLowerInvariant());
            if (record.TryGetProperty("selector", out JsonElement selector))
            {
                Assert.Equal(selector.GetUInt32(), BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(12)));
            }

            if (record.TryGetProperty("action", out JsonElement action))
            {
                Assert.Equal(action.GetUInt32(), BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(20)));
            }

            frames[i] = frame;
        }

        return frames;
    }
}
