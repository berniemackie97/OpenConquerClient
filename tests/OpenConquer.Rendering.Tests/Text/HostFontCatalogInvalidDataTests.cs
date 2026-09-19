using OpenConquer.Rendering.Text;
using OpenConquer.Rendering.Text.Fonts;
using OpenConquer.Rendering.Text.Fonts.Discovery;
using OpenConquer.Rendering.Text.Fonts.FreeType;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class HostFontCatalogInvalidDataTests
{
    [Fact]
    public void Constructor_EmptyRegisteredFontRemainsDiscoverableButIsNotFamilyEntry()
    {
        string root = Path.Combine(Path.GetTempPath(), $"OpenConquer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            string emptyFontPath = Path.Combine(root, "empty.ttf");
            File.WriteAllBytes(emptyFontPath, []);

            using FreeTypeLibrary library = new();
            FreeTypeFontInspector inspector = new(library);
            HostFontDiscovery discovery = new([new HostFontReference(emptyFontPath)]);

            HostFontCatalog catalog = new(inspector, discovery);

            Assert.Equal(Path.GetFullPath(emptyFontPath), Assert.Single(catalog.FontFiles));
            Assert.Empty(catalog.Entries);
            Assert.Null(catalog.DefaultGuiFont);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Constructor_EmptyDefaultGuiFontWithExplicitFaceIndexRemainsConcreteCandidate()
    {
        string root = Path.Combine(Path.GetTempPath(), $"OpenConquer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            string emptyFontPath = Path.Combine(root, "empty.ttf");
            File.WriteAllBytes(emptyFontPath, []);

            using FreeTypeLibrary library = new();
            FreeTypeFontInspector inspector = new(library);
            HostFontDiscovery discovery = new([], new HostFontReference(emptyFontPath, faceIndex: 0));

            HostFontCatalog catalog = new(inspector, discovery);

            Assert.Equal(Path.GetFullPath(emptyFontPath), Assert.Single(catalog.FontFiles));
            Assert.Empty(catalog.Entries);

            ResolvedFont defaultGuiFont = Assert.IsType<ResolvedFont>(catalog.DefaultGuiFont);
            Assert.Equal(Path.GetFullPath(emptyFontPath), defaultGuiFont.FilePath);
            Assert.Equal(0, defaultGuiFont.FaceIndex);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Constructor_EmptyDefaultGuiFontWithoutFaceIndexCannotResolveDefaultFace()
    {
        string root = Path.Combine(Path.GetTempPath(), $"OpenConquer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            string emptyFontPath = Path.Combine(root, "empty.ttf");
            File.WriteAllBytes(emptyFontPath, []);

            using FreeTypeLibrary library = new();
            FreeTypeFontInspector inspector = new(library);
            HostFontDiscovery discovery = new([], new HostFontReference(emptyFontPath));

            HostFontCatalog catalog = new(inspector, discovery);

            Assert.Equal(Path.GetFullPath(emptyFontPath), Assert.Single(catalog.FontFiles));
            Assert.Empty(catalog.Entries);
            Assert.Null(catalog.DefaultGuiFont);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
