using OpenConquer.Rendering.Text;
using OpenConquer.Rendering.Text.Fonts.FreeType;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class FreeTypeLibraryTests
{
    [Fact]
    public void Constructor_InitializesSupportedFreeTypeLibrary()
    {
        using FreeTypeLibrary library = new();
    }

    [Fact]
    public void Dispose_CanBeCalledMoreThanOnce()
    {
        FreeTypeLibrary library = new();

        library.Dispose();
        library.Dispose();
    }

    [Theory]
    [InlineData(1, 99, 99)]
    [InlineData(2, 13, 99)]
    [InlineData(2, 14, 0)]
    [InlineData(2, 14, 1)]
    [InlineData(2, 14, 2)]
    [InlineData(3, 0, 0)]
    public void IsSupportedNativeVersion_UnsupportedVersion_ReturnsFalse(int major, int minor, int patch)
    {
        Assert.False(FreeTypeLibrary.IsSupportedNativeVersion(major, minor, patch));
    }

    [Theory]
    [InlineData(2, 14, 3)]
    [InlineData(2, 14, 4)]
    [InlineData(2, 15, 0)]
    public void IsSupportedNativeVersion_SupportedVersion_ReturnsTrue(int major, int minor, int patch)
    {
        Assert.True(FreeTypeLibrary.IsSupportedNativeVersion(major, minor, patch));
    }
}
