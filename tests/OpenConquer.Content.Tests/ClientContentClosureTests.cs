using System.Text;

namespace OpenConquer.Content.Tests;

public sealed class ClientContentClosureTests
{
    private static readonly (string Name, int Frames)[] s_hudSections =
    [
        ("Progress40", 3), ("Progress41", 3), ("Progress42", 3), ("Progress45", 1),
        ("Progress46", 2), ("Progress47", 2), ("Dialog4", 2),
        ("Button40", 2), ("Button410", 2), ("Button42", 2), ("Button43", 2),
        ("Main3_MissionBtn", 3), ("Button45", 2), ("Button46", 2), ("Button47", 2),
        ("Button49", 2), ("Button48", 2), ("Button412", 2), ("Main3_OrganiseBtn", 4),
        ("Button41", 3), ("Check40", 2), ("Check43", 2), ("Check46", 2), ("Button411", 2),
    ];

    private static readonly string[] s_baseHudFramePaths =
    [
        "data/interface/Style01/Action/MissionBtnNormal.dds",
        "data/interface/Style01/Action/MissionBtnClick.dds",
        "data/interface/Style01/Action/MissionBtnEmboss.dds",
        "data/main/ChatBtn.dds",
        "data/main/ChatBtnClick.dds",
        "data/main/GoodBtn.dds",
        "data/main/GoodBtnClick.dds",
        "data/main/GroupBtn.dds",
        "data/main/GroupBtnClick.dds",
        "data/main/LevWordBtn.dds",
        "data/main/LevWordBtnClick.dds",
        "data/main/MapChk1.dds",
        "data/main/MapChk2.dds",
        "data/main/NpcEquip.dds",
        "data/main/NpcEquipClick.dds",
        "data/main/OrganiseBtnNormal.dds",
        "data/main/OrganiseBtnClick.dds",
        "data/main/OrganiseBtnUnClick.dds",
        "data/main/OrganiseBtnEmboss.dds",
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
        "data/main/ProgressForce2A.dds",
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
        "data/main/mainDialog2.dds",
    ];

    [Fact]
    public void Resolve_ReturnsTheImplementedRuntimeRequirementsInOrdinalOrder()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/info.ini", "[DlgLogo]\nBgFormat=Data/Main/Logo%d.bmp\n");
        WriteVerifiedIndexes(temporaryDirectory);

