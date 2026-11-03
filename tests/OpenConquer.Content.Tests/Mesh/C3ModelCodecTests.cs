using System.Buffers.Binary;
using System.Numerics;
using OpenConquer.Content.Mesh;

namespace OpenConquer.Content.Tests.Mesh;

public sealed class C3ModelCodecTests
{
    [Fact]
    public void RoundTripPreservesEverySourceByteAndNativeSample()
    {
        byte[] geometry = Fixture();
        C3ModelData model = Model(geometry);
        byte[] compiled = C3ModelCodec.Write(model);
        C3ModelData reopened = C3ModelCodec.Read(compiled);
        Assert.Equal(compiled, C3ModelCodec.Write(reopened));
        Assert.Equal(geometry, reopened.Source("C3/BODY.C3").ToArray());
        C3ModelTrack original = Assert.Single(model.DecodePart(0, 0));
        C3ModelTrack restored = Assert.Single(reopened.DecodePart(0, 0));
        Assert.Equal(original.Mesh.Vertices, restored.Mesh.Vertices);
        Assert.Equal(original.Mesh.Indices, restored.Mesh.Indices);
        Assert.Equal(original.Mesh.AlphaKeys.ToArray(), restored.Mesh.AlphaKeys.ToArray());
        Assert.Equal(original.Mesh.TrailingData.ToArray(), restored.Mesh.TrailingData.ToArray());
        Assert.Equal(model.Definition.Parts, reopened.Definition.Parts);
        foreach (int frame in new[] { 0, 5, 10 })
        {
            Assert.True(C3MotionSampler.TrySampleBone(restored.Motion, 0, frame, out Matrix4x4 matrix));
            Assert.Equal(Matrix4x4.CreateTranslation(frame, frame * 2, frame * 3), matrix);
        }
        Assert.True(restored.IsAttachment);
    }

    [Fact]
    public void PlaybackEditsRetainOriginalMatricesAndOpaqueFields()
    {
        C3ModelData original = Model(Fixture());
        C3ModelData edited = original.WithPlayback(0, 42, false);
        Assert.Equal(66, original.Definition.Clips[0].FrameMilliseconds);
        Assert.Equal(42, edited.Definition.Clips[0].FrameMilliseconds);
        Assert.False(edited.Definition.Clips[0].Repeat);
        foreach (C3ModelSource file in original.Definition.Sources)
        {
            Assert.Equal(original.Source(file.Path).ToArray(), edited.Source(file.Path).ToArray());
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(16)]
    [InlineData(100)]
    public void RejectsCorruptHeadersMetadataAndPayloads(int offset)
    {
        byte[] bytes = C3ModelCodec.Write(Model(Fixture()));
        bytes[offset] ^= 0x40;
        Assert.Throws<InvalidDataException>(() => C3ModelCodec.Read(bytes));
    }

    [Fact]
    public void RejectsTruncationTrailingDataAndUnorderedKeys()
    {
        byte[] bytes = C3ModelCodec.Write(Model(Fixture()));
        Assert.Throws<InvalidDataException>(() => C3ModelCodec.Read(bytes.AsMemory(0, bytes.Length - 1)));
        Assert.Throws<InvalidDataException>(() => C3ModelCodec.Read(bytes.Concat(new byte[1]).ToArray()));
        Assert.Throws<InvalidDataException>(() => Model(Fixture(lastKey: 0)));
    }

    [Fact]
    public void RejectsUnsafePathsAndUnresolvedDependencies()
    {
        Assert.Throws<ArgumentException>(() => C3ModelData.Create(Guid.NewGuid(), [], [], [],
            new Dictionary<string, byte[]> { ["../escape.c3"] = Fixture() }));
        C3ResolvedPart part = new("body", "missing.c3", "", "", 0, 0, 0, 0);
        Assert.Throws<InvalidDataException>(() => C3ModelData.Create(Guid.NewGuid(), [part], [], [],
            new Dictionary<string, byte[]> { ["c3/body.c3"] = Fixture() }));
    }

    private static C3ModelData Model(byte[] bytes)
    {
        C3ResolvedPart part = new("body", "c3/body.c3", "", "ini/3dmotion.dbc", 1, 5, 6, 7, 8, 9, 0);
        return C3ModelData.Create(Guid.NewGuid(), [part], [new C3ModelClip("Idle", "c3/body.c3", 66, true)],
            ["Plane01"], new Dictionary<string, byte[]> { ["c3/body.c3"] = bytes });
    }

    private static byte[] Fixture(uint lastKey = 10)
    {
        using MemoryStream file = new();
        file.Write(new byte[16]);
        void Section(string tag, byte[] data)
        {
            file.Write(System.Text.Encoding.ASCII.GetBytes(tag));
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(length, data.Length);
            file.Write(length);
            file.Write(data);
        }
        Section("PHY4", C3PhyMeshReaderTests.Payload("PHY4"));
        using MemoryStream motion = new();
        using (BinaryWriter writer = new(motion, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(1u);
            writer.Write(11u);
            writer.Write("KKEY"u8);
            writer.Write(2u);
            foreach (uint frame in new[] { 0u, lastKey })
            {
                writer.Write(frame);
                Matrix4x4 matrix = Matrix4x4.CreateTranslation(frame, frame * 2, frame * 3);
                ReadOnlySpan<float> values = System.Runtime.InteropServices.MemoryMarshal.Cast<Matrix4x4, float>(new ReadOnlySpan<Matrix4x4>(in matrix));
                foreach (float value in values)
                {
                    writer.Write(value);
                }
            }
            writer.Write(0);
        }
        Section("MOTI", motion.ToArray());
        Section("UNKN", [11, 22, 33, 44, 55]);
        return file.ToArray();
    }
}
