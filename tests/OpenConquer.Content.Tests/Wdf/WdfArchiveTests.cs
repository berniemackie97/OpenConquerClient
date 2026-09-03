using System.Buffers.Binary;
using OpenConquer.Content.Wdf;

namespace OpenConquer.Content.Tests.Wdf;

public sealed class WdfArchiveTests
{
    [Fact]
    public void Open_ValidArchiveIndexesAndReadsEntries()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        byte[] expectedPayload = [1, 2, 3, 4];

        string archivePath = temporaryDirectory.WriteFile(
            "data.wdf",
            new WdfTestArchiveBuilder()
                .AddEntry("c3/example.c3", [9])
                .AddEntry("data/example.bin", expectedPayload)
                .Build()
        );

        WdfArchive archive = WdfArchive.Open(archivePath);

        Assert.Equal(2, archive.EntryCount);

        Assert.True(archive.TryOpenRead("DATA/EXAMPLE.BIN", out Stream? stream));

        Assert.NotNull(stream);

        using Stream entryStream = stream;

        Assert.Equal(expectedPayload.Length, entryStream.Length);

        using MemoryStream destination = new();

        entryStream.CopyTo(destination);

        Assert.Equal(expectedPayload, destination.ToArray());
    }

    [Fact]
    public void TryOpenRead_WhenEntryDoesNotExist_ReturnsFalse()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        string archivePath = temporaryDirectory.WriteFile(
            "data.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry("data/example.bin", [1])
        );

        WdfArchive archive = WdfArchive.Open(archivePath);

        Assert.False(archive.TryOpenRead("data/missing.bin", out Stream? stream));

        Assert.Null(stream);
    }

    [Fact]
    public void EntryStream_SeekRemainsBoundedToSelectedPayload()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        string archivePath = temporaryDirectory.WriteFile(
            "data.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry("data/example.bin", [10, 20, 30, 40])
        );

        WdfArchive archive = WdfArchive.Open(archivePath);

        Assert.True(archive.TryOpenRead("data/example.bin", out Stream? stream));

        Assert.NotNull(stream);

        using Stream entryStream = stream;

        Assert.Equal(2, entryStream.Seek(2, SeekOrigin.Begin));

        Assert.Equal(30, entryStream.ReadByte());

        Assert.Throws<IOException>(() => entryStream.Seek(1, SeekOrigin.End));

        Assert.Throws<IOException>(() => entryStream.Seek(-1, SeekOrigin.Begin));
    }

    [Fact]
    public void Open_AcceptsZeroLengthEntryAtPayloadBoundary()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        string archivePath = temporaryDirectory.WriteFile(
            "data.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry("data/empty.bin", [])
        );

        WdfArchive archive = WdfArchive.Open(archivePath);

        Assert.True(archive.TryOpenRead("data/empty.bin", out Stream? stream));

        Assert.NotNull(stream);

        using Stream entryStream = stream;

        Assert.Equal(0, entryStream.Length);

        Assert.Equal(-1, entryStream.ReadByte());
    }

    [Fact]
    public void Open_RejectsTruncatedHeader()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        string archivePath = temporaryDirectory.WriteFile(
            "data.wdf",
            new byte[WdfArchive.HeaderLength - 1]
        );

        Assert.Throws<InvalidDataException>(() => WdfArchive.Open(archivePath));
    }

    [Fact]
    public void Open_RejectsInvalidMagic()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        byte[] archive = new WdfTestArchiveBuilder().Build();

        BinaryPrimitives.WriteUInt32LittleEndian(archive, 0xDEADBEEFu);

        string archivePath = temporaryDirectory.WriteFile("data.wdf", archive);

        Assert.Throws<InvalidDataException>(() => WdfArchive.Open(archivePath));
    }

    [Fact]
    public void Open_RejectsEntryCountAboveSafetyLimitBeforeAllocation()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        byte[] archive = new byte[WdfArchive.HeaderLength];

        BinaryPrimitives.WriteUInt32LittleEndian(archive, WdfArchive.Magic);

        BinaryPrimitives.WriteUInt32LittleEndian(
            archive.AsSpan(4),
            checked((uint)WdfArchive.MaximumEntryCount + 1u)
        );

        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(8), WdfArchive.HeaderLength);

        string archivePath = temporaryDirectory.WriteFile("data.wdf", archive);

        Assert.Throws<InvalidDataException>(() => WdfArchive.Open(archivePath));
    }

    [Fact]
    public void Open_RejectsEntryTableBeforePayloadRegion()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        byte[] archive = new byte[WdfArchive.HeaderLength];

        BinaryPrimitives.WriteUInt32LittleEndian(archive, WdfArchive.Magic);

        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(4), 0);

        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(8), WdfArchive.HeaderLength - 1);

        string archivePath = temporaryDirectory.WriteFile("data.wdf", archive);

        Assert.Throws<InvalidDataException>(() => WdfArchive.Open(archivePath));
    }

    [Fact]
    public void Open_RejectsTruncatedEntryTable()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        byte[] archive = new byte[WdfArchive.HeaderLength];

        BinaryPrimitives.WriteUInt32LittleEndian(archive, WdfArchive.Magic);

        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(4), 1);

        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(8), WdfArchive.HeaderLength);

        string archivePath = temporaryDirectory.WriteFile("data.wdf", archive);

        Assert.Throws<InvalidDataException>(() => WdfArchive.Open(archivePath));
    }

    [Fact]
    public void Open_RejectsNonZeroReservedEntryField()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        byte[] archive = new WdfTestArchiveBuilder()
            .AddEntry(id: 1, payload: [7], reserved: 1)
            .Build();

        string archivePath = temporaryDirectory.WriteFile("data.wdf", archive);

        Assert.Throws<InvalidDataException>(() => WdfArchive.Open(archivePath));
    }

    [Fact]
    public void Open_RejectsDuplicateEntryIds()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        byte[] archive = new WdfTestArchiveBuilder()
            .AddEntry(id: 7, payload: [1])
            .AddEntry(id: 7, payload: [2])
            .Build();

        string archivePath = temporaryDirectory.WriteFile("data.wdf", archive);

        Assert.Throws<InvalidDataException>(() => WdfArchive.Open(archivePath));
    }

    [Fact]
    public void Open_RejectsUnsortedEntryIds()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        byte[] archive = new WdfTestArchiveBuilder()
            .AddEntry(id: 2, payload: [1])
            .AddEntry(id: 1, payload: [2])
            .Build(sortEntries: false);

        string archivePath = temporaryDirectory.WriteFile("data.wdf", archive);

        Assert.Throws<InvalidDataException>(() => WdfArchive.Open(archivePath));
    }

    [Fact]
    public void Open_RejectsEntryPayloadInsideHeader()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        byte[] archive = WdfTestArchiveBuilder.CreateSingleEntry("data/example.bin", [1]);

        int tableOffset = GetTableOffset(archive);

        BinaryPrimitives.WriteUInt32LittleEndian(
            archive.AsSpan(tableOffset + 4),
            WdfArchive.HeaderLength - 1
        );

        string archivePath = temporaryDirectory.WriteFile("data.wdf", archive);

        Assert.Throws<InvalidDataException>(() => WdfArchive.Open(archivePath));
    }

    [Fact]
    public void Open_RejectsEntryPayloadThatExtendsIntoIndex()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        byte[] archive = WdfTestArchiveBuilder.CreateSingleEntry("data/example.bin", [1, 2]);

        int tableOffset = GetTableOffset(archive);

        BinaryPrimitives.WriteUInt32LittleEndian(
            archive.AsSpan(tableOffset + 4),
            checked((uint)tableOffset - 1u)
        );

        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(tableOffset + 8), 2);

        string archivePath = temporaryDirectory.WriteFile("data.wdf", archive);

        Assert.Throws<InvalidDataException>(() => WdfArchive.Open(archivePath));
    }

    [Fact]
    public void Open_RejectsSymbolicLinkArchive()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        string targetPath = temporaryDirectory.WriteFile(
            "actual-data.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry("data/example.bin", [1])
        );

        string linkPath = Path.Combine(temporaryDirectory.RootPath, "data.wdf");

        _ = File.CreateSymbolicLink(linkPath, targetPath);

        Assert.Throws<IOException>(() => WdfArchive.Open(linkPath));
    }

    private static int GetTableOffset(ReadOnlySpan<byte> archive)
    {
        return checked((int)BinaryPrimitives.ReadUInt32LittleEndian(archive[8..]));
    }
}
