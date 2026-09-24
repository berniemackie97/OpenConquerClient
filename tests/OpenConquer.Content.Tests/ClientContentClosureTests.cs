namespace OpenConquer.Content.Tests;

public sealed class ClientContentClosureTests
{
    [Fact]
    public void Resolve_ReturnsTheImplementedSliceRequirementsInOrdinalOrder()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/info.ini", "[DlgLogo]\nBgFormat=Data/Main/Logo%d.bmp\n");

        IReadOnlyList<ClientContentRequirement> closure = ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(
            [
                new ClientContentRequirement("Data/Main/Logo1.bmp", ContentLookupMode.LooseOnly),
                new ClientContentRequirement("Data/Main/Logo2.bmp", ContentLookupMode.LooseOnly),
                new ClientContentRequirement("ini/GameSetUp.ini", ContentLookupMode.LooseOnly),
                new ClientContentRequirement("ini/info.ini", ContentLookupMode.LooseOnly),
                new ClientContentRequirement("ini/package.ini", ContentLookupMode.LooseOnly),
            ],
            closure
        );
    }

    [Fact]
    public void Resolve_FollowsTheDeclaredBackgroundFormat()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/info.ini", "[DlgLogo]\nBgFormat=data/main/Splash%02d.bmp\n");

        IReadOnlyList<ClientContentRequirement> closure = ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Contains(new ClientContentRequirement("data/main/Splash01.bmp", ContentLookupMode.LooseOnly), closure);
        Assert.Contains(new ClientContentRequirement("data/main/Splash02.bmp", ContentLookupMode.LooseOnly), closure);

        Assert.DoesNotContain(closure, static requirement => string.Equals(requirement.ContentPath, "Server.dat", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Resolve_UsesTheVerifiedDefaultWhenInfoIsAbsent()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/GameSetUp.ini", "[ScreenMode]\nScreenModeRecord=0\n");

        IReadOnlyList<ClientContentRequirement> closure = ClientContentClosure.Resolve(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Contains(new ClientContentRequirement("Data/Main/Logo1.bmp", ContentLookupMode.LooseOnly), closure);
        Assert.Contains(new ClientContentRequirement("Data/Main/Logo2.bmp", ContentLookupMode.LooseOnly), closure);

        Assert.DoesNotContain(closure, static requirement => string.Equals(requirement.ContentPath, "Server.dat", StringComparison.OrdinalIgnoreCase));
    }
}
