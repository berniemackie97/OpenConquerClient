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
            "data/interface/Style01/Action/MissionBtnClick.dds",
            "data/interface/Style01/Action/MissionBtnEmboss.dds",
            "data/interface/Style01/Action/MissionBtnNormal.dds",
            "data/main/ChatBtn.dds",
            "data/main/ChatBtnClick.dds",
            "data/main/GoodBtn.dds",
            "data/main/GoodBtnClick.dds",
            "data/main/GroupBtn.dds",
            "data/main/GroupBtnClick.dds",
            "data/main/LevWordBtn.dds",
            "data/main/LevWordBtnClick.dds",
            "data/main/Logo1.bmp",
            "data/main/Logo2.bmp",
            "data/main/MainDialog2.dds",
            "data/main/OrganiseBtnClick.dds",
            "data/main/OrganiseBtnEmboss.dds",
            "data/main/OrganiseBtnNormal.dds",
            "data/main/OrganiseBtnUnClick.dds",
            "data/main/PkArre.dds",
            "data/main/PkArreClick.dds",
            "data/main/PkFree.dds",
            "data/main/PkFreeClick.dds",
            "data/main/PkGroup.dds",
            "data/main/PkGroupClick.dds",
            "data/main/PkSafe.dds",
            "data/main/PkSafeClick.dds",
            "data/main/ProgressBk.dds",
            "data/main/ProgressForce.dds",
            "data/main/ProgressForce2.dds",
            "data/main/ProgressForce2a.dds",
            "data/main/ProgressForceA.dds",
            "data/main/ProgressHP.dds",
            "data/main/ProgressHPA.dds",
            "data/main/ProgressHPH.dds",
            "data/main/ProgressMP.dds",
            "data/main/ProgressMPA.dds",
            "data/main/ProgressMPH.dds",
            "data/main/ProgressPower.dds",
            "data/main/ProgressPowerH.dds",
            "data/main/QueryBtn.dds",
            "data/main/QueryBtnClick.dds",
            "data/main/SetBtn.dds",
            "data/main/SetBtnClick.dds",
            "data/main/SkillBtn.dds",
            "data/main/SkillBtnClick.dds",
            "data/main/SkillBtnL.dds",
            "data/main/mainDialog1.dds",
            "ini/GameSetUp.ini",
            "ini/info.ini",
        ], manifest.Entries.Select(static entry => entry.SourcePath));

        Assert.DoesNotContain(manifest.Entries, static entry => string.Equals(entry.SourcePath, "data.wdf", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(manifest.Entries, static entry => string.Equals(entry.SourcePath, "Server.dat", StringComparison.OrdinalIgnoreCase));

        string payloadRoot = Path.Combine(destination, "payload");
        string[] payloadFiles = Directory.EnumerateFiles(payloadRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(payloadRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

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
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/ProgressHP.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/ProgressHPA.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/ProgressHPH.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/ProgressMP.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/ProgressMPA.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/ProgressMPH.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/ProgressPower.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/ProgressPowerH.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/ProgressForce.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/ProgressForceA.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/ProgressForce2.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/ProgressForce2a.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/QueryBtn.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/QueryBtnClick.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/LevWordBtn.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/LevWordBtnClick.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/GoodBtn.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/GoodBtnClick.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/SetBtn.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/SetBtnClick.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/interface/Style01/Action/MissionBtnNormal.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/interface/Style01/Action/MissionBtnClick.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/interface/Style01/Action/MissionBtnEmboss.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/ChatBtn.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/ChatBtnClick.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/GroupBtn.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/GroupBtnClick.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/PkFree.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/PkFreeClick.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/PkSafe.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/PkSafeClick.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/PkGroup.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/PkGroupClick.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/PkArre.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/PkArreClick.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/OrganiseBtnNormal.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/OrganiseBtnClick.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/OrganiseBtnUnClick.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/OrganiseBtnEmboss.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/SkillBtn.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/SkillBtnClick.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/SkillBtnL.dds" && entry.Signature == "dds");

        Assert.Equal("DDS ProgressBk", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "ProgressBk.dds"))));
        Assert.Equal("DDS mainDialog1", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "mainDialog1.dds"))));
        Assert.Equal("DDS MainDialog2 loose", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "MainDialog2.dds"))));
        Assert.Equal("DDS ProgressHP", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "ProgressHP.dds"))));
        Assert.Equal("DDS ProgressPower", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "ProgressPower.dds"))));
        Assert.Equal("DDS ProgressPowerH", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "ProgressPowerH.dds"))));
        Assert.Equal("DDS ProgressForceA", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "ProgressForceA.dds"))));
        Assert.Equal("DDS ProgressForce2 loose", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "ProgressForce2.dds"))));
        Assert.Equal("DDS ProgressForce2a loose", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "ProgressForce2a.dds"))));
        Assert.Equal("DDS QueryBtn loose", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "QueryBtn.dds"))));
        Assert.Equal("DDS MissionBtnNormal loose", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "interface", "Style01", "Action", "MissionBtnNormal.dds"))));
        Assert.Equal("DDS PkFree loose", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "PkFree.dds"))));
        Assert.Equal("DDS OrganiseBtnNormal loose", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "OrganiseBtnNormal.dds"))));
        Assert.Equal("DDS OrganiseBtnClick loose", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "OrganiseBtnClick.dds"))));
        Assert.Equal("DDS OrganiseBtnUnClick loose", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "OrganiseBtnUnClick.dds"))));
        Assert.Equal("DDS OrganiseBtnEmboss loose", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "OrganiseBtnEmboss.dds"))));
        Assert.Equal("DDS SkillBtnL loose", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "SkillBtnL.dds"))));
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
        Assert.Contains("data/main/ProgressHP.dds", manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.Contains("data/main/ProgressPower.dds", manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.Contains("data/main/ProgressPowerH.dds", manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.Contains("data/main/ProgressForce2a.dds", manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.Contains("data/main/MainDialog2.dds", manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.Contains("data/main/QueryBtn.dds", manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.Contains("data/interface/Style01/Action/MissionBtnNormal.dds", manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.Contains("data/main/PkFree.dds", manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.Contains("data/main/OrganiseBtnNormal.dds", manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.Contains("data/main/OrganiseBtnClick.dds", manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.Contains("data/main/OrganiseBtnUnClick.dds", manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.Contains("data/main/OrganiseBtnEmboss.dds", manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.Contains("data/main/SkillBtnL.dds", manifest.Entries.Select(static entry => entry.SourcePath));
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
        ContentManifestEntry progressPower = manifest.Entries.Single(static entry => entry.SourcePath == "data/main/ProgressPower.dds");
        ContentManifestEntry progressPowerHighlight = manifest.Entries.Single(static entry => entry.SourcePath == "data/main/ProgressPowerH.dds");
        ContentManifestEntry progressForce2Alternate = manifest.Entries.Single(static entry => entry.SourcePath == "data/main/ProgressForce2a.dds");
        ContentManifestEntry queryNormal = manifest.Entries.Single(static entry => entry.SourcePath == "data/main/QueryBtn.dds");
        ContentManifestEntry missionNormal = manifest.Entries.Single(static entry => entry.SourcePath == "data/interface/Style01/Action/MissionBtnNormal.dds");
        ContentManifestEntry pkFree = manifest.Entries.Single(static entry => entry.SourcePath == "data/main/PkFree.dds");
        ContentManifestEntry organiseNormal = manifest.Entries.Single(static entry => entry.SourcePath == "data/main/OrganiseBtnNormal.dds");
        ContentManifestEntry skillNormal = manifest.Entries.Single(static entry => entry.SourcePath == "data/main/SkillBtn.dds");

        Assert.Equal(50, manifest.FileCount);
        Assert.Equal("bmp", logo.Signature);
        Assert.Equal(TestBitmap.CreateTwoByTwo().Length, logo.Length);
        Assert.Equal(64, logo.Sha256.Length);
        Assert.Equal("data/main/logo1.bmp", logo.PathKey);

        Assert.Equal("dds", progressBackground.Signature);
        Assert.Equal("data/main/progressbk.dds", progressBackground.PathKey);
        Assert.Equal(64, progressBackground.Sha256.Length);

        Assert.Equal("dds", progressPower.Signature);
        Assert.Equal("data/main/progresspower.dds", progressPower.PathKey);
        Assert.Equal(64, progressPower.Sha256.Length);

        Assert.Equal("dds", progressPowerHighlight.Signature);
        Assert.Equal("data/main/progresspowerh.dds", progressPowerHighlight.PathKey);
        Assert.Equal(64, progressPowerHighlight.Sha256.Length);

        Assert.Equal("dds", progressForce2Alternate.Signature);
        Assert.Equal("data/main/progressforce2a.dds", progressForce2Alternate.PathKey);
        Assert.Equal(64, progressForce2Alternate.Sha256.Length);

        Assert.Equal("dds", queryNormal.Signature);
        Assert.Equal("data/main/querybtn.dds", queryNormal.PathKey);
        Assert.Equal(64, queryNormal.Sha256.Length);

        Assert.Equal("dds", missionNormal.Signature);
        Assert.Equal("data/interface/style01/action/missionbtnnormal.dds", missionNormal.PathKey);
        Assert.Equal(64, missionNormal.Sha256.Length);

        Assert.Equal("dds", pkFree.Signature);
        Assert.Equal("data/main/pkfree.dds", pkFree.PathKey);
        Assert.Equal(64, pkFree.Sha256.Length);

        Assert.Equal("dds", organiseNormal.Signature);
        Assert.Equal("data/main/organisebtnnormal.dds", organiseNormal.PathKey);
        Assert.Equal(64, organiseNormal.Sha256.Length);

        Assert.Equal("dds", skillNormal.Signature);
        Assert.Equal("data/main/skillbtn.dds", skillNormal.PathKey);
        Assert.Equal(64, skillNormal.Sha256.Length);

        Assert.Equal(manifest.Entries.Sum(static entry => entry.Length), manifest.Length);
    }

    [Fact]
    public void Import_IncludesUnusedNativeStaminaAlternateFrames()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destinationParent = new();

        source.WriteStartupSnapshot();

        ContentManifest manifest = ContentSetImporter.Import(source.RootPath, destinationParent.ChildPath("set"));

        Assert.Contains("data/main/ProgressForceA.dds", manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.Contains("data/main/ProgressForce2a.dds", manifest.Entries.Select(static entry => entry.SourcePath));
    }

    [Fact]
    public void Import_IncludesEveryNativeActionButtonFrame()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destinationParent = new();

        source.WriteStartupSnapshot();

        ContentManifest manifest = ContentSetImporter.Import(source.RootPath, destinationParent.ChildPath("set"));
        string[] paths = manifest.Entries.Select(static entry => entry.SourcePath).ToArray();

        Assert.Contains("data/main/QueryBtn.dds", paths);
        Assert.Contains("data/main/QueryBtnClick.dds", paths);
        Assert.Contains("data/main/LevWordBtn.dds", paths);
        Assert.Contains("data/main/LevWordBtnClick.dds", paths);
        Assert.Contains("data/main/GoodBtn.dds", paths);
        Assert.Contains("data/main/GoodBtnClick.dds", paths);
        Assert.Contains("data/main/SetBtn.dds", paths);
        Assert.Contains("data/main/SetBtnClick.dds", paths);
        Assert.Contains("data/interface/Style01/Action/MissionBtnNormal.dds", paths);
        Assert.Contains("data/interface/Style01/Action/MissionBtnClick.dds", paths);
        Assert.Contains("data/interface/Style01/Action/MissionBtnEmboss.dds", paths);
        Assert.Contains("data/main/ChatBtn.dds", paths);
        Assert.Contains("data/main/ChatBtnClick.dds", paths);
        Assert.Contains("data/main/GroupBtn.dds", paths);
        Assert.Contains("data/main/GroupBtnClick.dds", paths);
        Assert.Contains("data/main/PkFree.dds", paths);
        Assert.Contains("data/main/PkFreeClick.dds", paths);
        Assert.Contains("data/main/PkSafe.dds", paths);
        Assert.Contains("data/main/PkSafeClick.dds", paths);
        Assert.Contains("data/main/PkGroup.dds", paths);
        Assert.Contains("data/main/PkGroupClick.dds", paths);
        Assert.Contains("data/main/PkArre.dds", paths);
        Assert.Contains("data/main/PkArreClick.dds", paths);
        Assert.Contains("data/main/OrganiseBtnNormal.dds", paths);
        Assert.Contains("data/main/OrganiseBtnClick.dds", paths);
        Assert.Contains("data/main/OrganiseBtnUnClick.dds", paths);
        Assert.Contains("data/main/OrganiseBtnEmboss.dds", paths);
        Assert.Contains("data/main/SkillBtn.dds", paths);
        Assert.Contains("data/main/SkillBtnClick.dds", paths);
        Assert.Contains("data/main/SkillBtnL.dds", paths);
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
