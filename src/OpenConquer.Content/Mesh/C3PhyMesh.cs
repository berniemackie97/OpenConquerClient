using System.Numerics;

namespace OpenConquer.Content.Mesh;

public readonly record struct C3PhyVertex(
    Vector3 Position, Vector3 Normal, Vector2 TextureCoordinate,
    Vector4 BoneData, float BoneData4);

public sealed class C3PhyMesh
{
    internal C3PhyMesh(string name, uint flags, C3PhyVertex[] vertices, ushort[] indices,
        Matrix4x4 objectTransform, Vector3 boundsMin, Vector3 boundsMax, uint stepFrameCount, ReadOnlyMemory<byte> alphaKeys,
        ReadOnlyMemory<byte> drawKeys, ReadOnlyMemory<byte> textureKeys, ReadOnlyMemory<byte> trailingData)
    {
        Name = name;
        VertexSectionFlags = flags;
        Vertices = Array.AsReadOnly(vertices);
        Indices = Array.AsReadOnly(indices);
        ObjectTransform = objectTransform;
        BoundsMin = boundsMin;
        BoundsMax = boundsMax;
        StepFrameCount = stepFrameCount;
        AlphaKeys = alphaKeys;
        DrawKeys = drawKeys;
        TextureKeys = textureKeys;
        TrailingData = trailingData;
    }

    public string Name
    {
        get;
    }

    public uint VertexSectionFlags
    {
        get;
    }

    public IReadOnlyList<C3PhyVertex> Vertices
    {
        get;
    }

    public IReadOnlyList<ushort> Indices
    {
        get;
    }

    public Matrix4x4 ObjectTransform
    {
        get;
    }

    public Vector3 BoundsMin
    {
        get;
    }

    public Vector3 BoundsMax
    {
        get;
    }

    public uint StepFrameCount
    {
        get;
    }

    public ReadOnlyMemory<byte> AlphaKeys
    {
        get;
    }

    public ReadOnlyMemory<byte> DrawKeys
    {
        get;
    }

    public ReadOnlyMemory<byte> TextureKeys
    {
        get;
    }

    public ReadOnlyMemory<byte> TrailingData
    {
        get;
    }
}
