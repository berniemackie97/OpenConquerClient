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

    [Fact]
    public void Resolve_RequiresProgress45AndDialog4ForTheShippedClosure()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ani/Control.ani", "[Progress45]\nFrameAmount=1\nFrame0=data/main/ProgressBk.dds\n");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath)));

        Assert.Contains("[Dialog4]", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[Progress45]\nFrameAmount=2\nFrame0=data/main/ProgressBk.dds\nFrame1=data/main/Unused.dds\n[Dialog4]\nFrameAmount=2\nFrame0=data/main/mainDialog1.dds\nFrame1=data/main/mainDialog2.dds\n", "Progress45")]
    [InlineData("[Progress45]\nFrameAmount=1\nFrame0=data/main/ProgressBk.dds\n[Dialog4]\nFrameAmount=1\nFrame0=data/main/mainDialog1.dds\n", "Dialog4")]
    public void Resolve_RejectsUnexpectedVerifiedHudFrameCounts(string controlAni, string sectionName)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ani/Control.ani", controlAni);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath)));

        Assert.Contains($"[{sectionName}]", exception.Message, StringComparison.Ordinal);
    }

    private static void WriteVerifiedControlAni(TemporaryContentDirectory temporaryDirectory)
    {
        temporaryDirectory.WriteFile("ani/Control.ani",
            "[Dialog4]\n"
            + "FrameAmount=2\n"
            + "Frame0=data/main/mainDialog1.dds\n"
            + "Frame1=data/main/mainDialog2.dds\n"
            + "[Progress45]\n"
            + "FrameAmount=1\n"
            + "Frame0=data/main/ProgressBk.dds\n");
    }
}
