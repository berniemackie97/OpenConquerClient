namespace OpenConquer.Product.Tool.Tests;

public sealed class DevelopmentPublisherIdentityPathsTests
{
    [Fact]
    public void CreateWindowsUsesLocalApplicationData()
    {
        string userProfile = AbsoluteTestPath("windows-user");
        string localApplicationData = AbsoluteTestPath("windows-local");

        DevelopmentPublisherIdentityPaths paths = DevelopmentPublisherIdentityPaths.Create(
            ProductHostPlatform.Windows,
            userProfile,
            localApplicationData,
            xdgConfigHome: null
        );

        AssertPaths(paths, Path.Combine(localApplicationData, "OpenConquer", "Development"));
    }

    [Fact]
    public void CreateMacOSUsesApplicationSupport()
    {
        string userProfile = AbsoluteTestPath("mac-user");

        DevelopmentPublisherIdentityPaths paths = DevelopmentPublisherIdentityPaths.Create(
            ProductHostPlatform.MacOS,
            userProfile,
            localApplicationDataPath: null,
            xdgConfigHome: null
        );

        AssertPaths(
            paths,
            Path.Combine(
                userProfile,
                "Library",
                "Application Support",
                "OpenConquer",
                "Development"
            )
        );
    }

    [Fact]
    public void CreateLinuxUsesAbsoluteXdgConfigHome()
    {
        string userProfile = AbsoluteTestPath("linux-user");
        string xdgConfigHome = AbsoluteTestPath("linux-xdg");

        DevelopmentPublisherIdentityPaths paths = DevelopmentPublisherIdentityPaths.Create(
            ProductHostPlatform.Linux,
            userProfile,
            localApplicationDataPath: null,
            xdgConfigHome
        );

        AssertPaths(paths, Path.Combine(xdgConfigHome, "OpenConquer", "Development"));
    }

    [Fact]
    public void CreateLinuxFallsBackToUserConfigDirectory()
    {
        string userProfile = AbsoluteTestPath("linux-user");

        DevelopmentPublisherIdentityPaths paths = DevelopmentPublisherIdentityPaths.Create(
            ProductHostPlatform.Linux,
            userProfile,
            localApplicationDataPath: null,
            xdgConfigHome: null
        );

        AssertPaths(paths, Path.Combine(userProfile, ".config", "OpenConquer", "Development"));
    }

    [Fact]
    public void CreateLinuxIgnoresRelativeXdgConfigHome()
    {
        string userProfile = AbsoluteTestPath("linux-user");

        DevelopmentPublisherIdentityPaths paths = DevelopmentPublisherIdentityPaths.Create(
            ProductHostPlatform.Linux,
            userProfile,
            localApplicationDataPath: null,
            "relative-config"
        );

        AssertPaths(paths, Path.Combine(userProfile, ".config", "OpenConquer", "Development"));
    }

    [Fact]
    public void CreateWindowsRejectsMissingLocalApplicationData()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DevelopmentPublisherIdentityPaths.Create(
                ProductHostPlatform.Windows,
                AbsoluteTestPath("user"),
                localApplicationDataPath: null,
                xdgConfigHome: null
            )
        );
    }

    [Fact]
    public void CreateMacOSRejectsMissingUserProfile()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DevelopmentPublisherIdentityPaths.Create(
                ProductHostPlatform.MacOS,
                string.Empty,
                localApplicationDataPath: null,
                xdgConfigHome: null
            )
        );
    }

    [Fact]
    public void CreateLinuxRejectsMissingUserProfileWithoutUsableXdgPath()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DevelopmentPublisherIdentityPaths.Create(
                ProductHostPlatform.Linux,
                string.Empty,
                localApplicationDataPath: null,
                xdgConfigHome: null
            )
        );
    }

    private static string AbsoluteTestPath(string name)
    {
        return Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "openconquer-development-paths", name)
        );
    }

    private static void AssertPaths(DevelopmentPublisherIdentityPaths paths, string expectedRoot)
    {
        expectedRoot = Path.GetFullPath(expectedRoot);

        Assert.Equal(expectedRoot, paths.RootPath);
        Assert.Equal(
            Path.Combine(expectedRoot, DevelopmentPublisherIdentityPaths.PrivateKeyFileName),
            paths.PrivateKeyPath
        );
    }
}
