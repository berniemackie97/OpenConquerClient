using System.Reflection;
using Avalonia;
using Avalonia.Controls;

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
    public void LauncherUsesDedicatedAvaloniaApplicationAndWindowTypes()
    {
        Assert.True(typeof(Application).IsAssignableFrom(typeof(App)));

        Assert.True(typeof(Window).IsAssignableFrom(typeof(MainWindow)));
    }

    [Fact]
    public void LauncherAssemblyIdentityMatchesProductBoundary()
    {
        Assert.Equal("OpenConquer.Launcher", typeof(Program).Assembly.GetName().Name);
    }
}
