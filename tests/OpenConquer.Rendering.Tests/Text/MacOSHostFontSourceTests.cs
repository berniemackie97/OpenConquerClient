using OpenConquer.Rendering.Text;
using OpenConquer.Rendering.Text.Fonts.Discovery;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class MacOSHostFontSourceTests
{
    [Fact]
    public void Discover_OnMacOS_ReturnsRegisteredFontsAndDefaultGuiFont()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        MacOSHostFontSource source = new();
        HostFontDiscovery discovery = source.Discover();

        Assert.NotEmpty(discovery.FontReferences);
        Assert.NotNull(discovery.DefaultGuiFont);
        Assert.False(string.IsNullOrWhiteSpace(discovery.DefaultGuiFont.FilePath));
        Assert.True(Path.IsPathFullyQualified(discovery.DefaultGuiFont.FilePath));
        Assert.Null(discovery.DefaultGuiFont.FaceIndex);

        Assert.All(discovery.FontReferences, reference =>
        {
            Assert.False(string.IsNullOrWhiteSpace(reference.FilePath));
            Assert.True(Path.IsPathFullyQualified(reference.FilePath));
            Assert.Null(reference.FaceIndex);
        });
    }

    [Fact]
    public void Discover_OnUnsupportedPlatform_Throws()
    {
        if (OperatingSystem.IsMacOS())
        {
            return;
        }

        MacOSHostFontSource source = new();

        Assert.Throws<PlatformNotSupportedException>(() => source.Discover());
    }
}
