using System.Reflection;
using System.Xml.Linq;

namespace OpenConquer.Launcher.Tests;

public sealed class LauncherArchitectureTests
{
    [Fact]
    public void LauncherAssemblyDoesNotDependOnGameRuntimeSubsystemsOrSilkNet()
    {
        Assembly launcherAssembly = typeof(Program).Assembly;

        string[] prohibitedReferences = launcherAssembly
            .GetReferencedAssemblies()
            .Select(static assemblyName => assemblyName.Name)
            .OfType<string>()
            .Where(static assemblyName =>
                assemblyName.StartsWith("OpenConquer.", StringComparison.Ordinal)
                || assemblyName.StartsWith("Silk.NET.", StringComparison.Ordinal)
            )
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(prohibitedReferences);
    }

    [Fact]
    public void LauncherWindowsManifestEnforcesStandardUserProcessPolicy()
    {
        string repositoryRoot = GetRepositoryRoot();

        string launcherProjectPath = Path.Combine(
            repositoryRoot,
            "src",
            "OpenConquer.Launcher",
            "OpenConquer.Launcher.csproj"
        );

        XDocument launcherProject = XDocument.Load(launcherProjectPath);

        string applicationManifest = Assert.Single(
            launcherProject
                .Descendants("ApplicationManifest")
                .Select(static element => element.Value)
        );

        Assert.Equal("app.manifest", applicationManifest);

        string launcherProjectDirectory =
            Path.GetDirectoryName(launcherProjectPath)
            ?? throw new InvalidOperationException(
                "The launcher project path has no parent directory."
            );

        string manifestPath = Path.Combine(launcherProjectDirectory, applicationManifest);

        XDocument manifest = XDocument.Load(manifestPath);

        XNamespace assemblyV3 = "urn:schemas-microsoft-com:asm.v3";

        XElement requestedExecutionLevel = Assert.Single(
            manifest.Descendants(assemblyV3 + "requestedExecutionLevel")
        );

        Assert.Equal("asInvoker", requestedExecutionLevel.Attribute("level")?.Value);

        Assert.Equal("false", requestedExecutionLevel.Attribute("uiAccess")?.Value);

        XNamespace compatibility = "urn:schemas-microsoft-com:compatibility.v1";

        XElement supportedOperatingSystem = Assert.Single(
            manifest.Descendants(compatibility + "supportedOS")
        );

        Assert.Equal(
            "{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}",
            supportedOperatingSystem.Attribute("Id")?.Value
        );
    }

    [Fact]
    public void LauncherAssemblyIdentityMatchesProductBoundary()
    {
        Assert.Equal("OpenConquer.Launcher", typeof(Program).Assembly.GetName().Name);
    }

    [Fact]
    public void LauncherWindowDoesNotExposeArbitraryInstallationSelection()
    {
        string repositoryRoot = GetRepositoryRoot();
        string windowPath = Path.Combine(
            repositoryRoot,
            "src",
            "OpenConquer.Launcher",
            "MainWindow.axaml"
        );

        string windowMarkup = File.ReadAllText(windowPath);

        Assert.DoesNotContain("Browse", windowMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("folder path", windowMarkup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Find your game", windowMarkup, StringComparison.Ordinal);
    }

    [Fact]
    public void LauncherWindowUsesAStableNonResizableStatusSurface()
    {
        string repositoryRoot = GetRepositoryRoot();
        string windowPath = Path.Combine(
            repositoryRoot,
            "src",
            "OpenConquer.Launcher",
            "MainWindow.axaml"
        );

        XElement window = XDocument.Load(windowPath).Root
            ?? throw new InvalidOperationException("The launcher window markup has no root element.");

        Assert.Equal("760", window.Attribute("Width")?.Value);
        Assert.Equal("460", window.Attribute("Height")?.Value);
        Assert.Equal("False", window.Attribute("CanResize")?.Value);
    }

    [Fact]
    public void LauncherApplicationResolvesFromItsPackageContext()
    {
        string repositoryRoot = GetRepositoryRoot();
        string appPath = Path.Combine(
            repositoryRoot,
            "src",
            "OpenConquer.Launcher",
            "App.axaml.cs"
        );

        string appSource = File.ReadAllText(appPath);

        Assert.Contains("AppContext.BaseDirectory", appSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenConquer.Client.csproj", appSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LauncherPublishCarriesTheManagedInstallationDescriptor()
    {
        string repositoryRoot = GetRepositoryRoot();
        string launcherProjectPath = Path.Combine(
            repositoryRoot,
            "src",
            "OpenConquer.Launcher",
            "OpenConquer.Launcher.csproj"
        );

        XDocument launcherProject = XDocument.Load(launcherProjectPath);
        XElement descriptor = Assert.Single(
            launcherProject.Descendants("None"),
            static element => element.Attribute("Include")?.Value ==
                "Installation/openconquer.installation.json"
        );

        Assert.Equal(
            "PreserveNewest",
            descriptor.Attribute("CopyToPublishDirectory")?.Value
        );
        Assert.Equal("openconquer.installation.json", descriptor.Attribute("Link")?.Value);
    }

    private static string GetRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpenConquer.Client.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate the OpenConquer repository root above test output directory '{AppContext.BaseDirectory}'."
        );
    }
}
