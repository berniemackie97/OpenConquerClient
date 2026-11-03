using System.Numerics;
using System.Runtime.InteropServices;
using OpenConquer.Content.Mesh;

namespace OpenConquer.Content.Tests.Mesh;

public sealed class C3PhySkinnerTests
{
    [Fact]
    public void ReproducesCapturedVertexDeformationAtFirstAndLastFrame()
    {
        Vector3 loadedPosition = new(-79.60353088378906f, -39.80176544189453f, -4.9997735023498535f);
        Vector3 loadedNormal = new(-0.4472135901451111f, -0.8944271802902222f, 0f);
        Vector2 uv = new(0.0004995167255401611f, 0.999500036239624f);
        C3PhyMesh mesh = Mesh([
            Vertex(loadedPosition, loadedNormal, uv, index2: 0, index1: 1, weight2: 1f)
        ]);
        Matrix4x4 bone0 = new(
            0.9975640774f, -0.0697564706f, 0, 0,
            0.0697564706f, 0.9975640774f, 0, 0,
            0, 0, 1, 0,
            0.0343960971f, 1.9419987202f, 0, 1);
        Matrix4x4[] frames = new Matrix4x4[202];
        uint[] frameIndices = new uint[101];
        for (int frame = 0; frame < 101; frame++)
        {
            frameIndices[frame] = (uint)frame;
            frames[frame * 2] = bone0;
            frames[frame * 2 + 1] = Matrix4x4.Identity;
        }

        C3Motion motion = new(2, 101, 0, frameIndices, frames);
        Matrix4x4[] palette = new Matrix4x4[2];
        C3PhyOutputVertex[] output = new C3PhyOutputVertex[1];

        foreach (int frame in new[] { 0, 50, 100 })
        {
            C3PhySkinner.Calculate(mesh, motion, frame, null, 0, 0, palette, output);
            AssertClose(-82.15165710449219f, output[0].Position.X);
            AssertClose(-32.2099494934082f, output[0].Position.Y);
            AssertClose(-4.9997735023498535f, output[0].Position.Z);
            AssertClose(-0.5085163116455078f, output[0].Normal.X);
            AssertClose(-0.861052393913269f, output[0].Normal.Y);
            Assert.Equal(uv, output[0].TextureCoordinate);
            Assert.Equal(uint.MaxValue, output[0].DiffuseArgb);
        }

        Assert.Equal(loadedPosition, mesh.Vertices[0].Position);
        Assert.Equal(new uint[] { 0 }, mesh.UsedBoneIndices);
        Assert.Equal(36, Marshal.SizeOf<C3PhyOutputVertex>());
    }

    [Fact]
    public void BlendsTwoWeightedBoneMatricesWithoutNormalRenormalization()
    {
        C3PhyMesh mesh = Mesh([
            Vertex(new Vector3(1, 2, 3), Vector3.UnitY, new Vector2(0.1f, 0.2f),
                index2: 0, index1: 1, weight2: 0.25f, weight1: 0.75f)
        ]);
        Matrix4x4 second = Matrix4x4.CreateScale(1f, 3f, 1f);
        second.M42 = 20f;
        C3Motion motion = new(2, 1, 0, [0],
            [Matrix4x4.CreateTranslation(10, 0, 0), second]);
        C3PhyOutputVertex[] output = new C3PhyOutputVertex[1];
        Matrix4x4[] palette = new Matrix4x4[2];

        C3PhySkinner.Calculate(mesh, motion, 0, null, 0, 0, palette, output,
            diffuseArgb: 0x80AABBCC, textureOffset: new Vector2(0.25f, 0.5f));

        Assert.Equal(new Vector3(3.5f, 20f, 3f), output[0].Position);
        Assert.Equal(new Vector3(0, 2.5f, 0), output[0].Normal);
        AssertClose(0.35f, output[0].TextureCoordinate.X);
        AssertClose(0.7f, output[0].TextureCoordinate.Y);
        Assert.Equal(0x80AABBCCu, output[0].DiffuseArgb);
        Assert.Equal(new uint[] { 0, 1 }, mesh.UsedBoneIndices);
    }

    [Fact]
    public void SupportsSecondaryFrameBlendingWithoutClamping()
    {
        C3PhyMesh mesh = Mesh([Vertex(new Vector3(2, 0, 0), Vector3.UnitX,
            Vector2.Zero, index2: 0, weight2: 1f)]);
        C3Motion first = new(1, 1, 0, [0], [Matrix4x4.CreateTranslation(4, 0, 0)]);
        C3Motion second = new(1, 1, 0, [0], [Matrix4x4.CreateTranslation(12, 0, 0)]);
        Matrix4x4[] palette = new Matrix4x4[1];
        C3PhyOutputVertex[] output = new C3PhyOutputVertex[1];

        C3PhySkinner.Calculate(mesh, first, 0, second, 0, 1.5f, palette, output);

        Assert.Equal(18f, output[0].Position.X);
        Assert.Equal(Vector3.UnitX, output[0].Normal);
    }

    [Fact]
    public void PreservesUnweightedVerticesAndHandlesOutOfRangeBones()
    {
        C3PhyMesh mesh = Mesh([
            Vertex(new Vector3(7, 8, 9), Vector3.UnitZ, Vector2.Zero,
                index2: 900, weight2: 0f),
            Vertex(new Vector3(1, 2, 3), Vector3.UnitY, Vector2.Zero,
                index2: 900, weight2: 1f)
        ]);
        C3Motion motion = new(1, 1, 0, [0], [Matrix4x4.Identity]);
        C3PhyOutputVertex[] output = new C3PhyOutputVertex[2];

        C3PhySkinner.Calculate(mesh, motion, 0, null, 0, 0,
            new Matrix4x4[1], output);

        Assert.Equal(new Vector3(7, 8, 9), output[0].Position);
        Assert.Equal(Vector3.UnitZ, output[0].Normal);
        Assert.Equal(new Vector3(1, 2, 3), output[1].Position);
        Assert.Equal(Vector3.UnitY, output[1].Normal);
    }

    [Fact]
    public void RequiresAdequateCallerBuffersAndSampleableMotion()
    {
        C3PhyMesh mesh = Mesh([Vertex(Vector3.One, Vector3.UnitZ,
            Vector2.Zero, index2: 0, weight2: 1f)]);
        C3Motion motion = new(1, 1, 0, [0], [Matrix4x4.Identity]);
        C3PhyOutputVertex[] output = new C3PhyOutputVertex[1];

        Assert.Throws<ArgumentException>(() =>
            C3PhySkinner.Calculate(mesh, motion, 0, null, 0, 0, [], output));
        Assert.Throws<ArgumentException>(() =>
            C3PhySkinner.Calculate(mesh, motion, 0, null, 0, 0, new Matrix4x4[1], []));
        C3Motion empty = new(1, 1, 0, [], []);
        Assert.Throws<InvalidDataException>(() =>
            C3PhySkinner.Calculate(mesh, empty, 0, null, 0, 0, new Matrix4x4[1], output));
    }

    private static C3PhyMesh Mesh(C3PhyVertex[] vertices) =>
        new("Mesh", 0, vertices, [], Matrix4x4.Identity, Vector3.Zero,
            Vector3.Zero, 1, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty,
            ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);

    private static C3PhyVertex Vertex(Vector3 position, Vector3 normal, Vector2 uv,
        uint index2, float weight2, uint index1 = 0, float weight1 = 0) =>
        new(position, normal, uv, 0, index2, index1, weight2, weight1);

    private static void AssertClose(float expected, float actual) =>
        Assert.InRange(actual, expected - 0.0001f, expected + 0.0001f);
}
