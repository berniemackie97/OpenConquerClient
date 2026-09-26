namespace OpenConquer.Content.Tests;

public sealed class ClientContentClosureTests
{
    [Fact]
    public void Resolve_ReturnsTheImplementedRuntimeRequirementsInOrdinalOrder()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/info.ini", "[DlgLogo]\nBgFormat=Data/Main/Logo%d.bmp\n");
        WriteVerifiedControlAni(temporaryDirectory);

        IReadOnlyList<ClientContentRequirement> closure = ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(
        [
            new ClientContentRequirement("Data/Main/Logo1.bmp", ContentLookupMode.LooseOnly),
            new ClientContentRequirement("Data/Main/Logo2.bmp", ContentLookupMode.LooseOnly),
            new ClientContentRequirement("ani/Control.ani", ContentLookupMode.LooseOnly),
            new ClientContentRequirement("data/main/ProgressBk.dds", ContentLookupMode.LooseThenPackage),
            new ClientContentRequirement("data/main/ProgressForce.dds", ContentLookupMode.LooseThenPackage),
            new ClientContentRequirement("data/main/ProgressForce2.dds", ContentLookupMode.LooseThenPackage),
            new ClientContentRequirement("data/main/ProgressForce2A.dds", ContentLookupMode.LooseThenPackage),
            new ClientContentRequirement("data/main/ProgressForceA.dds", ContentLookupMode.LooseThenPackage),
            new ClientContentRequirement("data/main/ProgressHP.dds", ContentLookupMode.LooseThenPackage),
            new ClientContentRequirement("data/main/ProgressHPA.dds", ContentLookupMode.LooseThenPackage),
            new ClientContentRequirement("data/main/ProgressHPH.dds", ContentLookupMode.LooseThenPackage),
            new ClientContentRequirement("data/main/ProgressMP.dds", ContentLookupMode.LooseThenPackage),
            new ClientContentRequirement("data/main/ProgressMPA.dds", ContentLookupMode.LooseThenPackage),
            new ClientContentRequirement("data/main/ProgressMPH.dds", ContentLookupMode.LooseThenPackage),
            new ClientContentRequirement("data/main/mainDialog1.dds", ContentLookupMode.LooseThenPackage),
            new ClientContentRequirement("data/main/mainDialog2.dds", ContentLookupMode.LooseThenPackage),
            new ClientContentRequirement("ini/GameSetUp.ini", ContentLookupMode.LooseOnly),
            new ClientContentRequirement("ini/info.ini", ContentLookupMode.LooseOnly),
            new ClientContentRequirement("ini/package.ini", ContentLookupMode.LooseOnly),
        ], closure);
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
    [InlineData("Progress40")]
    [InlineData("Progress41")]
    [InlineData("Progress45")]
    [InlineData("Progress46")]
    [InlineData("Progress47")]
    [InlineData("Dialog4")]
    public void Resolve_RequiresEveryVerifiedHudSectionForTheShippedClosure(string omittedSectionName)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        WriteVerifiedControlAni(temporaryDirectory, omittedSectionName);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath)));

        Assert.Contains($"[{omittedSectionName}]", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Progress40", 2, 3)]
    [InlineData("Progress41", 2, 3)]
    [InlineData("Progress45", 2, 1)]
    [InlineData("Progress46", 1, 2)]
    [InlineData("Progress47", 1, 2)]
    [InlineData("Dialog4", 1, 2)]
    public void Resolve_RejectsUnexpectedVerifiedHudFrameCounts(string sectionName, int actualFrameCount, int expectedFrameCount)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        WriteVerifiedControlAni(temporaryDirectory, overriddenSectionName: sectionName, overriddenFrameCount: actualFrameCount);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath)));

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

    private static void WriteVerifiedControlAni(TemporaryContentDirectory temporaryDirectory, string? omittedSectionName = null, string? overriddenSectionName = null, int overriddenFrameCount = -1)
    {
        temporaryDirectory.WriteFile("ani/Control.ani",
            Section("Progress40", 3, ["data/main/ProgressHP.dds", "data/main/ProgressHPA.dds", "data/main/ProgressHPH.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Progress41", 3, ["data/main/ProgressMP.dds", "data/main/ProgressMPA.dds", "data/main/ProgressMPH.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Progress45", 1, ["data/main/ProgressBk.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Progress46", 2, ["data/main/ProgressForce.dds", "data/main/ProgressForceA.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Progress47", 2, ["data/main/ProgressForce2.dds", "data/main/ProgressForce2A.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount)
            + Section("Dialog4", 2, ["data/main/mainDialog1.dds", "data/main/mainDialog2.dds"], omittedSectionName, overriddenSectionName, overriddenFrameCount));
    }

    private static string Section(string sectionName, int verifiedFrameCount, string[] framePaths, string? omittedSectionName, string? overriddenSectionName, int overriddenFrameCount)
    {
        if (string.Equals(sectionName, omittedSectionName, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        int frameCount = string.Equals(sectionName, overriddenSectionName, StringComparison.Ordinal) ? overriddenFrameCount : verifiedFrameCount;
        string section = $"[{sectionName}]\nFrameAmount={frameCount}\n";

        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            string framePath = frameIndex < framePaths.Length ? framePaths[frameIndex] : $"data/main/TestUnused{frameIndex}.dds";
            section += $"Frame{frameIndex}={framePath}\n";
        }

        return section;
    }
}