        IReadOnlyList<ClientContentRequirement> closure = ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(CreateExpectedRequirements(), closure);
        Assert.Equal(100, closure.Count);
    }

    [Fact]
    public void Resolve_FollowsTheDeclaredStartupBackgroundFormat()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/info.ini", "[DlgLogo]\nBgFormat=data/main/Splash%02d.bmp\n");
        WriteVerifiedIndexes(temporaryDirectory);

        IReadOnlyList<ClientContentRequirement> closure = ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Contains(new ClientContentRequirement("data/main/Splash01.bmp", ContentLookupMode.LooseOnly), closure);
        Assert.Contains(new ClientContentRequirement("data/main/Splash02.bmp", ContentLookupMode.LooseOnly), closure);
        Assert.DoesNotContain(closure, static requirement => string.Equals(requirement.ContentPath, "Data/Main/Logo1.bmp", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(closure, static requirement => string.Equals(requirement.ContentPath, "Server.dat", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Resolve_UsesTheVerifiedStartupDefaultWhenInfoIsAbsent()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/GameSetUp.ini", "[ScreenMode]\nScreenModeRecord=0\n");
        WriteVerifiedIndexes(temporaryDirectory);

        IReadOnlyList<ClientContentRequirement> closure = ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Contains(new ClientContentRequirement("Data/Main/Logo1.bmp", ContentLookupMode.LooseOnly), closure);
        Assert.Contains(new ClientContentRequirement("Data/Main/Logo2.bmp", ContentLookupMode.LooseOnly), closure);
    }

    [Theory]
    [MemberData(nameof(VerifiedHudSections))]
    public void Resolve_RequiresEveryVerifiedHudSectionForTheShippedClosure(string sectionName, int _)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        WriteVerifiedIndexes(temporaryDirectory, omittedHudSectionName: sectionName);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath)));

        Assert.Contains($"[{sectionName}]", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(VerifiedHudSections))]
    public void Resolve_RejectsUnexpectedVerifiedHudFrameCounts(string sectionName, int expectedFrameCount)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        int actualFrameCount = expectedFrameCount == 1 ? 2 : expectedFrameCount - 1;

        WriteVerifiedIndexes(temporaryDirectory, overriddenHudSectionName: sectionName, overriddenHudFrameCount: actualFrameCount);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath)));

        Assert.Contains($"[{sectionName}]", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"exactly {expectedFrameCount} frame(s)", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(RequiredQuickbarControlSections))]
    public void Resolve_RequiresEveryFixedQuickbarControlSection(string sectionName)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        WriteVerifiedIndexes(temporaryDirectory, omittedQuickbarControlSectionName: sectionName);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath)));

        Assert.Contains($"[{sectionName}]", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(RequiredQuickbarGlowSections))]
    public void Resolve_RequiresEveryImplementedQuickbarGlowSection(string sectionName)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        WriteVerifiedIndexes(temporaryDirectory, omittedGlowSectionName: sectionName);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath)));

        Assert.Contains($"[{sectionName}]", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Image0")]
    [InlineData("Magic0")]
    public void Resolve_RequiresEverySelectedSkillSection(string sectionName)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        WriteVerifiedIndexes(temporaryDirectory, omittedSelectedSkillSectionName: sectionName);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath)));

        Assert.Contains($"[{sectionName}]", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Image0")]
    [InlineData("Magic0")]
    public void Resolve_RejectsUnexpectedSelectedSkillFrameCounts(string sectionName)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        WriteVerifiedIndexes(temporaryDirectory, overriddenSelectedSkillSectionName: sectionName, overriddenSelectedSkillFrameCount: 2);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath)));

        Assert.Contains($"[{sectionName}]", exception.Message, StringComparison.Ordinal);
        Assert.Contains("exactly 1 frame", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ani/Magic.ani")]
    [InlineData("ani/ItemMinIcon.Ani")]
    [InlineData("ani/effect.ani")]
    public void Resolve_RequiresEveryQuickbarAniCatalog(string catalogPath)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        WriteVerifiedIndexes(temporaryDirectory, omittedCatalogPath: catalogPath);

        Assert.Throws<FileNotFoundException>(() => ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath)));
    }

    [Fact]
    public void Resolve_IncludesParametricQuickbarFamilies()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        WriteVerifiedIndexes(temporaryDirectory);

        IReadOnlyList<ClientContentRequirement> closure = ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Contains(new ClientContentRequirement("ani/Magic.ani", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("ani/ItemMinIcon.Ani", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("ani/effect.ani", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/main/Act1.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/interface/Style01/Action/Dance2BtnNormal.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/main/MagicSkillType1000.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/main/XpSkillType2000.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/ItemMinIcon/Default.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/ItemMinIcon/100.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/Pic/FireLight/01.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/Pic/YellowLight/01.dds", ContentLookupMode.LooseThenPackage), closure);
    }

    [Fact]
    public void Resolve_IncludesSelectedSkillFrames()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        WriteVerifiedIndexes(temporaryDirectory);

        IReadOnlyList<ClientContentRequirement> closure = ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Contains(new ClientContentRequirement("data/main/MainImgMagic.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/main/ImageDisable.dds", ContentLookupMode.LooseThenPackage), closure);
    }

    [Fact]
    public void Resolve_IncludesNativeFontConfiguration()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        WriteVerifiedIndexes(temporaryDirectory);

        IReadOnlyList<ClientContentRequirement> closure = ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Contains(new ClientContentRequirement("ini/Font.ini", ContentLookupMode.LooseOnly), closure);
    }

    [Fact]
    public void Resolve_ExcludesUnreachableCatalogSectionsAndVerifiedMissingRetailFrame()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        WriteVerifiedIndexes(temporaryDirectory);

        IReadOnlyList<ClientContentRequirement> closure = ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.DoesNotContain(closure, static requirement => requirement.ContentPath == "data/main/UnusedControl.dds");
        Assert.DoesNotContain(closure, static requirement => requirement.ContentPath == "data/main/MagicOther.dds");
        Assert.DoesNotContain(closure, static requirement => requirement.ContentPath == "data/ItemMinIcon/Preview.dds");
        Assert.DoesNotContain(closure, static requirement => requirement.ContentPath == "data/Pic/CustomGlow/01.dds");
        Assert.DoesNotContain(closure, static requirement => string.Equals(requirement.ContentPath, "data/main3/skill38.dds", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Resolve_IncludesUnusedNativeStaminaAlternateFrames()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        WriteVerifiedIndexes(temporaryDirectory);

        IReadOnlyList<ClientContentRequirement> closure = ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Contains(new ClientContentRequirement("data/main/ProgressForceA.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/main/ProgressForce2A.dds", ContentLookupMode.LooseThenPackage), closure);
    }

    [Fact]
    public void Resolve_IncludesEveryVerifiedPkSkin()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        WriteVerifiedIndexes(temporaryDirectory);

        IReadOnlyList<ClientContentRequirement> closure = ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath));

        foreach (string path in new[]
        {
            "data/main/PkFree.dds", "data/main/PkFreeClick.dds",
            "data/main/PkSafe.dds", "data/main/PkSafeClick.dds",
            "data/main/PkGroup.dds", "data/main/PkGroupClick.dds",
            "data/main/PkArre.dds", "data/main/PkArreClick.dds",
        })
        {
            Assert.Contains(new ClientContentRequirement(path, ContentLookupMode.LooseThenPackage), closure);
        }
    }

    [Fact]
    public void Resolve_IncludesEveryVerifiedMainHudCheckControlFrame()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        WriteVerifiedIndexes(temporaryDirectory);

        IReadOnlyList<ClientContentRequirement> closure = ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath));

        foreach (string path in new[]
        {
            "data/main/RunChk1.dds", "data/main/RunChk2.dds",
            "data/main/MapChk2.dds", "data/main/MapChk1.dds",
            "data/main/ScreenMoveChk1.dds", "data/main/ScreenMoveChk2.dds",
            "data/main/NpcEquip.dds", "data/main/NpcEquipClick.dds",
        })
        {
            Assert.Contains(new ClientContentRequirement(path, ContentLookupMode.LooseThenPackage), closure);
        }
    }

    public static TheoryData<string, int> VerifiedHudSections
    {
        get
        {
            TheoryData<string, int> data = new();

            foreach ((string name, int frames) in s_hudSections)
            {
                data.Add(name, frames);
            }

            return data;
        }
    }

    public static TheoryData<string> RequiredQuickbarControlSections
    {
        get
        {
            TheoryData<string> data = new();

            data.Add("Compose_CoverPic");
            data.Add("Swapuse_UsemainbBtn");
            data.Add("Swapuse_SwapmainbBtn");
            data.Add("Equip_AddPic");

            for (int digit = 0; digit <= 9; digit++)
            {
                data.Add($"Main3_Num{digit}Pic");
                data.Add($"Equip_Num{digit}");
            }

            return data;
        }
    }

    public static TheoryData<string> RequiredQuickbarGlowSections =>
        new()
        {
            "FireLight",
            "RedLight",
            "BlueLight",
            "RoyalBlueLight",
            "YellowLight",
        };

    private static ClientContentRequirement[] CreateExpectedRequirements()
    {
        List<ClientContentRequirement> expected =
        [
            new("Data/Main/Logo1.bmp", ContentLookupMode.LooseOnly),
            new("Data/Main/Logo2.bmp", ContentLookupMode.LooseOnly),
            new("ani/Control.ani", ContentLookupMode.LooseOnly),
            new("ani/Magic.ani", ContentLookupMode.LooseThenPackage),
            new("ani/ItemMinIcon.Ani", ContentLookupMode.LooseThenPackage),
            new("ani/effect.ani", ContentLookupMode.LooseThenPackage),
            new("ini/Font.ini", ContentLookupMode.LooseOnly),
            new("ini/GameSetUp.ini", ContentLookupMode.LooseOnly),
            new("ini/info.ini", ContentLookupMode.LooseOnly),
        ];

        foreach (string path in s_baseHudFramePaths)
        {
            expected.Add(new ClientContentRequirement(path, ContentLookupMode.LooseThenPackage));
        }

        foreach (string path in GetExpectedQuickbarFramePaths())
        {
            expected.Add(new ClientContentRequirement(path, ContentLookupMode.LooseThenPackage));
        }

        expected.Add(new ClientContentRequirement("data/main/ImageDisable.dds", ContentLookupMode.LooseThenPackage));
        expected.Add(new ClientContentRequirement("data/main/MainImgMagic.dds", ContentLookupMode.LooseThenPackage));

        return expected.OrderBy(static requirement => requirement.ContentPath, StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<string> GetExpectedQuickbarFramePaths()
    {
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

    private static void WriteVerifiedIndexes(
        TemporaryContentDirectory temporaryDirectory,
        string? omittedHudSectionName = null,
        string? overriddenHudSectionName = null,
        int overriddenHudFrameCount = -1,
        string? omittedQuickbarControlSectionName = null,
        string? omittedCatalogPath = null,
        string? omittedGlowSectionName = null,
        string? omittedSelectedSkillSectionName = null,
        string? overriddenSelectedSkillSectionName = null,
        int overriddenSelectedSkillFrameCount = -1)
    {
        StringBuilder control = new();

        AppendSection(control, "Progress40", ["data/main/ProgressHP.dds", "data/main/ProgressHPA.dds", "data/main/ProgressHPH.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Progress41", ["data/main/ProgressMP.dds", "data/main/ProgressMPA.dds", "data/main/ProgressMPH.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Progress42", ["data/main/ProgressPower.dds", "data/main/ProgressPower.dds", "data/main/ProgressPowerH.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Progress45", ["data/main/ProgressBk.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Progress46", ["data/main/ProgressForce.dds", "data/main/ProgressForceA.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Progress47", ["data/main/ProgressForce2.dds", "data/main/ProgressForce2A.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Dialog4", ["data/main/mainDialog1.dds", "data/main/mainDialog2.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Button40", ["data/main/QueryBtn.dds", "data/main/QueryBtnClick.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Button410", ["data/main/LevWordBtn.dds", "data/main/LevWordBtnClick.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Button42", ["data/main/GoodBtn.dds", "data/main/GoodBtnClick.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Button43", ["data/main/SetBtn.dds", "data/main/SetBtnClick.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Main3_MissionBtn", ["data/interface/Style01/Action/MissionBtnNormal.dds", "data/interface/Style01/Action/MissionBtnClick.dds", "data/interface/Style01/Action/MissionBtnEmboss.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Button45", ["data/main/ChatBtn.dds", "data/main/ChatBtnClick.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Button46", ["data/main/GroupBtn.dds", "data/main/GroupBtnClick.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Button47", ["data/main/PkFree.dds", "data/main/PkFreeClick.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Button49", ["data/main/PkSafe.dds", "data/main/PkSafeClick.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Button48", ["data/main/PkGroup.dds", "data/main/PkGroupClick.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Button412", ["data/main/PkArre.dds", "data/main/PkArreClick.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Main3_OrganiseBtn", ["data/main/OrganiseBtnNormal.dds", "data/main/OrganiseBtnClick.dds", "data/main/OrganiseBtnUnClick.dds", "data/main/OrganiseBtnEmboss.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Button41", ["data/main/SkillBtn.dds", "data/main/SkillBtnClick.dds", "data/main/SkillBtnL.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Check40", ["data/main/RunChk1.dds", "data/main/RunChk2.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Check43", ["data/main/MapChk2.dds", "data/main/MapChk1.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Check46", ["data/main/ScreenMoveChk1.dds", "data/main/ScreenMoveChk2.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);
        AppendSection(control, "Button411", ["data/main/NpcEquip.dds", "data/main/NpcEquipClick.dds"], omittedHudSectionName, overriddenHudSectionName, overriddenHudFrameCount);

        AppendQuickbarSection(control, "Compose_CoverPic", ["data/interface/compose/CoverPic.dds"], omittedQuickbarControlSectionName);
        AppendQuickbarSection(control, "Swapuse_UsemainbBtn", ["data/main/UsemainbBtnNormal.dds"], omittedQuickbarControlSectionName);
        AppendQuickbarSection(control, "Swapuse_SwapmainbBtn", ["data/main/SwapmainbBtnNormal.dds"], omittedQuickbarControlSectionName);
        AppendQuickbarSection(control, "Equip_AddPic", ["data/interface/Style01/Equip/Num/AddPic.dds"], omittedQuickbarControlSectionName);

        for (int digit = 0; digit <= 9; digit++)
        {
            AppendQuickbarSection(control, $"Main3_Num{digit}Pic", [$"data/main/Num{digit}Pic.dds"], omittedQuickbarControlSectionName);
            AppendQuickbarSection(control, $"Equip_Num{digit}", [$"data/interface/Style01/Equip/Num/{digit}.dds"], omittedQuickbarControlSectionName);
        }

        AppendQuickbarSection(control, "ButtonA1", ["data/main/Act1.dds"], omittedQuickbarControlSectionName);
        AppendQuickbarSection(control, "Action_Dance2Btn", ["data/interface/Style01/Action/Dance2BtnNormal.dds"], omittedQuickbarControlSectionName);
        AppendQuickbarSection(control, "OtherControl", ["data/main/UnusedControl.dds"], omittedQuickbarControlSectionName);

        AppendSection(control, "Image0", ["data/main/ImageDisable.dds"], omittedSelectedSkillSectionName, overriddenSelectedSkillSectionName, overriddenSelectedSkillFrameCount);

        temporaryDirectory.WriteFile("ani/Control.ani", control.ToString());

        if (!string.Equals(omittedCatalogPath, "ani/Magic.ani", StringComparison.Ordinal))
        {
            StringBuilder magic = new();

            AppendSection(magic, "Magic0", ["data/main/MainImgMagic.dds"], omittedSelectedSkillSectionName, overriddenSelectedSkillSectionName, overriddenSelectedSkillFrameCount);
            AppendSection(magic, "MagicSkillType1000", ["data/main/MagicSkillType1000.dds"], null, null, -1);
            AppendSection(magic, "XpSkillType2000", ["data/main/XpSkillType2000.dds"], null, null, -1);
            AppendSection(magic, "MagicSkillType1415", ["data/main3/skill38.dds"], null, null, -1);
            AppendSection(magic, "MagicOther", ["data/main/MagicOther.dds"], null, null, -1);

            temporaryDirectory.WriteFile("ani/Magic.ani", magic.ToString());
        }

        if (!string.Equals(omittedCatalogPath, "ani/ItemMinIcon.Ani", StringComparison.Ordinal))
        {
            temporaryDirectory.WriteFile("ani/ItemMinIcon.Ani",
                "[ItemDefault]\nFrameAmount=1\nFrame0=data/ItemMinIcon/Default.dds\n"
                + "[Item100]\nFrameAmount=1\nFrame0=data/ItemMinIcon/100.dds\n"
                + "[ItemPreview]\nFrameAmount=1\nFrame0=data/ItemMinIcon/Preview.dds\n");
        }

        if (!string.Equals(omittedCatalogPath, "ani/effect.ani", StringComparison.Ordinal))
        {
            StringBuilder effect = new();

            AppendOptionalSection(effect, "FireLight", ["data/Pic/FireLight/01.dds", "data/Pic/FireLight/02.dds"], omittedGlowSectionName);
            AppendOptionalSection(effect, "RedLight", ["data/Pic/RedLight/01.dds"], omittedGlowSectionName);
            AppendOptionalSection(effect, "BlueLight", ["data/Pic/BlueLight/01.dds"], omittedGlowSectionName);
            AppendOptionalSection(effect, "RoyalBlueLight", ["data/Pic/RoyalBlueLight/01.dds"], omittedGlowSectionName);
            AppendOptionalSection(effect, "YellowLight", ["data/Pic/YellowLight/01.dds"], omittedGlowSectionName);
            AppendOptionalSection(effect, "CustomGlow", ["data/Pic/CustomGlow/01.dds"], null);

            temporaryDirectory.WriteFile("ani/effect.ani", effect.ToString());
        }
    }

    private static void AppendSection(StringBuilder builder, string sectionName, string[] framePaths, string? omittedSectionName, string? overriddenSectionName, int overriddenFrameCount)
    {
        if (string.Equals(sectionName, omittedSectionName, StringComparison.Ordinal))
        {
            return;
        }

        int frameCount = string.Equals(sectionName, overriddenSectionName, StringComparison.Ordinal) ? overriddenFrameCount : framePaths.Length;

        builder.Append('[').Append(sectionName).Append("]\nFrameAmount=").Append(frameCount).Append('\n');

        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            string framePath = frameIndex < framePaths.Length ? framePaths[frameIndex] : $"data/main/TestUnused{frameIndex}.dds";
            builder.Append("Frame").Append(frameIndex).Append('=').Append(framePath).Append('\n');
        }
    }

    private static void AppendQuickbarSection(StringBuilder builder, string sectionName, string[] framePaths, string? omittedSectionName)
    {
        if (!string.Equals(sectionName, omittedSectionName, StringComparison.Ordinal))
        {
            AppendSection(builder, sectionName, framePaths, null, null, -1);
        }
    }

    private static void AppendOptionalSection(StringBuilder builder, string sectionName, string[] framePaths, string? omittedSectionName)
    {
        if (!string.Equals(sectionName, omittedSectionName, StringComparison.Ordinal))
        {
            AppendSection(builder, sectionName, framePaths, null, null, -1);
        }
    }
}
