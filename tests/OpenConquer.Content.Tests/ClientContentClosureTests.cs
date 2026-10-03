namespace OpenConquer.Content.Tests;

public sealed class ClientContentClosureTests
{
    private static readonly (string Name, int Frames)[] s_hudSections =
    [
        ("Progress40", 3),
        ("Progress41", 3),
        ("Progress42", 3),
        ("Progress45", 1),
        ("Progress46", 2),
        ("Progress47", 2),
        ("Dialog4", 2),
        ("Button40", 2),
        ("Button410", 2),
        ("Button42", 2),
        ("Button43", 2),
        ("Main3_MissionBtn", 3),
        ("Button45", 2),
        ("Button46", 2),
        ("Button47", 2),
        ("Button49", 2),
        ("Button48", 2),
        ("Button412", 2),
        ("Main3_OrganiseBtn", 4),
        ("Button41", 3),
        ("Check40", 2),
        ("Check43", 2),
        ("Check46", 2),
        ("Button411", 2),
    ];

    [Fact]
    public void Resolve_ReturnsTheImplementedRuntimeRequirementsInOrdinalOrder()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/info.ini", "[DlgLogo]\nBgFormat=Data/Main/Logo%d.bmp\n");
        WriteVerifiedControlAni(temporaryDirectory);

