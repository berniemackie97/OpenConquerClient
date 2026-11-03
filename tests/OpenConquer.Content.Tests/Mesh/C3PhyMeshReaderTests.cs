using System.Buffers.Binary;
using System.Numerics;
using OpenConquer.Content.Mesh;

namespace OpenConquer.Content.Tests.Mesh;

public sealed class C3PhyMeshReaderTests
{
    [Theory]
    [InlineData("PHY ", -1f)]
    [InlineData("PHY2", 1f)]
    [InlineData("PHY3", 1f)]
    [InlineData("PHY4", -1f)]
    public void ReadsVertexLayoutsAndRetainsGeometry(string tag, float normalZ)
    {
        C3PhyMesh mesh = C3PhyMeshReader.Read(Section(tag, Payload(tag)));

        Assert.Equal("Plane01", mesh.Name);
        Assert.Equal(2u, mesh.VertexSectionFlags);
        Assert.Equal(3, mesh.Vertices.Count);
        Assert.Equal(new ushort[] { 0, 1, 2 }, mesh.Indices);
        Assert.Equal(new Vector3(10, 20, -5), mesh.Vertices[0].Position);
        Assert.Equal(new Vector2(0.25f, 0.5f), mesh.Vertices[0].TextureCoordinate);
        Assert.Equal(1f, mesh.Vertices[0].BoneData.W);
        Assert.Equal(normalZ, mesh.Vertices[0].Normal.Z);
        Assert.Equal(new Vector3(-2), mesh.BoundsMin);
        Assert.Equal(new Vector3(2), mesh.BoundsMax);
        Assert.Equal(1u, mesh.StepFrameCount);
        Assert.Equal(16, mesh.AlphaKeys.Length);
        Assert.True(mesh.DrawKeys.IsEmpty);
        Assert.True(mesh.TextureKeys.IsEmpty);
        Assert.Equal("STEP"u8.ToArray().Concat(new byte[8]).ToArray(), mesh.TrailingData.ToArray());
    }

    [Fact]
    public void AppliesRecordedMeshTransformToFirstVertex()
    {
        C3PhyMesh mesh = C3PhyMeshReader.Read(Section("PHY ", Payload("PHY ", fixtureTransform: true)));
        Vector3 position = mesh.Vertices[0].Position;

        Assert.InRange(position.X, -79.60363f, -79.60343f);
        Assert.InRange(position.Y, -39.80187f, -39.80167f);
        Assert.InRange(position.Z, -4.999874f, -4.999674f);
        Assert.Equal(new Vector2(0.0004995167f, 0.9995000362f),
            mesh.Vertices[0].TextureCoordinate);
    }

    [Fact]
    public void SmoothsGeneratedNormalsAcrossSharedPositions()
    {
        C3PhyVertex[] vertices =
        [
            Vertex(0, 0, 0), Vertex(1, 0, 0), Vertex(0, 1, 0),
            Vertex(0.005f, 0, 0), Vertex(0, 1, 0), Vertex(0, 0, 1)
        ];

        C3PhyNormals.Generate(vertices, [0, 1, 2, 3, 4, 5]);

        Assert.Equal(vertices[0].Normal, vertices[3].Normal);
        Assert.Equal(vertices[2].Normal, vertices[4].Normal);
        Assert.NotEqual(Vector3.Zero, vertices[0].Normal);
    }

