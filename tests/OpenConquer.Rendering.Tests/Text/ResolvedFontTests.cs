using OpenConquer.Rendering.Text.Fonts;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class ResolvedFontTests
{
    [Fact]
    public void Constructor_PreservesFilePathAndFaceIndex()
    {
        ResolvedFont font = new("/fonts/example.ttc", faceIndex: 3);

        Assert.Equal("/fonts/example.ttc", font.FilePath);
        Assert.Equal(3, font.FaceIndex);
    }

    [Fact]
    public void Constructor_NullFilePath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ResolvedFont(null!, faceIndex: 0));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_EmptyOrWhiteSpaceFilePath_Throws(string filePath)
    {
        Assert.Throws<ArgumentException>(() => new ResolvedFont(filePath, faceIndex: 0));
    }

    [Fact]
    public void Constructor_NegativeFaceIndex_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResolvedFont("/fonts/example.ttc", faceIndex: -1));
    }
}
