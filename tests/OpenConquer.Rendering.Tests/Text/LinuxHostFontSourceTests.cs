using OpenConquer.Rendering.Text;
using OpenConquer.Rendering.Text.Fonts.Discovery;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class LinuxHostFontSourceTests
{
    [Fact]
    public void Discover_OnLinux_ReturnsRegisteredFontsAndDefaultGuiFont()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        LinuxHostFontSource source = new();
        HostFontDiscovery discovery = source.Discover();

        Assert.NotEmpty(discovery.FontReferences);
        Assert.NotNull(discovery.DefaultGuiFont);
        Assert.False(string.IsNullOrWhiteSpace(discovery.DefaultGuiFont.FilePath));
        Assert.True(Path.IsPathFullyQualified(discovery.DefaultGuiFont.FilePath));
        Assert.NotNull(discovery.DefaultGuiFont.FaceIndex);
        Assert.True(discovery.DefaultGuiFont.FaceIndex >= 0);

        Assert.All(
            discovery.FontReferences,
            reference =>
            {
                Assert.False(string.IsNullOrWhiteSpace(reference.FilePath));
                Assert.True(Path.IsPathFullyQualified(reference.FilePath));
                Assert.NotNull(reference.FaceIndex);
                Assert.True(reference.FaceIndex >= 0);
            }
        );
    }

    [Fact]
    public void Discover_OnUnsupportedPlatform_Throws()
    {
        if (OperatingSystem.IsLinux())
        {
            return;
        }

        LinuxHostFontSource source = new();

        Assert.Throws<PlatformNotSupportedException>(() => source.Discover());
    }
}
