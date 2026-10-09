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
        string[] expectedPaths = ExpectedImportedPaths();

        Assert.Equal(108, expectedPaths.Length);
        Assert.Equal(expectedPaths, manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.DoesNotContain(manifest.Entries, static entry => string.Equals(entry.SourcePath, "data.wdf", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(manifest.Entries, static entry => string.Equals(entry.SourcePath, "Server.dat", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(manifest.Entries, static entry => entry.SourcePath == "data/main/UnusedControl.dds");
        Assert.DoesNotContain(manifest.Entries, static entry => entry.SourcePath == "data/main/MagicOther.dds");
        Assert.DoesNotContain(manifest.Entries, static entry => entry.SourcePath == "data/ItemMinIcon/Preview.dds");
        Assert.DoesNotContain(manifest.Entries, static entry => entry.SourcePath == "data/Pic/CustomGlow/01.dds");
        Assert.DoesNotContain(manifest.Entries, static entry => string.Equals(entry.SourcePath, "data/main3/skill38.dds", StringComparison.OrdinalIgnoreCase));

        string payloadRoot = Path.Combine(destination, "payload");
        string[] payloadFiles = Directory.EnumerateFiles(payloadRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(payloadRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(manifest.Entries.Select(static entry => entry.PathKey).Order(StringComparer.Ordinal), payloadFiles);
        Assert.All(payloadFiles, static path => Assert.Equal(path.ToLowerInvariant(), path));
    }

    [Fact]
    public void Import_MaterializesImplementedHudAssets()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destinationParent = new();

        source.WriteStartupSnapshot();

        string destination = destinationParent.ChildPath("set");
        ContentManifest manifest = ContentSetImporter.Import(source.RootPath, destination);
        string payloadRoot = Path.Combine(destination, "payload");

        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "ini/Font.ini");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/ProgressBk.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/mainDialog1.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/MainDialog2.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "ani/Magic.ani");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "ani/ItemMinIcon.Ani");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "ani/effect.ani");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/interface/compose/CoverPic.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/MagicSkillType1000.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/ItemMinIcon/Default.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/Pic/FireLight/01.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/MainImgMagic.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/ImageDisable.dds" && entry.Signature == "dds");
        Assert.Contains(manifest.Entries, static entry => entry.SourcePath == "data/main/MsgDlg.dds" && entry.Signature == "dds");

        foreach (string path in ExpectedStatusHintPaths())
        {
            Assert.Contains(manifest.Entries, entry => entry.SourcePath == path);
        }

        Assert.Equal("Arial 12", Encoding.Latin1.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "ini", "font.ini"))));
        Assert.Equal("DDS ProgressBk", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "progressbk.dds"))));
        Assert.Equal("DDS mainDialog1", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "maindialog1.dds"))));
        Assert.Equal("DDS MainDialog2 loose", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "maindialog2.dds"))));
        Assert.Equal("DDS MagicSkillType1000", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "magicskilltype1000.dds"))));
        Assert.Equal("DDS ItemDefault", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "itemminicon", "default.dds"))));
        Assert.Equal("DDS MainImgMagic", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "mainimgmagic.dds"))));
        Assert.Equal("DDS ImageDisable", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "imagedisable.dds"))));
        Assert.Equal("DDS MsgDlg", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(payloadRoot, "data", "main", "msgdlg.dds"))));
        Assert.Contains("10070=Walk/Run", File.ReadAllText(Path.Combine(payloadRoot, "ini", "strres.ini")), StringComparison.Ordinal);

        Assert.True(File.Exists(Path.Combine(payloadRoot, "ini", "magictype.dat")));
        Assert.True(File.Exists(Path.Combine(payloadRoot, "ini", "magiceffect.ini")));
        Assert.True(File.Exists(Path.Combine(payloadRoot, "ini", "subprofessioninfo.ini")));
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
        Assert.Contains("data/interface/compose/CoverPic.dds", manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.Contains("ani/Magic.ani", manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.Contains("data/main/MainImgMagic.dds", manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.Contains("data/main/ImageDisable.dds", manifest.Entries.Select(static entry => entry.SourcePath));
        Assert.Contains("data/main/MsgDlg.dds", manifest.Entries.Select(static entry => entry.SourcePath));
    }

    [Fact]
    public void Import_ProducesAByteIdenticalManifestOnRepeatedRuns()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destinationParent = new();

        source.WriteStartupSnapshot();

        ContentSetImporter.Import(source.RootPath, destinationParent.ChildPath("first"));
        ContentSetImporter.Import(source.RootPath, destinationParent.ChildPath("second"));

        Assert.Equal(
            File.ReadAllBytes(Path.Combine(destinationParent.ChildPath("first"), "manifest.json")),
            File.ReadAllBytes(Path.Combine(destinationParent.ChildPath("second"), "manifest.json")));
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
        ContentManifestEntry quickbarCover = manifest.Entries.Single(static entry => entry.SourcePath == "data/interface/compose/CoverPic.dds");
        ContentManifestEntry magic = manifest.Entries.Single(static entry => entry.SourcePath == "data/main/MagicSkillType1000.dds");
        ContentManifestEntry selectedSkill = manifest.Entries.Single(static entry => entry.SourcePath == "data/main/MainImgMagic.dds");
        ContentManifestEntry statusHint = manifest.Entries.Single(static entry => entry.SourcePath == "data/main/MsgDlg.dds");

        Assert.Equal(108, manifest.FileCount);

        Assert.Equal("bmp", logo.Signature);
        Assert.Equal(TestBitmap.CreateTwoByTwo().Length, logo.Length);
        Assert.Equal(64, logo.Sha256.Length);
        Assert.Equal("data/main/logo1.bmp", logo.PathKey);

        Assert.Equal("dds", progressBackground.Signature);
        Assert.Equal("data/main/progressbk.dds", progressBackground.PathKey);
        Assert.Equal(64, progressBackground.Sha256.Length);

        Assert.Equal("dds", quickbarCover.Signature);
        Assert.Equal("data/interface/compose/coverpic.dds", quickbarCover.PathKey);
        Assert.Equal(64, quickbarCover.Sha256.Length);

        Assert.Equal("dds", magic.Signature);
        Assert.Equal("data/main/magicskilltype1000.dds", magic.PathKey);
        Assert.Equal(64, magic.Sha256.Length);

        Assert.Equal("dds", selectedSkill.Signature);
        Assert.Equal("data/main/mainimgmagic.dds", selectedSkill.PathKey);
        Assert.Equal(64, selectedSkill.Sha256.Length);

        Assert.Equal("dds", statusHint.Signature);
        Assert.Equal("data/main/msgdlg.dds", statusHint.PathKey);
        Assert.Equal(64, statusHint.Sha256.Length);

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

        string[] paths = ContentSetImporter.Import(source.RootPath, destinationParent.ChildPath("set")).Entries
            .Select(static entry => entry.SourcePath)
            .ToArray();

        foreach (string path in new[]
        {
            "data/main/QueryBtn.dds", "data/main/QueryBtnClick.dds",
            "data/main/LevWordBtn.dds", "data/main/LevWordBtnClick.dds",
            "data/main/GoodBtn.dds", "data/main/GoodBtnClick.dds",
            "data/main/SetBtn.dds", "data/main/SetBtnClick.dds",
            "data/interface/Style01/Action/MissionBtnNormal.dds",
            "data/interface/Style01/Action/MissionBtnClick.dds",
            "data/interface/Style01/Action/MissionBtnEmboss.dds",
            "data/main/ChatBtn.dds", "data/main/ChatBtnClick.dds",
            "data/main/GroupBtn.dds", "data/main/GroupBtnClick.dds",
            "data/main/PkFree.dds", "data/main/PkFreeClick.dds",
            "data/main/PkSafe.dds", "data/main/PkSafeClick.dds",
            "data/main/PkGroup.dds", "data/main/PkGroupClick.dds",
            "data/main/PkArre.dds", "data/main/PkArreClick.dds",
            "data/main/OrganiseBtnNormal.dds", "data/main/OrganiseBtnClick.dds",
            "data/main/OrganiseBtnUnClick.dds", "data/main/OrganiseBtnEmboss.dds",
            "data/main/SkillBtn.dds", "data/main/SkillBtnClick.dds", "data/main/SkillBtnL.dds",
        })
        {
            Assert.Contains(path, paths);
        }
    }

    [Fact]
    public void Import_IncludesEverySyntheticQuickbarFamily()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destinationParent = new();

        source.WriteStartupSnapshot();

        string[] paths = ContentSetImporter.Import(source.RootPath, destinationParent.ChildPath("set")).Entries
            .Select(static entry => entry.SourcePath)
            .ToArray();

        foreach (string path in ExpectedQuickbarPaths())
        {
            Assert.Contains(path, paths);
        }
    }

    [Fact]
    public void Import_IncludesSelectedSkillAssets()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destinationParent = new();

        source.WriteStartupSnapshot();

        string[] paths = ContentSetImporter.Import(source.RootPath, destinationParent.ChildPath("set")).Entries
            .Select(static entry => entry.SourcePath)
            .ToArray();

        foreach (string path in ExpectedSelectedSkillPaths())
        {
            Assert.Contains(path, paths);
        }
    }

    [Fact]
    public void Import_IncludesStatusHintAssets()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destinationParent = new();

        source.WriteStartupSnapshot();

        string[] paths = ContentSetImporter.Import(source.RootPath, destinationParent.ChildPath("set")).Entries
            .Select(static entry => entry.SourcePath)
            .ToArray();

        foreach (string path in ExpectedStatusHintPaths())
        {
            Assert.Contains(path, paths);
        }
    }

    [Fact]
    public void Import_FailsWhenAnyNewRequiredLooseMagicDependencyIsMissing()
    {
        foreach (string relativePath in new[]
        {
            "ini/MagicType.dat",
            "ini/MagicEffect.ini",
            "ini/SubProfessionInfo.ini",
        })
        {
            using TemporarySourceTree source = new();
            using TemporarySourceTree destinationParent = new();

            source.WriteStartupSnapshot();
            File.Delete(source.ChildPath(relativePath));

            Assert.Throws<FileNotFoundException>(() =>
                ContentSetImporter.Import(source.RootPath, destinationParent.ChildPath("set")));
        }
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
    public void Import_FailsWhenRequiredStatusHintStringsAreMissing()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destinationParent = new();

        source.WriteStartupSnapshot();
        File.Delete(source.ChildPath("ini/StrRes.ini"));

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

    private static string[] ExpectedImportedPaths()
    {
        string[] basePaths =
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
            "data/main/MapChk1.dds",
            "data/main/MapChk2.dds",
            "data/main/NpcEquip.dds",
            "data/main/NpcEquipClick.dds",
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
            "data/main/RunChk1.dds",
            "data/main/RunChk2.dds",
            "data/main/ScreenMoveChk1.dds",
            "data/main/ScreenMoveChk2.dds",
            "data/main/SetBtn.dds",
            "data/main/SetBtnClick.dds",
            "data/main/SkillBtn.dds",
            "data/main/SkillBtnClick.dds",
            "data/main/SkillBtnL.dds",
            "data/main/mainDialog1.dds",
            "ini/Font.ini",
            "ini/GameSetUp.ini",
            "ini/info.ini",
        ];

        return basePaths
            .Concat(ExpectedQuickbarPaths())
            .Concat(ExpectedSelectedSkillPaths())
            .Concat(ExpectedStatusHintPaths())
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> ExpectedQuickbarPaths()
    {
        yield return "ani/Magic.ani";
        yield return "ani/ItemMinIcon.Ani";
        yield return "ani/effect.ani";
        yield return "data/interface/compose/CoverPic.dds";
        yield return "data/main/UsemainbBtnNormal.dds";
        yield return "data/main/SwapmainbBtnNormal.dds";
        yield return "data/interface/Style01/Equip/Num/AddPic.dds";

        for (int digit = 0; digit <= 9; digit++)
        {
            yield return $"data/main/Num{digit}Pic.dds";
            yield return $"data/interface/Style01/Equip/Num/{digit}.dds";
        }

        yield return "data/main/Act1.dds";
        yield return "data/interface/Style01/Action/Dance2BtnNormal.dds";
        yield return "data/main/MagicSkillType1000.dds";
        yield return "data/main/XpSkillType2000.dds";
        yield return "data/ItemMinIcon/Default.dds";
        yield return "data/ItemMinIcon/100.dds";
        yield return "data/Pic/FireLight/01.dds";
        yield return "data/Pic/FireLight/02.dds";
        yield return "data/Pic/RedLight/01.dds";
        yield return "data/Pic/BlueLight/01.dds";
        yield return "data/Pic/RoyalBlueLight/01.dds";
        yield return "data/Pic/YellowLight/01.dds";
    }

    private static IEnumerable<string> ExpectedSelectedSkillPaths()
    {
        yield return "data/main/ImageDisable.dds";
        yield return "data/main/MainImgMagic.dds";
    }

    private static IEnumerable<string> ExpectedStatusHintPaths()
    {
        yield return "data/main/MsgDlg.dds";
        yield return "ini/StrRes.ini";
        yield return "ini/ProgressXp.rgn";
        yield return "ini/ProgressMp.rgn";
        yield return "ini/ProgressHp.rgn";
        yield return "ini/MagicType.dat";
        yield return "ini/MagicEffect.ini";
        yield return "ini/SubProfessionInfo.ini";
    }
}
