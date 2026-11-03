using System.Buffers.Binary;
using System.Text;
using OpenConquer.Content.Mesh;

namespace OpenConquer.Content.Tests.Mesh;

public sealed class C3MeshFileTests
{
    [Fact]
    public void Parse_PreservesOrderedSectionsAndPayloads()
    {
        byte[] source = FileWithSections(
            ("PHY ", [1, 2, 3]),
            ("MOTI", [4, 5]),
            ("ABCD", [6]));

        C3MeshFile file = C3MeshFile.Parse(source);

        Assert.Equal(1, file.PhyCount);
        Assert.Equal(3, file.Sections.Count);
        Assert.Equal([1, 2, 3], file.Sections[0].Payload.ToArray());
        Assert.Equal([4, 5], file.Sections[1].Payload.ToArray());
        Assert.Equal([6], file.Sections[2].Payload.ToArray());
        Assert.Equal(24, file.Sections[0].Offset);
        Assert.Equal(35, file.Sections[1].Offset);
        Assert.Equal(45, file.Sections[2].Offset);
        Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian("MOTI"u8), file.Sections[1].Identifier);
    }

    [Theory]
    [InlineData("PHY ")]
    [InlineData("PHY2")]
    [InlineData("PHY3")]
    [InlineData("PHY4")]
    public void Parse_RecognizesSupportedPhyVariants(string kind)
    {
        C3MeshFile file = C3MeshFile.Parse(FileWithSections((kind, [42])));
        Assert.Equal(1, file.PhyCount);
        Assert.Single(file.Sections);
    }

    [Fact]
    public void Parse_DoesNotCountUnrecognizedChunksAsPhy()
    {
        C3MeshFile file = C3MeshFile.Parse(FileWithSections(("PHY?", [1]), ("DATA", [2])));
        Assert.Equal(0, file.PhyCount);
        Assert.Equal(2, file.Sections.Count);
    }

    [Fact]
    public void Parse_RejectsMoreThanSixteenPhySections()
    {
        (string, byte[])[] sections = Enumerable.Range(0, 17)
            .Select(_ => ("PHY ", new byte[] { 0 }))
            .ToArray();

        Assert.Throws<InvalidDataException>(() => C3MeshFile.Parse(FileWithSections(sections)));
    }

    [Fact]
    public void Parse_RejectsTruncatedHeaderAndDeclaredPayload()
    {
        Assert.Throws<InvalidDataException>(() => C3MeshFile.Parse(new byte[15]));
        Assert.Throws<InvalidDataException>(() => C3MeshFile.Parse(new byte[17]));

        byte[] data = FileWithSections(("PHY ", [1]));
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(20), uint.MaxValue);
        Assert.Throws<InvalidDataException>(() => C3MeshFile.Parse(data));
    }

    [Fact]
    public void Parse_RejectsOversizedFiles()
    {
        Assert.Throws<InvalidDataException>(() =>
            C3MeshFile.Parse(new byte[C3MeshFile.MaximumFileBytes + 1]));
    }

    private static byte[] FileWithSections(params (string Kind, byte[] Payload)[] sections)
    {
        using MemoryStream output = new();
        output.Write(new byte[16]);
        foreach ((string kind, byte[] payload) in sections)
        {
            byte[] name = Encoding.ASCII.GetBytes(kind);
            if (name.Length != 4)
            {
                throw new ArgumentException("Section names must contain four bytes.", nameof(sections));
            }

            output.Write(name);
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(length, (uint)payload.Length);
            output.Write(length);
            output.Write(payload);
        }

        return output.ToArray();
    }
}
