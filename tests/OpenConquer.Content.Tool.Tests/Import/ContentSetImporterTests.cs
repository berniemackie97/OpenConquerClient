using System.Text;
using OpenConquer.Content.Tool.Import;
using OpenConquer.Content.Tool.Manifest;

namespace OpenConquer.Content.Tool.Tests.Import;

public sealed class ContentSetImporterTests
{
    [Fact]
    public void Import_WritesOnlyTheResolvedClosure()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destinationParent = new();

        source.WriteStartupSnapshot();
        source.WriteText("ini/UnrelatedCatalog.ini", "[Section]\nKey=Value\n");
        source.WriteBytes("data/main/UnrelatedTexture.bmp", TestBitmap.CreateTwoByTwo());

        string destination = destinationParent.ChildPath("set");
        ContentManifest manifest = ContentSetImporter.Import(source.RootPath, destination);

        Assert.Equal(
        [
            "ani/Control.ani",
            "data/main/Logo1.bmp",
            "data/main/Logo2.bmp",
            "data/main/MainDialog2.dds",
            "data/main/ProgressBk.dds",
            "data/main/mainDialog1.dds",
            "ini/GameSetUp.ini",
            "ini/info.ini",
            "ini/package.ini",
        ], manifest.Entries.Select(static entry => entry.SourcePath));

