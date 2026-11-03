using System.Buffers.Binary;
using System.Numerics;
using OpenConquer.Content.Mesh;

namespace OpenConquer.Content.Tests.Mesh;

public sealed class C3MotionReaderTests
{
    [Fact]
    public void ReadsIndexedMatricesAndKeyframeIndices()
    {
        using MemoryStream data = Header(1, 11, "KKEY");
        WriteUInt32(data, 2);
        WriteUInt32(data, 0);
        WriteMatrix(data, Matrix4x4.Identity);
        WriteUInt32(data, 10);
        WriteMatrix(data, Matrix4x4.CreateTranslation(10, 20, 30));
        WriteUInt32(data, 0);

        C3Motion motion = C3MotionReader.Read(Section(data.ToArray()));

        Assert.Equal(1u, motion.BoneCount);
        Assert.Equal(11u, motion.FrameCount);
        Assert.Equal(2, motion.KeyframeCount);
        Assert.Equal(0u, motion.GetKeyframeIndex(0));
        Assert.Equal(10u, motion.GetKeyframeIndex(1));
        Assert.Equal(new Vector3(10, 20, 30), motion.GetBoneMatrix(1, 0).Translation);
    }

    [Fact]
    public void ExpandsQuaternionAndTranslation()
    {
        using MemoryStream data = Header(1, 1, "ZKEY");
        WriteUInt32(data, 1);
        WriteUInt16(data, 7);
        float halfRoot = MathF.Sqrt(0.5f);
        foreach (float value in new float[] { 0, 0, halfRoot, halfRoot, 3, -4, 5 })
        {
            WriteSingle(data, value);
        }
        WriteUInt32(data, 0);

        C3Motion motion = C3MotionReader.Read(Section(data.ToArray()));
        Matrix4x4 matrix = motion.GetBoneMatrix(0, 0);

        Assert.Equal(7u, motion.GetKeyframeIndex(0));
        Assert.InRange(matrix.M11, -0.000001f, 0.000001f);
        Assert.InRange(matrix.M12, 0.999999f, 1.000001f);
        Assert.InRange(matrix.M21, -1.000001f, -0.999999f);
        Assert.Equal(new Vector3(3, -4, 5), matrix.Translation);
        Assert.Equal(1f, matrix.M44);
    }

    [Fact]
    public void ExpandsAffineKeysWithoutTransposing()
    {
        using MemoryStream data = Header(1, 1, "XKEY");
        WriteUInt32(data, 1);
        WriteUInt16(data, 12);
        foreach (float value in new float[] { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 })
        {
            WriteSingle(data, value);
        }
        WriteUInt32(data, 0);

        C3Motion motion = C3MotionReader.Read(Section(data.ToArray()));
        Matrix4x4 matrix = motion.GetBoneMatrix(0, 0);

        Assert.Equal(12u, motion.GetKeyframeIndex(0));
        Assert.Equal(2f, matrix.M11);
        Assert.Equal(7f, matrix.M23);
        Assert.Equal(10f, matrix.M33);
        Assert.Equal(new Vector3(11, 12, 13), matrix.Translation);
        Assert.Equal(0f, matrix.M34);
        Assert.Equal(1f, matrix.M44);
    }

    [Fact]
    public void ReadsImplicitFramesInBoneMajorOrder()
    {
        using MemoryStream data = new();
        WriteUInt32(data, 2);
        WriteUInt32(data, 101);
        Matrix4x4 firstBone = new(
            0.9975640774f, -0.0697564706f, 0, 0,
            0.0697564706f, 0.9975640774f, 0, 0,
            0, 0, 1, 0,
            0.0343960971f, 1.9419987202f, 0, 1);

        for (int i = 0; i < 101; i++)
        {
            WriteMatrix(data, firstBone);
        }

        for (int i = 0; i < 101; i++)
        {
            WriteMatrix(data, Matrix4x4.CreateTranslation(i, 0, 0));
        }

        WriteUInt32(data, 0);
        C3Motion motion = C3MotionReader.Read(Section(data.ToArray()));

        Assert.Equal(101, motion.KeyframeCount);
        Assert.Equal(100u, motion.GetKeyframeIndex(100));
        Assert.Equal(firstBone, motion.GetBoneMatrix(0, 0));
        Assert.Equal(firstBone, motion.GetBoneMatrix(100, 0));
        Assert.Equal(37f, motion.GetBoneMatrix(37, 1).M41);
        Assert.Equal(100f, motion.GetBoneMatrix(100, 1).M41);
    }

