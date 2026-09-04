namespace OpenConquer.Content.Tests;

public sealed class ClientContentClosureTests
{
    [Fact]
    public void Resolve_ReturnsTheImplementedSlicePathsInOrdinalOrder()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/info.ini", "[DlgLogo]\nBgFormat=Data/Main/Logo%d.bmp\n");

        IReadOnlyList<string> closure = ClientContentClosure.Resolve(
            new ClientContentRoot(temporaryDirectory.RootPath)
        );

        Assert.Equal(
            [
                "Data/Main/Logo1.bmp",
                "Data/Main/Logo2.bmp",
                "ini/GameSetUp.ini",
                "ini/info.ini",
                "ini/package.ini",
            ],
            closure
        );
    }

    /// <summary>
    /// The logo entries are data-driven, so a content root that declares a different format must
    /// produce a different closure rather than the hard-coded default.
    /// </summary>
    [Fact]
    public void Resolve_FollowsTheDeclaredBackgroundFormat()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(
            "ini/info.ini",
            "[DlgLogo]\nBgFormat=data/main/Splash%02d.bmp\n"
        );

        IReadOnlyList<string> closure = ClientContentClosure.Resolve(
            new ClientContentRoot(temporaryDirectory.RootPath)
        );

        Assert.Contains("data/main/Splash01.bmp", closure);
        Assert.Contains("data/main/Splash02.bmp", closure);

        Assert.DoesNotContain(
            closure,
            static contentPath =>
                string.Equals(contentPath, "Server.dat", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Resolve_UsesTheVerifiedDefaultWhenInfoIsAbsent()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/GameSetUp.ini", "[ScreenMode]\nScreenModeRecord=0\n");

        IReadOnlyList<string> closure = ClientContentClosure.Resolve(
            new ClientContentRoot(temporaryDirectory.RootPath)
        );

        Assert.Contains("Data/Main/Logo1.bmp", closure);
        Assert.Contains("Data/Main/Logo2.bmp", closure);

        Assert.DoesNotContain(
            closure,
            static contentPath =>
                string.Equals(contentPath, "Server.dat", StringComparison.OrdinalIgnoreCase)
        );
    }
}
