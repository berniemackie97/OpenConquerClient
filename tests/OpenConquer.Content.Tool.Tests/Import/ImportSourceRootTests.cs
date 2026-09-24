using System.Buffers.Binary;
using OpenConquer.Content.Tool.Import;

namespace OpenConquer.Content.Tool.Tests.Import;

public sealed class ImportSourceRootTests
{
    private const uint ProgressBackgroundUid = 0x0561D7F3;

    [Fact]
    public void OpenRequiredRead_LooseThenPackage_FallsBackToPackage()
    {
        using TemporarySourceTree source = new();

        source.WriteStartupSnapshot();

        byte[] packagedPayload = "DDS packaged"u8.ToArray();
        source.WriteBytes("data.wdf", CreateWdf(ProgressBackgroundUid, packagedPayload));

        ImportSourceRoot sourceRoot = ImportSourceRoot.Open(source.RootPath);
        ClientContentRequirement requirement = new("data/main/ProgressBk.dds", ContentLookupMode.LooseThenPackage);

        using Stream stream = sourceRoot.OpenRequiredRead(requirement, out string sourcePath);

        Assert.Equal("data/main/ProgressBk.dds", sourcePath);
        Assert.Equal(packagedPayload, ReadAllBytes(stream));
    }

    [Fact]
    public void OpenRequiredRead_LooseThenPackage_PrefersLooseFile()
    {
        using TemporarySourceTree source = new();

        source.WriteStartupSnapshot();

        byte[] packagedPayload = "DDS packaged"u8.ToArray();
        byte[] loosePayload = "DDS loose"u8.ToArray();

        source.WriteBytes("data.wdf", CreateWdf(ProgressBackgroundUid, packagedPayload));
        source.WriteBytes("data/main/ProgressBk.dds", loosePayload);

        ImportSourceRoot sourceRoot = ImportSourceRoot.Open(source.RootPath);
        ClientContentRequirement requirement = new("data/main/ProgressBk.dds", ContentLookupMode.LooseThenPackage);

        using Stream stream = sourceRoot.OpenRequiredRead(requirement, out string sourcePath);

        Assert.Equal("data/main/ProgressBk.dds", sourcePath);
        Assert.Equal(loosePayload, ReadAllBytes(stream));
    }

    [Fact]
    public void OpenRequiredRead_PackageOnly_BypassesLooseFile()
    {
        using TemporarySourceTree source = new();

        source.WriteStartupSnapshot();

        byte[] packagedPayload = "DDS packaged"u8.ToArray();
        byte[] loosePayload = "DDS loose"u8.ToArray();

        source.WriteBytes("data.wdf", CreateWdf(ProgressBackgroundUid, packagedPayload));
        source.WriteBytes("data/main/ProgressBk.dds", loosePayload);

        ImportSourceRoot sourceRoot = ImportSourceRoot.Open(source.RootPath);
        ClientContentRequirement requirement = new("data/main/ProgressBk.dds", ContentLookupMode.PackageOnly);

        using Stream stream = sourceRoot.OpenRequiredRead(requirement, out string sourcePath);

        Assert.Equal("data/main/ProgressBk.dds", sourcePath);
        Assert.Equal(packagedPayload, ReadAllBytes(stream));
    }

    private static byte[] CreateWdf(uint uid, ReadOnlySpan<byte> payload)
    {
        const int headerLength = 12;
        const int entryLength = 16;

        int tableOffset = checked(headerLength + payload.Length);
        byte[] archive = new byte[checked(tableOffset + entryLength)];

        BinaryPrimitives.WriteUInt32LittleEndian(archive, 0x57444650);
        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(8), checked((uint)tableOffset));

        payload.CopyTo(archive.AsSpan(headerLength));

        Span<byte> entry = archive.AsSpan(tableOffset, entryLength);

        BinaryPrimitives.WriteUInt32LittleEndian(entry, uid);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[4..], headerLength);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[8..], checked((uint)payload.Length));

        return archive;
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        byte[] bytes = new byte[checked((int)stream.Length)];

        stream.ReadExactly(bytes);

        return bytes;
    }
}
