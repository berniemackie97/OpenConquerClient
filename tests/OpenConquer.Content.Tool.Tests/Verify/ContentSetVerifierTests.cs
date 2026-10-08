using System.Security.Cryptography;
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

        Assert.Equal(105, manifest.FileCount);
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "ani/Control.ani");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "ani/Magic.ani");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "ani/ItemMinIcon.Ani");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "ani/effect.ani");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "ini/Font.ini");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/ProgressBk.dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/interface/compose/CoverPic.dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/MagicSkillType1000.dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/ItemMinIcon/Default.dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/Pic/FireLight/01.dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/Pic/YellowLight/01.dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/MainImgMagic.dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/ImageDisable.dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/MsgDlg.dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "ini/StrRes.ini");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "ini/ProgressXp.rgn");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "ini/ProgressMp.rgn");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "ini/ProgressHp.rgn");
        Assert.DoesNotContain(manifest.Entries, static entry => entry.SourcePath == "data/Pic/CustomGlow/01.dds");
        Assert.DoesNotContain(manifest.Entries, static entry => string.Equals(entry.SourcePath, "data/main3/skill38.dds", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Verify_RejectsAContentSetMissingADeclaredPayload()
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);
        File.Delete(Path.Combine(contentSet, "payload", "ini", "info.ini"));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet));

        Assert.Contains("ini/info.ini", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_RejectsAPayloadFileTheManifestDoesNotDeclare()
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);
        File.WriteAllText(Path.Combine(contentSet, "payload", "ini", "extra.ini"), "[Section]\n");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet));

        Assert.Contains("ini/extra.ini", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_RejectsManifestAndPayloadThatBothContainAssetOutsideImplementedClosure()
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);
        string extraSourcePath = "data/main/unused.bin";
        string extraFilePath = Path.Combine(contentSet, "payload", "data", "main", "unused.bin");

        File.WriteAllBytes(extraFilePath, [1, 2, 3, 4]);

        ContentManifest manifest = ReadManifest(contentSet);
        ContentManifestEntry extraEntry = CreateManifestEntry(extraSourcePath, extraFilePath);

        RewriteManifest(contentSet, new ContentManifest(manifest.ClientVersion, manifest.VersionMarkerSha256,
            manifest.Entries.Append(extraEntry).OrderBy(static entry => entry.SourcePath, StringComparer.Ordinal).ToArray()));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet));

        Assert.Contains("outside implemented closure", exception.Message, StringComparison.Ordinal);
        Assert.Contains(extraSourcePath, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_RejectsManifestAndPayloadThatBothOmitRequiredHudClosureAsset()
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);
        const string omittedSourcePath = "data/main/QueryBtn.dds";

        File.Delete(Path.Combine(contentSet, "payload", "data", "main", "querybtn.dds"));

        ContentManifest manifest = ReadManifest(contentSet);

        RewriteManifest(contentSet, new ContentManifest(manifest.ClientVersion, manifest.VersionMarkerSha256,
            manifest.Entries.Where(entry => !string.Equals(entry.SourcePath, omittedSourcePath, StringComparison.Ordinal)).ToArray()));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet));

        Assert.Contains("missing from manifest", exception.Message, StringComparison.Ordinal);
        Assert.Contains(omittedSourcePath, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_RejectsManifestAndPayloadThatBothOmitQuickbarClosureAsset()
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);
        const string omittedSourcePath = "data/interface/compose/CoverPic.dds";

        File.Delete(Path.Combine(contentSet, "payload", "data", "interface", "compose", "coverpic.dds"));

        ContentManifest manifest = ReadManifest(contentSet);

        RewriteManifest(contentSet, new ContentManifest(manifest.ClientVersion, manifest.VersionMarkerSha256,
            manifest.Entries.Where(entry => !string.Equals(entry.SourcePath, omittedSourcePath, StringComparison.Ordinal)).ToArray()));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet));

        Assert.Contains("missing from manifest", exception.Message, StringComparison.Ordinal);
        Assert.Contains(omittedSourcePath, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_RejectsManifestAndPayloadThatBothOmitSelectedSkillClosureAsset()
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);
        const string omittedSourcePath = "data/main/MainImgMagic.dds";

        File.Delete(Path.Combine(contentSet, "payload", "data", "main", "mainimgmagic.dds"));

        ContentManifest manifest = ReadManifest(contentSet);

        RewriteManifest(contentSet, new ContentManifest(manifest.ClientVersion, manifest.VersionMarkerSha256,
            manifest.Entries.Where(entry => !string.Equals(entry.SourcePath, omittedSourcePath, StringComparison.Ordinal)).ToArray()));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet));

        Assert.Contains("missing from manifest", exception.Message, StringComparison.Ordinal);
        Assert.Contains(omittedSourcePath, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("data/main/MsgDlg.dds", "data/main/msgdlg.dds")]
    [InlineData("ini/StrRes.ini", "ini/strres.ini")]
    [InlineData("ini/ProgressXp.rgn", "ini/progressxp.rgn")]
    [InlineData("ini/ProgressMp.rgn", "ini/progressmp.rgn")]
    [InlineData("ini/ProgressHp.rgn", "ini/progresshp.rgn")]
    public void Verify_RejectsManifestAndPayloadThatBothOmitRequiredStatusHintAsset(string omittedSourcePath, string payloadPath)
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);
        File.Delete(Path.Combine(contentSet, "payload", payloadPath.Replace('/', Path.DirectorySeparatorChar)));

        ContentManifest manifest = ReadManifest(contentSet);

        RewriteManifest(contentSet, new ContentManifest(manifest.ClientVersion, manifest.VersionMarkerSha256,
            manifest.Entries.Where(entry => !string.Equals(entry.SourcePath, omittedSourcePath, StringComparison.Ordinal)).ToArray()));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet));

        Assert.Contains("missing from manifest", exception.Message, StringComparison.Ordinal);
        Assert.Contains(omittedSourcePath, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_RejectsAPayloadFileWithAChangedLength()
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);
        File.AppendAllText(Path.Combine(contentSet, "payload", "ini", "info.ini"), "\nextra\n");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet));

        Assert.Contains("bytes; the manifest declares", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_RejectsAPayloadFileWithChangedBytesAtTheSameLength()
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);
        string infoPath = Path.Combine(contentSet, "payload", "ini", "info.ini");
        byte[] bytes = File.ReadAllBytes(infoPath);

        bytes[0] ^= 0x01;
        File.WriteAllBytes(infoPath, bytes);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet));

        Assert.Contains("failed SHA-256 verification", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_RejectsAPayloadFileWhoseSignatureNoLongerMatches()
    {
        using TemporarySourceTree fixture = new();

        string contentSet = ImportContentSet(fixture);
        string logoPath = Path.Combine(contentSet, "payload", "data", "main", "logo1.bmp");
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
        string manifestPath = Path.Combine
