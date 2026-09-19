using System.Text;
using OpenConquer.Rendering.Text.Fonts;
using OpenConquer.Rendering.Text.Fonts.Discovery;
using OpenConquer.Rendering.Text.Fonts.FreeType;
using OpenConquer.Rendering.Text.Glyphs;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class SystemFontResolverTests
{
    private static string FontFilePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Fonts", "Knewave-Regular.ttf");

    [Fact]
    public void TryResolve_FamilyName_ReturnsResolvedFace()
    {
        string root = CreateTemporaryDirectory();

        try
        {
            string fontPath = Path.Combine(root, "Knewave-Regular.ttf");
            File.Copy(FontFilePath, fontPath);

            using FreeTypeLibrary library = new();
            SystemFontResolver resolver = CreateResolver(library, [new HostFontReference(fontPath)]);

            Assert.True(resolver.TryResolve("Knewave", out ResolvedFont? font));

            Assert.NotNull(font);
            Assert.Equal(Path.GetFullPath(fontPath), font.FilePath);
            Assert.Equal(0, font.FaceIndex);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryResolve_FamilyName_IsCaseInsensitive()
    {
        string root = CreateTemporaryDirectory();

        try
        {
            string fontPath = Path.Combine(root, "Knewave-Regular.ttf");
            File.Copy(FontFilePath, fontPath);

            using FreeTypeLibrary library = new();
            SystemFontResolver resolver = CreateResolver(library, [new HostFontReference(fontPath)]);

            Assert.True(resolver.TryResolve("kNeWaVe", out ResolvedFont? font));
            Assert.NotNull(font);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryResolve_FamilyName_PreservesResolvedFaceIndex()
    {
        ResolvedFont expected = new("/fonts/family.ttc", faceIndex: 3);
        HostFontCatalog.Entry entry = new(expected, "Example");
        SystemFontResolver resolver = new([], [entry]);

        Assert.True(resolver.TryResolve("Example", out ResolvedFont? font));
        Assert.Same(expected, font);
    }

    [Fact]
    public void TryResolve_FamilyName_PrefersRegularFaceOverEarlierStyledFace()
    {
        HostFontCatalog.Entry bold = new(new ResolvedFont("/fonts/a-bold.ttf", faceIndex: 0), "Example", isBold: true, isItalic: false);
        HostFontCatalog.Entry italic = new(new ResolvedFont("/fonts/b-italic.ttf", faceIndex: 0), "Example", isBold: false, isItalic: true);
        ResolvedFont regularFont = new("/fonts/z-regular.ttf", faceIndex: 0);
        HostFontCatalog.Entry regular = new(regularFont, "Example");
        SystemFontResolver resolver = new([], [bold, italic, regular]);

        Assert.True(resolver.TryResolve("Example", out ResolvedFont? font));
        Assert.Same(regularFont, font);
    }

    [Fact]
    public void TryResolve_FamilyName_MultipleRegularFacesUseExistingDeterministicOrder()
    {
        ResolvedFont firstFont = new("/fonts/a-regular.ttf", faceIndex: 0);
        HostFontCatalog.Entry first = new(firstFont, "Example");
        HostFontCatalog.Entry second = new(new ResolvedFont("/fonts/b-regular.ttf", faceIndex: 0), "Example");
        SystemFontResolver resolver = new([], [first, second]);

        Assert.True(resolver.TryResolve("Example", out ResolvedFont? font));
        Assert.Same(firstFont, font);
    }

    [Fact]
    public void TryResolve_FamilyName_NoRegularFaceUsesExistingDeterministicOrder()
    {
        ResolvedFont firstFont = new("/fonts/a-bold.ttf", faceIndex: 0);
        HostFontCatalog.Entry first = new(firstFont, "Example", isBold: true, isItalic: false);
        HostFontCatalog.Entry second = new(new ResolvedFont("/fonts/b-italic.ttf", faceIndex: 0), "Example", isBold: false, isItalic: true);
        SystemFontResolver resolver = new([], [first, second]);

        Assert.True(resolver.TryResolve("Example", out ResolvedFont? font));
        Assert.Same(firstFont, font);
    }

    [Fact]
    public void TryResolve_NativeFileToken_ReturnsFaceZero()
    {
        string fontPath = Path.Combine(Path.GetTempPath(), "Knewave-Regular.ttf");
        SystemFontResolver resolver = new([fontPath], []);

        Assert.True(resolver.TryResolve("$Knewave-Regular.ttf", out ResolvedFont? font));

        Assert.NotNull(font);
        Assert.Equal(fontPath, font.FilePath);
        Assert.Equal(0, font.FaceIndex);
    }

    [Fact]
    public void TryResolve_NativeFileToken_IsCaseInsensitive()
    {
        string fontPath = Path.Combine(Path.GetTempPath(), "Knewave-Regular.ttf");
        SystemFontResolver resolver = new([fontPath], []);

        Assert.True(resolver.TryResolve("$kNeWaVe-rEgUlAr.TTF", out ResolvedFont? font));
        Assert.NotNull(font);
    }

    [Fact]
    public void TryResolve_DuplicateFileNamesUseExistingDeterministicOrder()
    {
        string firstPath = Path.Combine(Path.GetTempPath(), "a", "Knewave-Regular.ttf");
        string secondPath = Path.Combine(Path.GetTempPath(), "b", "Knewave-Regular.ttf");
        SystemFontResolver resolver = new([firstPath, secondPath], []);

        Assert.True(resolver.TryResolve("$Knewave-Regular.ttf", out ResolvedFont? font));

        Assert.NotNull(font);
        Assert.Equal(firstPath, font.FilePath);
        Assert.Equal(0, font.FaceIndex);
    }

    [Theory]
    [InlineData("$")]
    [InlineData("$font")]
    [InlineData("$font.tt")]
    [InlineData("$font.ttff")]
    [InlineData("$font.otf")]
    public void TryResolve_InvalidNativeFileToken_ReturnsFalse(string fontToken)
    {
        SystemFontResolver resolver = new(["/fonts/font.otf"], []);

        Assert.False(resolver.TryResolve(fontToken, out ResolvedFont? font));
        Assert.Null(font);
    }

    [Theory]
    [InlineData("$../Knewave-Regular.ttf")]
    [InlineData("$nested/Knewave-Regular.ttf")]
    [InlineData("$nested\\Knewave-Regular.ttf")]
    public void TryResolve_NativeFileTokenContainingDirectorySeparators_ReturnsFalse(string fontToken)
    {
        SystemFontResolver resolver = new(["/fonts/Knewave-Regular.ttf"], []);

        Assert.False(resolver.TryResolve(fontToken, out ResolvedFont? font));
        Assert.Null(font);
    }

    [Fact]
    public void TryResolve_MissingFamily_ReturnsFalse()
    {
        SystemFontResolver resolver = new([], []);

        Assert.False(resolver.TryResolve("Missing Font", out ResolvedFont? font));
        Assert.Null(font);
    }

    [Fact]
    public void TryResolve_EmptyToken_ReturnsFalse()
    {
        SystemFontResolver resolver = new([], []);

        Assert.False(resolver.TryResolve(string.Empty, out ResolvedFont? font));
        Assert.Null(font);
    }

    [Fact]
    public void TryResolve_NullToken_Throws()
    {
        SystemFontResolver resolver = new([], []);

        Assert.Throws<ArgumentNullException>(() => resolver.TryResolve(null!, out _));
    }

    [Fact]
    public void TryResolveDefaultGuiFont_ConfiguredFont_ReturnsResolvedFont()
    {
        ResolvedFont expected = new("/fonts/default.ttf", faceIndex: 2);
        SystemFontResolver resolver = new([], [], expected);

        Assert.True(resolver.TryResolveDefaultGuiFont(out ResolvedFont? font));
        Assert.Same(expected, font);
    }

    [Fact]
    public void TryResolveDefaultGuiFont_NoConfiguredFont_ReturnsFalse()
    {
        SystemFontResolver resolver = new([], []);

        Assert.False(resolver.TryResolveDefaultGuiFont(out ResolvedFont? font));
        Assert.Null(font);
    }

    [Fact]
    public void ProductionConstructor_PreservesCatalogDefaultGuiFont()
    {
        string root = CreateTemporaryDirectory();

        try
        {
            string fontPath = Path.Combine(root, "default.ttf");
            File.Copy(FontFilePath, fontPath);

            using FreeTypeLibrary library = new();
            FreeTypeFontInspector inspector = new(library);
            HostFontDiscovery discovery = new([], new HostFontReference(fontPath));
            HostFontCatalog catalog = new(inspector, discovery);
            SystemFontResolver resolver = new(catalog);

            Assert.True(resolver.TryResolveDefaultGuiFont(out ResolvedFont? font));

            Assert.NotNull(font);
            Assert.Equal(Path.GetFullPath(fontPath), font.FilePath);
            Assert.Equal(0, font.FaceIndex);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ProductionConstructor_NullCatalog_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SystemFontResolver(null!));
    }

    [Fact]
    public void InternalConstructor_NullFontFiles_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SystemFontResolver(null!, []));
    }

    [Fact]
    public void InternalConstructor_NullEntries_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SystemFontResolver([], null!));
    }

    [Fact]
    public void InternalConstructor_WhiteSpaceFontFile_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SystemFontResolver([" "], []));
    }

    [Fact]
    public void InternalConstructor_NullEntry_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SystemFontResolver([], [null!]));
    }

    [Fact]
    public void ResolvedFamilyFace_CanRasterizeGlyph()
    {
        string root = CreateTemporaryDirectory();

        try
        {
            string fontPath = Path.Combine(root, "Knewave-Regular.ttf");
            File.Copy(FontFilePath, fontPath);

            using FreeTypeLibrary library = new();
            SystemFontResolver resolver = CreateResolver(library, [new HostFontReference(fontPath)]);

            Assert.True(resolver.TryResolve("Knewave", out ResolvedFont? font));
            Assert.NotNull(font);

            using FreeTypeGlyphRasterizer rasterizer = new(library, font, nominalPixelHeight: 16, antialiasEnabled: true);

            Assert.True(rasterizer.TryRasterizeGlyph(new Rune('A'), out RasterizedGlyph? glyph));

            Assert.NotNull(glyph);
            Assert.True(glyph.WidthPixels > 0);
            Assert.True(glyph.HeightPixels > 0);
            Assert.True(glyph.AdvancePixels > 0);
            Assert.Equal(glyph.WidthPixels * glyph.HeightPixels, glyph.Coverage.Length);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static SystemFontResolver CreateResolver(FreeTypeLibrary library, IReadOnlyList<HostFontReference> references)
    {
        FreeTypeFontInspector inspector = new(library);
        HostFontDiscovery discovery = new(references);
        HostFontCatalog catalog = new(inspector, discovery);
        return new SystemFontResolver(catalog);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"OpenConquer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
