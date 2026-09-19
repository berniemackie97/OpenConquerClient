using OpenConquer.Rendering.Text;
using OpenConquer.Rendering.Text.Fonts.FreeType;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class FreeTypeFontInspectorTests
{
    private static string FontFilePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Fonts", "Knewave-Regular.ttf");

    [Fact]
    public void ReadFaces_ValidSingleFaceFont_ReturnsFamilyAndStyleMetadata()
    {
        byte[] fontData = File.ReadAllBytes(FontFilePath);

        using FreeTypeLibrary library = new();
        FreeTypeFontInspector inspector = new(library);

        FreeTypeFontInspector.FaceInfo[] faces = inspector.ReadFaces(fontData);

        FreeTypeFontInspector.FaceInfo face = Assert.Single(faces);
        Assert.Equal(0, face.FaceIndex);
        Assert.Equal("Knewave", face.FamilyName);
        Assert.False(face.IsBold);
        Assert.False(face.IsItalic);
    }

    [Fact]
    public void ReadFaces_EmptyData_ReturnsEmpty()
    {
        using FreeTypeLibrary library = new();
        FreeTypeFontInspector inspector = new(library);

        FreeTypeFontInspector.FaceInfo[] faces = inspector.ReadFaces([]);

        Assert.Empty(faces);
    }

    [Fact]
    public void ReadFaces_InvalidFontData_ReturnsEmpty()
    {
        byte[] fontData = [0x01, 0x02, 0x03, 0x04];

        using FreeTypeLibrary library = new();
        FreeTypeFontInspector inspector = new(library);

        FreeTypeFontInspector.FaceInfo[] faces = inspector.ReadFaces(fontData);

        Assert.Empty(faces);
    }

    [Fact]
    public void ReadFaces_AfterLibraryDispose_Throws()
    {
        byte[] fontData = File.ReadAllBytes(FontFilePath);

        FreeTypeLibrary library = new();
        FreeTypeFontInspector inspector = new(library);

        library.Dispose();

        Assert.Throws<ObjectDisposedException>(() => inspector.ReadFaces(fontData));
    }

    [Fact]
    public void FaceInfo_PreservesValues()
    {
        FreeTypeFontInspector.FaceInfo face = new(faceIndex: 3, familyName: "Example", isBold: true, isItalic: true);

        Assert.Equal(3, face.FaceIndex);
        Assert.Equal("Example", face.FamilyName);
        Assert.True(face.IsBold);
        Assert.True(face.IsItalic);
    }

    [Fact]
    public void FaceInfo_DefaultConstructorRepresentsRegularFace()
    {
        FreeTypeFontInspector.FaceInfo face = new(faceIndex: 3, familyName: "Example");

        Assert.False(face.IsBold);
        Assert.False(face.IsItalic);
    }

    [Fact]
    public void FaceInfo_NegativeFaceIndex_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FreeTypeFontInspector.FaceInfo(faceIndex: -1, familyName: "Example"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void FaceInfo_EmptyOrWhiteSpaceFamilyName_Throws(string familyName)
    {
        Assert.Throws<ArgumentException>(() => new FreeTypeFontInspector.FaceInfo(faceIndex: 0, familyName));
    }

    [Fact]
    public void FaceInfo_NullFamilyName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new FreeTypeFontInspector.FaceInfo(faceIndex: 0, familyName: null!));
    }
}
