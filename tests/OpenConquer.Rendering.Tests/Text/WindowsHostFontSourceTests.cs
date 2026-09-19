using OpenConquer.Rendering.Text.Fonts.Discovery;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class WindowsHostFontSourceTests
{
    [Fact]
    public void Discover_OnWindows_ReturnsRegisteredFontsAndValidDefaultGuiFontWhenAvailable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        WindowsHostFontSource source = new();
        HostFontDiscovery discovery = source.Discover();

        Assert.NotEmpty(discovery.FontReferences);

        Assert.All(discovery.FontReferences, reference =>
        {
            Assert.False(string.IsNullOrWhiteSpace(reference.FilePath));
            Assert.True(Path.IsPathFullyQualified(reference.FilePath));
            Assert.NotNull(reference.FaceIndex);
            Assert.True(reference.FaceIndex >= 0);
        });

        if (discovery.DefaultGuiFont is { } defaultGuiFont)
        {
            Assert.False(string.IsNullOrWhiteSpace(defaultGuiFont.FilePath));
            Assert.True(Path.IsPathFullyQualified(defaultGuiFont.FilePath));
            Assert.NotNull(defaultGuiFont.FaceIndex);
            Assert.True(defaultGuiFont.FaceIndex >= 0);
        }
    }

    [Fact]
    public void Discover_OnUnsupportedPlatform_Throws()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        WindowsHostFontSource source = new();

        Assert.Throws<PlatformNotSupportedException>(() => source.Discover());
    }
}
