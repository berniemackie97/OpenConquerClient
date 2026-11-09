using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using OpenConquer.Client.World;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.World;

public sealed partial class WorldInboundStateTests
{
    [Fact]
    public void CapturedManagedPeerFramesApplySpawnWalkRunAndRemovalInOrder()
    {
        byte[][] frames = LoadManagedPeerFrames();
        WorldInboundState state = CreateState(heroId: 1_000_000);
        Assert.True(state.Apply(Correction(1_000_000, 60, 108)));

        Assert.False(state.Apply(frames[0]));
        Assert.True(state.RemoteRoles.TryGet(1_000_001, out GamePlayerSpawn10014? spawned));
        Assert.Equal(((ushort)60, (ushort)108), (spawned!.X, spawned.Y));
        Assert.Equal(0, state.RemoteRoles.GetPlayback(1_000_001)!.Action);

        Assert.False(state.Apply(frames[1]));
        Assert.True(state.RemoteRoles.TryGet(1_000_001, out GamePlayerSpawn10014? walked));
        Assert.Equal(((ushort)61, (ushort)108, (byte)6),
            (walked!.X, walked.Y, walked.Direction));
        Assert.Equal(WorldRolePlayback.GroundWalkFirst, state.RemoteRoles.GetPlayback(1_000_001)!.Action);

        Assert.False(state.Apply(frames[2]));
        Assert.True(state.RemoteRoles.TryGet(1_000_001, out GamePlayerSpawn10014? ran));
        Assert.Equal(((ushort)60, (ushort)108, (byte)2), (ran!.X, ran.Y, ran.Direction));
        Assert.Equal(WorldRolePlayback.GroundRunFirst, state.RemoteRoles.GetPlayback(1_000_001)!.Action);

        Assert.False(state.Apply(frames[3]));
        Assert.False(state.RemoteRoles.Contains(1_000_001));
        Assert.Null(state.RemoteRoles.GetPlayback(1_000_001));
        Assert.Equal(0, state.RemoteRoles.Count);
        Assert.Equal((60, 108, (byte)3), (state.X, state.Y, state.DirectionIndex));
    }

    private static byte[][] LoadManagedPeerFrames()
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

            frames[i] = frame;
        }

        return frames;
    }