        Assert.DoesNotContain(manifest.Entries, static entry => string.Equals(entry.SourcePath, "data.wdf", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(manifest.Entries, static entry => string.Equals(entry.SourcePath, "Server.dat", StringComparison.OrdinalIgnoreCase));

        string payloadRoot = Path.Combine(destination, "payload");
        string[] payloadFiles = Directory.EnumerateFiles(payloadRoot, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(payloadRoot, path).Replace('\\', '/')).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(manifest.Entries.Select(static entry => entry.SourcePath), payloadFiles);
    }

    [Fact]
    public void Import_MaterializesPackagedHudFramesAndPreservesLooseOverrideCasing()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destinationParent = new();

        source.WriteStartupSnapshot();

        string destination = destinationParent.ChildPath("set");
        ContentManifest manifest = ContentSetImporter.Import(source.RootPath, destination);
        string payloadRoot = Path.Combine(destination, "payload");

        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/ProgressBk.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/mainDialog1.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/MainDialog2.dds" && entry.Signature == "dds");

        Assert.Equal("DDS ProgressBk", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "ProgressBk.dds"))));
        Assert.Equal("DDS mainDialog1", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "mainDialog1.dds"))));
        Assert.Equal("DDS MainDialog2 loose", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "MainDialog2.dds"))));
        Assert.False(File.Exists(Path.Combine(payloadRoot, "data.wdf")));
    }

    [Fact]
    public void Import_FollowsTheDeclaredBackgroundFormat()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destinationParent = new();

        source.WriteStartupSnapshot(backgroundFormat: "data/main/Splash%02d.bmp");
        source.WriteBytes("data/main/Splash01.bmp", TestBitmap.CreateTwoByTwo());
        source.WriteBytes("data/main/Splash02.bmp", TestBitmap.CreateTwoByTwo());

        ContentManifest manifest = ContentSetImporter.Import(source.RootPath, destinationParent.ChildPath("set"));

        Assert.Contains("data/main/Splash01.bmp", manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.Contains("data/main/Splash02.bmp", manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.DoesNotContain("data/main/Logo1.bmp", manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.Contains("data/main/ProgressBk.dds", manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.Contains("data/main/MainDialog2.dds", manifest.Entries.Select(static entry => entry.SourcePath));
    }

    [Fact]
    public void Import_ProducesAByteIdenticalManifestOnRepeatedRuns()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destinationParent = new();

        source.WriteStartupSnapshot();

        ContentSetImporter.Import(source.RootPath, destinationParent.ChildPath("first"));
        ContentSetImporter.Import(source.RootPath, destinationParent.ChildPath("second"));

        Assert.Equal(File.ReadAllBytes(Path.Combine(destinationParent.ChildPath("first"), "manifest.json")), File.ReadAllBytes(Path.Combine(destinationParent.ChildPath("second"), "manifest.json")));
    }

    [Fact]
    public void Import_WritesTheManifestWithLineFeedsAndATrailingNewline()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destinationParent = new();

        source.WriteStartupSnapshot();
        ContentSetImporter.Import(source.RootPath, destinationParent.ChildPath("set"));

        byte[] manifestBytes = File.ReadAllBytes(Path.Combine(destinationParent.ChildPath("set"), "manifest.json"));

        Assert.DoesNotContain((byte)'\r', manifestBytes);
        Assert.Equal((byte)'\n', manifestBytes[^1]);
        Assert.StartsWith("{\n  \"schemaVersion\": 2,", Encoding.UTF8.GetString(manifestBytes), StringComparison.Ordinal);
    }

    [Fact]
    public void Import_RecordsLengthHashAndSignatureForEveryEntry()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destinationParent = new();

        source.WriteStartupSnapshot();

        ContentManifest manifest = ContentSetImporter.Import(source.RootPath, destinationParent.ChildPath("set"));
        ContentManifestEntry logo = manifest.Entries.Single(static entry => entry.SourcePath == "data/main/Logo1.bmp");
        ContentManifestEntry progressBackground = manifest.Entries.Single(static entry => entry.SourcePath == "data/main/ProgressBk.dds");

        Assert.Equal(9, manifest.FileCount);
        Assert.Equal("bmp", logo.Signature);
        Assert.Equal(TestBitmap.CreateTwoByTwo().Length, logo.Length);
        Assert.Equal(64, logo.Sha256.Length);
        Assert.Equal("data/main/logo1.bmp", logo.PathKey);

        Assert.Equal("dds", progressBackground.Signature);
        Assert.Equal("data/main/progressbk.dds", progressBackground.PathKey);
        Assert.Equal(64, progressBackground.Sha256.Length);
        Assert.Equal(manifest.Entries.Sum(static entry => entry.Length), manifest.Length);
    }

    [Fact]
    public void Import_RejectsASourceWithoutTheExpectedVersionMarker()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destinationParent = new();

        source.WriteStartupSnapshot();
        source.WriteText("version.dat", "9999");

        Assert.Throws<InvalidDataException>(() => ContentSetImporter.Import(source.RootPath, destinationParent.ChildPath("set")));
    }

    [Fact]
    public void Import_RejectsASourceWithNoVersionMarker()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destinationParent = new();

        source.WriteText("ini/GameSetUp.ini", "[ScreenMode]\nScreenModeRecord=2\n");

        Assert.Throws<FileNotFoundException>(() => ContentSetImporter.Import(source.RootPath, destinationParent.ChildPath("set")));
    }

    [Fact]
    public void Import_FailsWhenAClosureFileIsMissingFromTheSource()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destinationParent = new();

        source.WriteStartupSnapshot();
        File.Delete(source.ChildPath("ani/Control.ani"));

        Assert.Throws<FileNotFoundException>(() => ContentSetImporter.Import(source.RootPath, destinationParent.ChildPath("set")));
    }

    [Fact]
    public void Import_LeavesNoStagingDirectoryBehindWhenItFails()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destinationParent = new();

        source.WriteStartupSnapshot();
        File.Delete(source.ChildPath("data.wdf"));

        Assert.Throws<FileNotFoundException>(() => ContentSetImporter.Import(source.RootPath, destinationParent.ChildPath("set")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(destinationParent.RootPath));
    }

    [Fact]
    public void Import_RefusesToOverwriteAnExistingDestination()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destinationParent = new();

        source.WriteStartupSnapshot();
        Directory.CreateDirectory(destinationParent.ChildPath("set"));

        Assert.Throws<IOException>(() => ContentSetImporter.Import(source.RootPath, destinationParent.ChildPath("set")));
    }
}