        IReadOnlyList<ClientContentRequirement> closure = ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath));

        ClientContentRequirement[] expected =
        [
            new("Data/Main/Logo1.bmp", ContentLookupMode.LooseOnly),
            new("Data/Main/Logo2.bmp", ContentLookupMode.LooseOnly),
            new("ani/Control.ani", ContentLookupMode.LooseOnly),
            new("data/interface/Style01/Action/MissionBtnNormal.dds", ContentLookupMode.LooseThenPackage),
            new("data/interface/Style01/Action/MissionBtnClick.dds", ContentLookupMode.LooseThenPackage),
            new("data/interface/Style01/Action/MissionBtnEmboss.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/ChatBtn.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/ChatBtnClick.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/GoodBtn.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/GoodBtnClick.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/GroupBtn.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/GroupBtnClick.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/LevWordBtn.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/LevWordBtnClick.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/MapChk1.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/MapChk2.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/NpcEquip.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/NpcEquipClick.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/OrganiseBtnNormal.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/OrganiseBtnClick.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/OrganiseBtnUnClick.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/OrganiseBtnEmboss.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/PkArre.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/PkArreClick.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/PkFree.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/PkFreeClick.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/PkGroup.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/PkGroupClick.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/PkSafe.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/PkSafeClick.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/ProgressBk.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/ProgressForce.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/ProgressForce2.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/ProgressForce2A.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/ProgressForceA.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/ProgressHP.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/ProgressHPA.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/ProgressHPH.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/ProgressMP.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/ProgressMPA.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/ProgressMPH.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/ProgressPower.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/ProgressPowerH.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/QueryBtn.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/QueryBtnClick.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/RunChk1.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/RunChk2.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/ScreenMoveChk1.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/ScreenMoveChk2.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/SetBtn.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/SetBtnClick.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/SkillBtn.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/SkillBtnClick.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/SkillBtnL.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/mainDialog1.dds", ContentLookupMode.LooseThenPackage),
            new("data/main/mainDialog2.dds", ContentLookupMode.LooseThenPackage),
            new("ini/GameSetUp.ini", ContentLookupMode.LooseOnly),
            new("ini/info.ini", ContentLookupMode.LooseOnly),
        ];

        Assert.Equal(expected.OrderBy(static requirement => requirement.ContentPath, StringComparer.Ordinal).ToArray(), closure);
    }

    [Fact]
    public void Resolve_FollowsTheDeclaredStartupBackgroundFormat()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/info.ini", "[DlgLogo]\nBgFormat=data/main/Splash%02d.bmp\n");
        WriteVerifiedControlAni(temporaryDirectory);

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
        WriteVerifiedControlAni(temporaryDirectory);

        IReadOnlyList<ClientContentRequirement> closure = ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Contains(new ClientContentRequirement("Data/Main/Logo1.bmp", ContentLookupMode.LooseOnly), closure);
        Assert.Contains(new ClientContentRequirement("Data/Main/Logo2.bmp", ContentLookupMode.LooseOnly), closure);
        Assert.DoesNotContain(closure, static requirement => string.Equals(requirement.ContentPath, "Server.dat", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(VerifiedHudSections))]
    public void Resolve_RequiresEveryVerifiedHudSectionForTheShippedClosure(string sectionName, int _)
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        WriteVerifiedControlAni(temporaryDirectory, omittedSectionName: sectionName);

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
        WriteVerifiedControlAni(temporaryDirectory, overriddenSectionName: sectionName, overriddenFrameCount: actualFrameCount);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath)));

        Assert.Contains($"[{sectionName}]", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"exactly {expectedFrameCount} frame(s)", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_IncludesUnusedNativeStaminaAlternateFrames()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        WriteVerifiedControlAni(temporaryDirectory);

        IReadOnlyList<ClientContentRequirement> closure = ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Contains(new ClientContentRequirement("data/main/ProgressForceA.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/main/ProgressForce2A.dds", ContentLookupMode.LooseThenPackage), closure);
    }

    [Fact]
    public void Resolve_IncludesEveryVerifiedPkSkin()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        WriteVerifiedControlAni(temporaryDirectory);

        IReadOnlyList<ClientContentRequirement> closure = ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Contains(new ClientContentRequirement("data/main/PkFree.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/main/PkFreeClick.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/main/PkSafe.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/main/PkSafeClick.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/main/PkGroup.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/main/PkGroupClick.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/main/PkArre.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/main/PkArreClick.dds", ContentLookupMode.LooseThenPackage), closure);
    }

    [Fact]
    public void Resolve_IncludesEveryVerifiedMainHudCheckControlFrame()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        WriteVerifiedControlAni(temporaryDirectory);

        IReadOnlyList<ClientContentRequirement> closure = ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Contains(new ClientContentRequirement("data/main/RunChk1.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/main/RunChk2.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/main/MapChk2.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/main/MapChk1.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/main/ScreenMoveChk1.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/main/ScreenMoveChk2.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/main/NpcEquip.dds", ContentLookupMode.LooseThenPackage), closure);
        Assert.Contains(new ClientContentRequirement("data/main/NpcEquipClick.dds", ContentLookupMode.LooseThenPackage), closure);
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

    private static void WriteVerifiedControlAni(TemporaryContentDirectory temporaryDirectory, string? omittedSectionName = null, string? overriddenSectionName = null, int overriddenFrameCount = -1)
    {
        temporaryDirectory.WriteFile("ani/Control.ani",
            Section("Progress40", ["data/main/ProgressHP.dds", "data/main/ProgressHPA.dds", "data/main/ProgressHPH.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Progress41", ["data/main/ProgressMP.dds", "data/main/ProgressMPA.dds", "data/main/ProgressMPH.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Progress42", ["data/main/ProgressPower.dds", "data/main/ProgressPower.dds", "data/main/ProgressPowerH.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Progress45", ["data/main/ProgressBk.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Progress46", ["data/main/ProgressForce.dds", "data/main/ProgressForceA.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Progress47", ["data/main/ProgressForce2.dds", "data/main/ProgressForce2A.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Dialog4", ["data/main/mainDialog1.dds", "data/main/mainDialog2.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Button40", ["data/main/QueryBtn.dds", "data/main/QueryBtnClick.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Button410", ["data/main/LevWordBtn.dds", "data/main/LevWordBtnClick.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Button42", ["data/main/GoodBtn.dds", "data/main/GoodBtnClick.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Button43", ["data/main/SetBtn.dds", "data/main/SetBtnClick.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Main3_MissionBtn", ["data/interface/Style01/Action/MissionBtnNormal.dds", "data/interface/Style01/Action/MissionBtnClick.dds", "data/interface/Style01/Action/MissionBtnEmboss.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Button45", ["data/main/ChatBtn.dds", "data/main/ChatBtnClick.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Button46", ["data/main/GroupBtn.dds", "data/main/GroupBtnClick.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Button47", ["data/main/PkFree.dds", "data/main/PkFreeClick.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Button49", ["data/main/PkSafe.dds", "data/main/PkSafeClick.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Button48", ["data/main/PkGroup.dds", "data/main/PkGroupClick.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Button412", ["data/main/PkArre.dds", "data/main/PkArreClick.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Main3_OrganiseBtn", ["data/main/OrganiseBtnNormal.dds", "data/main/OrganiseBtnClick.dds", "data/main/OrganiseBtnUnClick.dds", "data/main/OrganiseBtnEmboss.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Button41", ["data/main/SkillBtn.dds", "data/main/SkillBtnClick.dds", "data/main/SkillBtnL.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Check40", ["data/main/RunChk1.dds", "data/main/RunChk2.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Check43", ["data/main/MapChk2.dds", "data/main/MapChk1.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Check46", ["data/main/ScreenMoveChk1.dds", "data/main/ScreenMoveChk2.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Button411", ["data/main/NpcEquip.dds", "data/main/NpcEquipClick.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount));
    }

    private static string Section(string sectionName, string[] framePaths, string? omittedSectionName, string? overriddenSectionName, int overriddenFrameCount)
    {
        if (string.Equals(sectionName, omittedSectionName, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        int frameCount = string.Equals(sectionName, overriddenSectionName, StringComparison.Ordinal)
            ? overriddenFrameCount
            : framePaths.Length;

        string section = $"[{sectionName}]\nFrameAmount={frameCount}\n";

        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            string framePath = frameIndex < framePaths.Length ? framePaths[frameIndex] : $"data/main/TestUnused{frameIndex}.dds";
            section += $"Frame{frameIndex}={framePath}\n";
        }

        return section;
    }
}
