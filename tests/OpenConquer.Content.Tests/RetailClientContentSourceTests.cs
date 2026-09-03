using System.Buffers.Binary;
using OpenConquer.Content.Wdf;

namespace OpenConquer.Content.Tests;

public sealed class RetailClientContentSourceTests
{
    [Fact]
    public void TryOpenRead_UsesWdfPackageWhenLooseFileIsMissing()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\n");
        temporaryDirectory.WriteFile("data.wdf", CreateWdf("data/example.bin", [1, 2, 3]));

        RetailClientContentSource source = RetailClientContentSource.Open(temporaryDirectory.RootPath);

        Assert.True(source.TryOpenRead("DATA/EXAMPLE.BIN", out Stream? stream));

        using (stream)
        {
            using MemoryStream destination = new();
            stream.CopyTo(destination);
            Assert.Equal([1, 2, 3], destination.ToArray());
        }
    }

    [Fact]
    public void TryOpenRead_PrefersLooseFileOverWdfEntry()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\n");
        temporaryDirectory.WriteFile("data.wdf", CreateWdf("data/example.bin", [1, 2, 3]));
        temporaryDirectory.WriteFile("data/example.bin", [9, 8, 7]);

        RetailClientContentSource source = RetailClientContentSource.Open(temporaryDirectory.RootPath);

        using Stream stream = source.OpenRequiredRead("data/example.bin");
        using MemoryStream destination = new();
        stream.CopyTo(destination);

        Assert.Equal([9, 8, 7], destination.ToArray());
    }

    [Fact]
    public void Open_ReportsDeclaredPackagesThatAreNotPresent()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf c3.wdf\n");
        temporaryDirectory.WriteFile("data.wdf", CreateWdf("data/example.bin", [1]));

        RetailClientContentSource source = RetailClientContentSource.Open(temporaryDirectory.RootPath);

        Assert.Equal(["c3.wdf"], source.MissingPackageNames);
    }

    private static byte[] CreateWdf(string contentPath, ReadOnlySpan<byte> payload)
    {
        int tableOffset = WdfArchive.HeaderLength + payload.Length;
        byte[] archive = new byte[tableOffset + WdfArchive.EntryLength];

        BinaryPrimitives.WriteUInt32LittleEndian(archive, WdfArchive.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(8), (uint)tableOffset);
        payload.CopyTo(archive.AsSpan(WdfArchive.HeaderLength));
        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(tableOffset), WdfPathHash.Compute(contentPath));
        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(tableOffset + 4), WdfArchive.HeaderLength);
        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(tableOffset + 8), (uint)payload.Length);

        return archive;
    }
}
