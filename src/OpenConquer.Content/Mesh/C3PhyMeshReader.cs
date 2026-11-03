using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace OpenConquer.Content.Mesh;

public static class C3PhyMeshReader
{
    public static C3PhyMesh Read(C3MeshSection section)
    {
        if ((section.Identifier & 0x00FFFFFF) != 0x00594850)
        {
            throw new InvalidDataException("The section is not a PHY mesh.");
        }

        byte variant = (byte)(section.Identifier >> 24);
        int stride = variant switch
        {
            0x20 => 76,
            0x32 => 88,
            0x33 => 52,
            0x34 => 40,
            _ => throw new InvalidDataException("Unsupported PHY vertex layout.")
        };

        PayloadReader reader = new(section.Payload);
        int nameLength = reader.ReadCount();
        string name = Encoding.Latin1.GetString(reader.Read(nameLength).Span).TrimEnd('\0');
        uint flags = reader.ReadUInt32();
        int vertexCount = SumCounts(reader.ReadCount(), reader.ReadCount());
        ReadOnlyMemory<byte> vertexBytes = reader.ReadItems(vertexCount, stride);
        C3PhyVertex[] vertices = new C3PhyVertex[vertexCount];
        bool generatedNormals = variant is 0x20 or 0x34;
        bool split = variant is 0x20 or 0x32;
        for (int i = 0; i < vertices.Length; i++)
        {
            ReadOnlySpan<byte> input = vertexBytes.Span.Slice(i * stride, stride);
            int uvOffset = split ? 48 : 12;
            int boneOffset = uvOffset + 8;
            Vector3 position = ReadVector3(input);
            Vector2 uv = new(ReadFloat(input, uvOffset), ReadFloat(input, uvOffset + 4));
            Vector4 bones = new(ReadFloat(input, boneOffset), ReadFloat(input, boneOffset + 4),
                ReadFloat(input, boneOffset + 8), ReadFloat(input, boneOffset + 12));
            float bone4 = ReadFloat(input, boneOffset + 16);
            Vector3 normal = generatedNormals ? Vector3.Zero : ReadVector3(input[(boneOffset + 20)..]);
            vertices[i] = new C3PhyVertex(position, normal, uv, bones, bone4);
        }

        int faceCount = SumCounts(reader.ReadCount(), reader.ReadCount());
        ReadOnlySpan<byte> indexBytes = reader.ReadItems(faceCount, 6).Span;
        ushort[] indices = new ushort[faceCount * 3];
        for (int i = 0; i < indices.Length; i++)
        {
            ushort index = BinaryPrimitives.ReadUInt16LittleEndian(indexBytes[(i * 2)..]);
            if (index >= vertexCount)
            {
                throw new InvalidDataException("PHY triangle index exceeds its vertex count.");
            }
            indices[i] = index;
        }

        reader.Read(reader.ReadCount());
        Vector3 boundsMin = ReadVector3(reader.Read(12).Span);
        Vector3 boundsMax = ReadVector3(reader.Read(12).Span);
        Vector3 minimum = Vector3.Min(boundsMin, boundsMax);
        Vector3 maximum = Vector3.Max(boundsMin, boundsMax);

        ReadOnlySpan<byte> matrixBytes = reader.Read(64).Span;
        Matrix4x4 matrix = new(
            ReadFloat(matrixBytes, 0), ReadFloat(matrixBytes, 4), ReadFloat(matrixBytes, 8), ReadFloat(matrixBytes, 12),
            ReadFloat(matrixBytes, 16), ReadFloat(matrixBytes, 20), ReadFloat(matrixBytes, 24), ReadFloat(matrixBytes, 28),
            ReadFloat(matrixBytes, 32), ReadFloat(matrixBytes, 36), ReadFloat(matrixBytes, 40), ReadFloat(matrixBytes, 44),
            ReadFloat(matrixBytes, 48), ReadFloat(matrixBytes, 52), ReadFloat(matrixBytes, 56), ReadFloat(matrixBytes, 60));

        for (int i = 0; i < vertices.Length; i++)
        {
            C3PhyVertex vertex = vertices[i];
            Vector4 transformed = Vector4.Transform(new Vector4(vertex.Position, 1), matrix);
            if (MathF.Abs(transformed.W - 1f) > 1.1920929e-7f)
            {
                transformed /= transformed.W;
            }

            if (!float.IsFinite(transformed.X) || !float.IsFinite(transformed.Y) ||
                !float.IsFinite(transformed.Z))
            {
                throw new InvalidDataException("PHY vertex transform produces invalid coordinates.");
            }

            vertices[i] = vertex with
            {
                Position = new Vector3(transformed.X, transformed.Y, transformed.Z)
            };
        }

        uint stepFrameCount = reader.ReadUInt32();
        ReadOnlyMemory<byte> alphaKeys = reader.ReadItems(reader.ReadCount(), 16);
        ReadOnlyMemory<byte> drawKeys = reader.ReadItems(reader.ReadCount(), 16);
        ReadOnlyMemory<byte> textureKeys = reader.ReadItems(reader.ReadCount(), 16);
        ReadOnlyMemory<byte> trailing = reader.Read(reader.Remaining);

        if (generatedNormals)
        {
            C3PhyNormals.Generate(vertices, indices);
        }

        return new C3PhyMesh(name, flags, vertices, indices, matrix, minimum, maximum, stepFrameCount,
            alphaKeys, drawKeys, textureKeys, trailing);
    }

    private static int SumCounts(int first, int second) =>
        (long)first + second <= int.MaxValue
            ? first + second
            : throw new InvalidDataException("PHY element count exceeds supported bounds.");

    private static Vector3 ReadVector3(ReadOnlySpan<byte> input) =>
        new(ReadFloat(input, 0), ReadFloat(input, 4), ReadFloat(input, 8));

    private static float ReadFloat(ReadOnlySpan<byte> input, int offset) =>
        BinaryPrimitives.ReadSingleLittleEndian(input[offset..]);

    private struct PayloadReader(ReadOnlyMemory<byte> data)
    {
        private int _offset;

        internal int Remaining => data.Length - _offset;

        internal ReadOnlyMemory<byte> Read(int length)
        {
            if (length < 0 || length > Remaining)
            {
                throw new InvalidDataException("PHY section ends inside a required field.");
            }

            ReadOnlyMemory<byte> result = data.Slice(_offset, length);
            _offset += length;
            return result;
        }

        internal uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(Read(4).Span);

        internal int ReadCount()
        {
            uint count = ReadUInt32();
            if (count > int.MaxValue)
            {
                throw new InvalidDataException("PHY element count exceeds supported bounds.");
            }
            return (int)count;
        }

        internal ReadOnlyMemory<byte> ReadItems(int count, int stride)
        {
            if (count > Remaining / stride)
            {
                throw new InvalidDataException("PHY element array exceeds its section boundary.");
            }
            return Read(count * stride);
        }
    }
}
