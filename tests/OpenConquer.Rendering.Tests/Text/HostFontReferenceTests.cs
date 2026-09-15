using OpenConquer.Rendering.Text;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class HostFontReferenceTests
{
    [Fact]
    public void Constructor_PreservesFilePathAndFaceIndex()
    {
        HostFontReference reference = new("/fonts/example.ttc", faceIndex: 3);

        Assert.Equal("/fonts/example.ttc", reference.FilePath);
        Assert.Equal(3, reference.FaceIndex);
    }

    [Fact]
    public void Constructor_WithoutFaceIndex_PreservesUnknownFace()
    {
        HostFontReference reference = new("/fonts/example.ttf");

        Assert.Equal("/fonts/example.ttf", reference.FilePath);
        Assert.Null(reference.FaceIndex);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_EmptyOrWhiteSpaceFilePath_Throws(string filePath)
    {
        Assert.Throws<ArgumentException>(() => new HostFontReference(filePath));
    }

    [Fact]
    public void Constructor_NullFilePath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new HostFontReference(null!));
    }

    [Fact]
    public void Constructor_NegativeFaceIndex_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HostFontReference("/fonts/example.ttc", faceIndex: -1));
    }

    [Fact]
    public void Constructor_ZeroFaceIndex_IsAccepted()
    {
        HostFontReference reference = new("/fonts/example.ttf", faceIndex: 0);

        Assert.Equal(0, reference.FaceIndex);
    }
}
