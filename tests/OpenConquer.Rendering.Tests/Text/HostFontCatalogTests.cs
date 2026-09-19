using OpenConquer.Rendering.Text.Fonts;
using OpenConquer.Rendering.Text.Fonts.Discovery;
using OpenConquer.Rendering.Text.Fonts.FreeType;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class HostFontCatalogTests
{
    private static string FontFilePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Fonts", "Knewave-Regular.ttf");

    [Fact]
    public void Constructor_RegisteredFontBuildsFamilyEntry()
    {
        string root = CreateTemporaryDirectory();

        try
        {
            string fontPath = Path.Combine(root, "Knewave-Regular.ttf");
            File.Copy(FontFilePath, fontPath);

            using FreeTypeLibrary library = new();
            FreeTypeFontInspector inspector = new(library);
            HostFontDiscovery discovery = new([new HostFontReference(fontPath)]);
            HostFontCatalog catalog = new(inspector, discovery);

            string discoveredFont = Assert.Single(catalog.FontFiles);
            Assert.Equal(Path.GetFullPath(fontPath), discoveredFont);

            HostFontCatalog.Entry entry = Assert.Single(catalog.Entries);
            Assert.Equal(Path.GetFullPath(fontPath), entry.Font.FilePath);
            Assert.Equal(0, entry.Font.FaceIndex);
            Assert.Equal("Knewave", entry.FamilyName);
            Assert.False(entry.IsBold);
            Assert.False(entry.IsItalic);
            Assert.Null(catalog.DefaultGuiFont);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Constructor_ExplicitFaceIndexIncludesOnlyRegisteredFace()
    {
        string root = CreateTemporaryDirectory();

        try
        {
            string fontPath = Path.Combine(root, "Knewave-Regular.ttf");
            File.Copy(FontFilePath, fontPath);

            using FreeTypeLibrary library = new();
            FreeTypeFontInspector inspector = new(library);
            HostFontDiscovery discovery = new([new HostFontReference(fontPath, faceIndex: 0)]);
            HostFontCatalog catalog = new(inspector, discovery);

            HostFontCatalog.Entry entry = Assert.Single(catalog.Entries);
            Assert.Equal(0, entry.Font.FaceIndex);
            Assert.Equal("Knewave", entry.FamilyName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Constructor_UnregisteredFaceIndexIsExcludedFromEntries()
    {
        string root = CreateTemporaryDirectory();

        try
        {
            string fontPath = Path.Combine(root, "Knewave-Regular.ttf");
            File.Copy(FontFilePath, fontPath);

            using FreeTypeLibrary library = new();
            FreeTypeFontInspector inspector = new(library);
            HostFontDiscovery discovery = new([new HostFontReference(fontPath, faceIndex: 1)]);
            HostFontCatalog catalog = new(inspector, discovery);

            Assert.Single(catalog.FontFiles);
            Assert.Empty(catalog.Entries);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Constructor_FileReferenceWithoutFaceIndexIncludesAllInspectedFaces()
    {
        string root = CreateTemporaryDirectory();

        try
        {
            string fontPath = Path.Combine(root, "Knewave-Regular.ttf");
            File.Copy(FontFilePath, fontPath);

            using FreeTypeLibrary library = new();
            FreeTypeFontInspector inspector = new(library);
            HostFontDiscovery discovery = new([new HostFontReference(fontPath, faceIndex: 1), new HostFontReference(fontPath)]);
            HostFontCatalog catalog = new(inspector, discovery);

            HostFontCatalog.Entry entry = Assert.Single(catalog.Entries);
            Assert.Equal(0, entry.Font.FaceIndex);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Constructor_DuplicateReferencesAreCollapsed()
    {
        string root = CreateTemporaryDirectory();

        try
        {
            string fontPath = Path.Combine(root, "font.ttf");
            File.Copy(FontFilePath, fontPath);

            using FreeTypeLibrary library = new();
            FreeTypeFontInspector inspector = new(library);
            HostFontReference reference = new(fontPath);
            HostFontDiscovery discovery = new([reference, reference]);
            HostFontCatalog catalog = new(inspector, discovery);

            Assert.Single(catalog.FontFiles);
            Assert.Single(catalog.Entries);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Constructor_FontFilesAndEntriesAreDeterministicallyOrderedByPath()
    {
        string root = CreateTemporaryDirectory();

        try
        {
            string zPath = Path.Combine(root, "z.ttf");
            string aPath = Path.Combine(root, "a.ttf");
            string mPath = Path.Combine(root, "m.ttf");

            File.Copy(FontFilePath, zPath);
            File.Copy(FontFilePath, aPath);
            File.Copy(FontFilePath, mPath);

            using FreeTypeLibrary library = new();
            FreeTypeFontInspector inspector = new(library);
            HostFontDiscovery discovery = new([new HostFontReference(zPath), new HostFontReference(aPath), new HostFontReference(mPath)]);
            HostFontCatalog catalog = new(inspector, discovery);

            string[] fontFileNames = catalog.FontFiles.Select(static path => Path.GetFileName(path)!).ToArray();
            string[] entryFileNames = catalog.Entries.Select(static entry => Path.GetFileName(entry.Font.FilePath)!).ToArray();

            Assert.Equal(["a.ttf", "m.ttf", "z.ttf"], fontFileNames);
            Assert.Equal(["a.ttf", "m.ttf", "z.ttf"], entryFileNames);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Constructor_InvalidFontRemainsDiscoverableButIsNotFamilyEntry()
    {
        string root = CreateTemporaryDirectory();

        try
        {
            string invalidPath = Path.Combine(root, "invalid.ttf");
            string validPath = Path.Combine(root, "valid.ttf");

            File.WriteAllBytes(invalidPath, [0x01, 0x02, 0x03, 0x04]);
            File.Copy(FontFilePath, validPath);

            using FreeTypeLibrary library = new();
            FreeTypeFontInspector inspector = new(library);
            HostFontDiscovery discovery = new([new HostFontReference(invalidPath), new HostFontReference(validPath)]);
            HostFontCatalog catalog = new(inspector, discovery);

            string[] fontFileNames = catalog.FontFiles.Select(static path => Path.GetFileName(path)!).ToArray();

            Assert.Equal(["invalid.ttf", "valid.ttf"], fontFileNames);

            HostFontCatalog.Entry entry = Assert.Single(catalog.Entries);
            Assert.Equal("valid.ttf", Path.GetFileName(entry.Font.FilePath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Constructor_MissingFontRemainsDiscoverableButIsNotFamilyEntry()
    {
        string root = CreateTemporaryDirectory();

        try
        {
            string missingPath = Path.Combine(root, "missing.ttf");

            using FreeTypeLibrary library = new();
            FreeTypeFontInspector inspector = new(library);
            HostFontDiscovery discovery = new([new HostFontReference(missingPath)]);
            HostFontCatalog catalog = new(inspector, discovery);

            Assert.Equal(Path.GetFullPath(missingPath), Assert.Single(catalog.FontFiles));
            Assert.Empty(catalog.Entries);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Constructor_DefaultGuiFontIsIncludedWhenAbsentFromRegisteredFonts()
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

            Assert.Equal(Path.GetFullPath(fontPath), Assert.Single(catalog.FontFiles));
            Assert.Single(catalog.Entries);
            Assert.NotNull(catalog.DefaultGuiFont);
            Assert.Equal(Path.GetFullPath(fontPath), catalog.DefaultGuiFont.FilePath);
            Assert.Equal(0, catalog.DefaultGuiFont.FaceIndex);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Constructor_DefaultGuiFontWithExplicitFaceIndexRemainsCreationCandidateWhenInspectionFails()
    {
        string root = CreateTemporaryDirectory();

        try
        {
            string fontPath = Path.Combine(root, "invalid.ttf");
            File.WriteAllBytes(fontPath, [0x01, 0x02, 0x03, 0x04]);

            using FreeTypeLibrary library = new();
            FreeTypeFontInspector inspector = new(library);
            HostFontDiscovery discovery = new([], new HostFontReference(fontPath, faceIndex: 7));
            HostFontCatalog catalog = new(inspector, discovery);

            Assert.Equal(Path.GetFullPath(fontPath), Assert.Single(catalog.FontFiles));
            Assert.Empty(catalog.Entries);
            Assert.NotNull(catalog.DefaultGuiFont);
            Assert.Equal(Path.GetFullPath(fontPath), catalog.DefaultGuiFont.FilePath);
            Assert.Equal(7, catalog.DefaultGuiFont.FaceIndex);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Constructor_DefaultGuiFontWithoutFaceIndexRequiresInspectableFace()
    {
        string root = CreateTemporaryDirectory();

        try
        {
            string fontPath = Path.Combine(root, "invalid.ttf");
            File.WriteAllBytes(fontPath, [0x01, 0x02, 0x03, 0x04]);

            using FreeTypeLibrary library = new();
            FreeTypeFontInspector inspector = new(library);
            HostFontDiscovery discovery = new([], new HostFontReference(fontPath));
            HostFontCatalog catalog = new(inspector, discovery);

            Assert.Single(catalog.FontFiles);
            Assert.Empty(catalog.Entries);
            Assert.Null(catalog.DefaultGuiFont);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Constructor_NullInspector_Throws()
    {
        HostFontDiscovery discovery = new([]);

        Assert.Throws<ArgumentNullException>(() => new HostFontCatalog(null!, discovery));
    }

    [Fact]
    public void Constructor_NullDiscovery_Throws()
    {
        using FreeTypeLibrary library = new();
        FreeTypeFontInspector inspector = new(library);

        Assert.Throws<ArgumentNullException>(() => new HostFontCatalog(inspector, null!));
    }

    [Fact]
    public void Entry_PreservesValues()
    {
        ResolvedFont font = new("/fonts/example.ttf", faceIndex: 2);
        HostFontCatalog.Entry entry = new(font, "Example", isBold: true, isItalic: true);

        Assert.Same(font, entry.Font);
        Assert.Equal("Example", entry.FamilyName);
        Assert.True(entry.IsBold);
        Assert.True(entry.IsItalic);
    }

    [Fact]
    public void Entry_DefaultConstructorRepresentsRegularFace()
    {
        ResolvedFont font = new("/fonts/example.ttf", faceIndex: 0);
        HostFontCatalog.Entry entry = new(font, "Example");

        Assert.False(entry.IsBold);
        Assert.False(entry.IsItalic);
    }

    [Fact]
    public void Entry_NullFont_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new HostFontCatalog.Entry(null!, "Example"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Entry_EmptyOrWhiteSpaceFamilyName_Throws(string familyName)
    {
        ResolvedFont font = new("/fonts/example.ttf", faceIndex: 0);

        Assert.Throws<ArgumentException>(() => new HostFontCatalog.Entry(font, familyName));
    }

    [Fact]
    public void Entry_NullFamilyName_Throws()
    {
        ResolvedFont font = new("/fonts/example.ttf", faceIndex: 0);

        Assert.Throws<ArgumentNullException>(() => new HostFontCatalog.Entry(font, null!));
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"OpenConquer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
