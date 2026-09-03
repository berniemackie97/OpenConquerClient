using System.Text;
using OpenConquer.Content.Tool.Import;
using OpenConquer.Content.Tool.Manifest;
using OpenConquer.Content.Tool.Verify;

namespace OpenConquer.Content.Tool.Tests.Verify;

public sealed class ContentSetVerifierTests
{
    [Fact]
    public void Verify_AcceptsAFreshlyImportedContentSet()
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);

        ContentManifest manifest = ContentSetVerifier.Verify(contentSet);

        Assert.Equal(5, manifest.FileCount);
    }

    [Fact]
    public void Verify_RejectsAContentSetMissingADeclaredPayload()
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);
        File.Delete(Path.Combine(contentSet, "payload", "ini", "package.ini"));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet));

        Assert.Contains("ini/package.ini", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_RejectsAPayloadFileTheManifestDoesNotDeclare()
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);
        File.WriteAllText(Path.Combine(contentSet, "payload", "ini", "Extra.ini"), "[Section]\n");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet));

        Assert.Contains("ini/Extra.ini", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_RejectsAPayloadFileWithAChangedLength()
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);
        File.AppendAllText(Path.Combine(contentSet, "payload", "ini", "package.ini"), "extra.wdf\n");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet));

        Assert.Contains("bytes; the manifest declares", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A same-length replacement is the case a length check alone would miss.
    /// </summary>
    [Fact]
    public void Verify_RejectsAPayloadFileWithChangedBytesAtTheSameLength()
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);
        string packagePath = Path.Combine(contentSet, "payload", "ini", "package.ini");
        byte[] bytes = File.ReadAllBytes(packagePath);
        bytes[0] = (byte)'D';
        File.WriteAllBytes(packagePath, bytes);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet));

        Assert.Contains("failed SHA-256 verification", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_RejectsAPayloadFileWhoseSignatureNoLongerMatches()
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);
        string logoPath = Path.Combine(contentSet, "payload", "data", "main", "Logo1.bmp");
        byte[] bytes = File.ReadAllBytes(logoPath);
        bytes[0] = (byte)'D';
        bytes[1] = (byte)'D';
        bytes[2] = (byte)'S';
        bytes[3] = (byte)' ';
        File.WriteAllBytes(logoPath, bytes);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet));

        Assert.Contains("has signature 'dds'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_RejectsAManifestWithAnUnsupportedSchemaVersion()
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);
        string manifestPath = Path.Combine(contentSet, "manifest.json");
        File.WriteAllText(
            manifestPath,
            File.ReadAllText(manifestPath, Encoding.UTF8).Replace("\"schemaVersion\": 2", "\"schemaVersion\": 3", StringComparison.Ordinal),
            Encoding.UTF8
        );

        Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet));
    }

    [Fact]
    public void Verify_RejectsAManifestSummaryThatDisagreesWithItsEntries()
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);
        string manifestPath = Path.Combine(contentSet, "manifest.json");
        File.WriteAllText(
            manifestPath,
            File.ReadAllText(manifestPath, Encoding.UTF8).Replace("\"fileCount\": 5", "\"fileCount\": 4", StringComparison.Ordinal),
            Encoding.UTF8
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet));

        Assert.Contains("summary does not match", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_RejectsAManifestWithAnInconsistentPathKey()
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);
        string manifestPath = Path.Combine(contentSet, "manifest.json");
        File.WriteAllText(
            manifestPath,
            File.ReadAllText(manifestPath, Encoding.UTF8).Replace("\"pathKey\": \"ini/package.ini\"", "\"pathKey\": \"ini/Package.ini\"", StringComparison.Ordinal),
            Encoding.UTF8
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet));

        Assert.Contains("inconsistent path key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_RejectsAManifestEntryThatEscapesThePayloadRoot()
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);
        string manifestPath = Path.Combine(contentSet, "manifest.json");
        File.WriteAllText(
            manifestPath,
            File.ReadAllText(manifestPath, Encoding.UTF8)
                .Replace("\"sourcePath\": \"ini/package.ini\"", "\"sourcePath\": \"../escape.ini\"", StringComparison.Ordinal),
            Encoding.UTF8
        );

        Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet));
    }

    [Fact]
    public void Verify_RejectsAContentSetWithoutAPayloadDirectory()
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);
        Directory.Delete(Path.Combine(contentSet, "payload"), recursive: true);

        Assert.Throws<DirectoryNotFoundException>(() => ContentSetVerifier.Verify(contentSet));
    }

    [Fact]
    public void Verify_RejectsAContentSetWithoutAManifest()
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);
        File.Delete(Path.Combine(contentSet, "manifest.json"));

        Assert.Throws<FileNotFoundException>(() => ContentSetVerifier.Verify(contentSet));
    }

    private static string ImportContentSet(TemporarySourceTree fixture)
    {
        fixture.WriteStartupSnapshot();

        string contentSet = fixture.ChildPath("content-set");
        ContentSetImporter.Import(fixture.RootPath, contentSet);

        return contentSet;
    }
}