    [Fact]
    public void SmoothingUpdatesEveryMatchedVertex()
    {
        C3PhyVertex[] vertices =
        [
            Vertex(0, 0, 0), Vertex(1, 0, 0), Vertex(0, 1, 0),
            Vertex(0, 0, 0), Vertex(0, 1, 0), Vertex(0, 0, 1),
            Vertex(0, 0, 0), Vertex(0, 0, 1), Vertex(1, 0, 0)
        ];

        C3PhyNormals.Generate(vertices, [0, 1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Equal(vertices[0].Normal, vertices[3].Normal);
        Assert.Equal(vertices[3].Normal, vertices[6].Normal);
        Assert.NotEqual(Vector3.Zero, vertices[0].Normal);
    }

    [Fact]
    public void PreservesUnitNormalsAndZerosDegenerateFaces()
    {
        C3PhyVertex[] vertices =
        [
            Vertex(0, 0, 0), Vertex(0, 0, 1), Vertex(-0.8f, 0.6f, 0)
        ];

        C3PhyNormals.Generate(vertices, [0, 1, 2]);
        Assert.Equal(new Vector3(0.6f, 0.8f, 0), vertices[0].Normal);

        C3PhyVertex[] degenerate = [Vertex(0, 0, 0), Vertex(0, 0, 0), Vertex(0, 0, 0)];
        C3PhyNormals.Generate(degenerate, [0, 1, 2]);
        Assert.All(degenerate, vertex => Assert.Equal(Vector3.Zero, vertex.Normal));
    }

    [Fact]
    public void RejectsTruncatedPayloadAndInvalidIndices()
    {
        byte[] payload = Payload("PHY ");
        Assert.Throws<InvalidDataException>(() => C3PhyMeshReader.Read(
            Section("PHY ", payload.AsMemory(0, payload.Length - 64))));

        byte[] badIndex = Payload("PHY ", lastIndex: 3);
        Assert.Throws<InvalidDataException>(() => C3PhyMeshReader.Read(Section("PHY ", badIndex)));
    }

    [Fact]
    public void RejectsUnsupportedLayoutAndExcessiveCounts()
    {
        byte[] payload = Payload("PHY ");
        Assert.Throws<InvalidDataException>(() => C3PhyMeshReader.Read(Section("PHY?", payload)));

        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(15), uint.MaxValue);
        Assert.Throws<InvalidDataException>(() => C3PhyMeshReader.Read(Section("PHY ", payload)));
    }

    private static C3PhyVertex Vertex(float x, float y, float z) =>
        new(new Vector3(x, y, z), Vector3.Zero, Vector2.Zero, Vector4.Zero, 0);

    private static C3MeshSection Section(string tag, ReadOnlyMemory<byte> payload) =>
        new(BinaryPrimitives.ReadUInt32LittleEndian(System.Text.Encoding.ASCII.GetBytes(tag)), 24, payload);

    private static byte[] Payload(string tag, ushort lastIndex = 2, bool fixtureTransform = false)
    {
        using MemoryStream output = new();
        WriteUInt32(output, 7);
        output.Write("Plane01"u8);
        WriteUInt32(output, 2);
        WriteUInt32(output, 2);
        WriteUInt32(output, 1);

        Vector3[] positions = fixtureTransform
            ? [new(-6, -3, 0.0000170716048f), new(1, 0, 0), new(0, 1, 0)]
            : [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)];
        for (int i = 0; i < positions.Length; i++)
        {
            WriteFloat(output, positions[i].X);
            WriteFloat(output, positions[i].Y);
            WriteFloat(output, positions[i].Z);
            if (tag is "PHY " or "PHY2")
            {
                output.Write(new byte[36]);
            }

            WriteFloat(output, fixtureTransform && i == 0 ? 0.0004995167f : 0.25f);
            WriteFloat(output, fixtureTransform && i == 0 ? 0.9995000362f : 0.5f);
            for (int j = 0; j < 5; j++)
            {
                WriteFloat(output, j == 3 ? 1 : 0);
            }

            if (tag is "PHY2" or "PHY3")
            {
                WriteFloat(output, 0);
                WriteFloat(output, 0);
                WriteFloat(output, 1);
            }
        }

        WriteUInt32(output, 1);
        WriteUInt32(output, 0);
        WriteUInt16(output, 0);
        WriteUInt16(output, 1);
        WriteUInt16(output, lastIndex);
        WriteUInt32(output, 3);
        output.Write([0xAB, 0xCD, 0xEF]);

        foreach (float value in new float[] { 2, 2, 2, -2, -2, -2 })
        {
            WriteFloat(output, value);
        }

        float scale = fixtureTransform ? 13.267255783081055f : 1;
        float[] matrix =
        [
            scale, 0, 0, 0,
            0, scale, 0, 0,
            0, 0, scale, 0,
            fixtureTransform ? 0 : 10, fixtureTransform ? 0 : 20, -5, 1
        ];
        foreach (float value in matrix)
        {
            WriteFloat(output, value);
        }

        WriteUInt32(output, 1);
        WriteUInt32(output, 1);
        output.Write(new byte[16]);
        WriteUInt32(output, 0);
        WriteUInt32(output, 0);
        output.Write("STEP"u8);
        output.Write(new byte[8]);
        return output.ToArray();
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteFloat(Stream stream, float value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(bytes, value);
        stream.Write(bytes);
    }
}