    [Fact]
    public void ConsumesBoundedExtraSection()
    {
        using MemoryStream data = Header(1, 2, "KKEY");
        WriteUInt32(data, 1);
        WriteUInt32(data, 0);
        WriteMatrix(data, Matrix4x4.Identity);
        WriteUInt32(data, 3);
        data.Write(new byte[24]);

        C3Motion motion = C3MotionReader.Read(Section(data.ToArray()));
        Assert.Equal(1, motion.KeyframeCount);
    }

    [Theory]
    [InlineData("KKEY")]
    [InlineData("ZKEY")]
    [InlineData("XKEY")]
    public void RejectsTruncatedKeyStreams(string encoding)
    {
        using MemoryStream data = Header(1, 10, encoding);
        WriteUInt32(data, 1);
        if (encoding == "KKEY")
        {
            WriteUInt32(data, 0);
        }
        else
        {
            WriteUInt16(data, 0);
        }

        Assert.Throws<InvalidDataException>(() => C3MotionReader.Read(data.ToArray()));
    }

    [Fact]
    public void RejectsWrongSectionAndInvalidContentBounds()
    {
        byte[] data = new byte[12];
        Assert.Throws<InvalidDataException>(() =>
            C3MotionReader.Read(new C3MeshSection(0, 0, data)));
        Assert.Throws<InvalidDataException>(() => C3MotionReader.Read(new byte[11]));

        BinaryPrimitives.WriteUInt32LittleEndian(data, 1001);
        Assert.Throws<InvalidDataException>(() => C3MotionReader.Read(data));

        using MemoryStream large = Header(1000, 1000, "KKEY");
        WriteUInt32(large, uint.MaxValue);
        Assert.Throws<InvalidDataException>(() => C3MotionReader.Read(large.ToArray()));

        using MemoryStream badIndex = Header(1, 1, "KKEY");
        WriteUInt32(badIndex, 1);
        WriteUInt32(badIndex, 0);
        WriteMatrix(badIndex, Matrix4x4.Identity);
        WriteUInt32(badIndex, -1);
        Assert.Throws<InvalidDataException>(() => C3MotionReader.Read(badIndex.ToArray()));
    }

    private static C3MeshSection Section(byte[] payload) => new(0x49544F4D, 24, payload);

    private static MemoryStream Header(uint bones, uint frames, string encoding)
    {
        MemoryStream result = new();
        WriteUInt32(result, bones);
        WriteUInt32(result, frames);
        result.Write(System.Text.Encoding.ASCII.GetBytes(encoding));
        return result;
    }

    private static void WriteMatrix(Stream output, Matrix4x4 m)
    {
        foreach (float value in new float[]
        {
            m.M11, m.M12, m.M13, m.M14,
            m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34,
            m.M41, m.M42, m.M43, m.M44
        })
        {
            WriteSingle(output, value);
        }
    }

    private static void WriteUInt32(Stream output, int value) => WriteUInt32(output, unchecked((uint)value));

    private static void WriteUInt32(Stream output, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        output.Write(bytes);
    }

    private static void WriteUInt16(Stream output, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        output.Write(bytes);
    }

    private static void WriteSingle(Stream output, float value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(bytes, value);
        output.Write(bytes);
    }
}
