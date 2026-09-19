using System.Text;
using OpenConquer.Rendering.Text;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class FreeTypeGlyphRasterizerFactoryTests
{
    private static string FontFilePath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Fonts", "Knewave-Regular.ttf");

    [Fact]
    public void Create_RequestedFontSucceeds_ReturnsRequestedRasterizer()
    {
        ResolvedFont requestedFont = new("/fonts/requested.ttf", faceIndex: 2);
        FakeFontResolver resolver = new();
        resolver.Add("Requested", requestedFont);
        FakeGlyphRasterizer expected = new();

        FreeTypeGlyphRasterizerFactory factory = new(
            resolver,
            (font, nominalPixelHeight, antialiasEnabled) =>
            {
                Assert.Same(requestedFont, font);
                Assert.Equal(16, nominalPixelHeight);
                Assert.True(antialiasEnabled);
                return expected;
            }
        );

        IGlyphRasterizer rasterizer = factory.Create(
            "Requested",
            nominalPixelHeight: 16,
            antialiasEnabled: true
        );

        Assert.Same(expected, rasterizer);
        Assert.Equal(["Requested"], resolver.ResolvedTokens);
        Assert.Equal(0, resolver.DefaultGuiResolveCount);
    }

    [Fact]
    public void Create_NullRequestedToken_StartsWithDefaultGuiFont()
    {
        ResolvedFont defaultGuiFont = new("/fonts/default.ttf", faceIndex: 1);
        FakeFontResolver resolver = new(defaultGuiFont);
        FakeGlyphRasterizer expected = new();
        FreeTypeGlyphRasterizerFactory factory = new(
            resolver,
            (font, _, _) =>
            {
                Assert.Same(defaultGuiFont, font);
                return expected;
            }
        );

        IGlyphRasterizer rasterizer = factory.Create(
            null,
            nominalPixelHeight: 16,
            antialiasEnabled: true
        );

        Assert.Same(expected, rasterizer);
        Assert.Empty(resolver.ResolvedTokens);
        Assert.Equal(1, resolver.DefaultGuiResolveCount);
    }

    [Fact]
    public void Create_EmptyRequestedToken_StartsWithDefaultGuiFont()
    {
        ResolvedFont defaultGuiFont = new("/fonts/default.ttf", faceIndex: 1);
        FakeFontResolver resolver = new(defaultGuiFont);
        FakeGlyphRasterizer expected = new();
        FreeTypeGlyphRasterizerFactory factory = new(resolver, (_, _, _) => expected);

        IGlyphRasterizer rasterizer = factory.Create(
            string.Empty,
            nominalPixelHeight: 16,
            antialiasEnabled: true
        );

        Assert.Same(expected, rasterizer);
        Assert.Empty(resolver.ResolvedTokens);
        Assert.Equal(1, resolver.DefaultGuiResolveCount);
    }

    [Fact]
    public void Create_WhiteSpaceRequestedToken_IsAttempted()
    {
        ResolvedFont requestedFont = new("/fonts/requested.ttf", faceIndex: 0);
        FakeFontResolver resolver = new();
        resolver.Add(" ", requestedFont);
        FakeGlyphRasterizer expected = new();
        FreeTypeGlyphRasterizerFactory factory = new(resolver, (_, _, _) => expected);

        IGlyphRasterizer rasterizer = factory.Create(
            " ",
            nominalPixelHeight: 16,
            antialiasEnabled: true
        );

        Assert.Same(expected, rasterizer);
        Assert.Equal([" "], resolver.ResolvedTokens);
        Assert.Equal(0, resolver.DefaultGuiResolveCount);
    }

    [Fact]
    public void Create_UsesNativeFallbackOrder()
    {
        ResolvedFont requestedFont = new("/fonts/requested.ttf", faceIndex: 0);
        ResolvedFont defaultGuiFont = new("/fonts/default.ttf", faceIndex: 1);
        ResolvedFont simSunCollection = new("/fonts/simsun.ttc", faceIndex: 0);
        ResolvedFont simSunFont = new("/fonts/simsun.ttf", faceIndex: 0);
        ResolvedFont courierNew = new("/fonts/courier.ttf", faceIndex: 0);

        FakeFontResolver resolver = new(defaultGuiFont);
        resolver.Add("Requested", requestedFont);
        resolver.Add("$simsun.ttc", simSunCollection);
        resolver.Add("$simsun.ttf", simSunFont);
        resolver.Add("Courier New", courierNew);

        List<ResolvedFont> creationAttempts = [];
        FakeGlyphRasterizer expected = new();

        FreeTypeGlyphRasterizerFactory factory = new(
            resolver,
            (font, _, _) =>
            {
                creationAttempts.Add(font);

                if (ReferenceEquals(font, courierNew))
                {
                    return expected;
                }

                throw new FontFaceCreationException("Expected creation failure.");
            }
        );

        IGlyphRasterizer rasterizer = factory.Create(
            "Requested",
            nominalPixelHeight: 16,
            antialiasEnabled: true
        );

        Assert.Same(expected, rasterizer);
        Assert.Equal(
            ["Requested", "$simsun.ttc", "$simsun.ttf", "Courier New"],
            resolver.ResolvedTokens
        );
        Assert.Equal(1, resolver.DefaultGuiResolveCount);
        Assert.Equal(
            [requestedFont, defaultGuiFont, simSunCollection, simSunFont, courierNew],
            creationAttempts
        );
    }

    [Fact]
    public void Create_UnresolvedCandidatesAdvanceThroughFallbackChain()
    {
        ResolvedFont courierNew = new("/fonts/courier.ttf", faceIndex: 0);
        FakeFontResolver resolver = new();
        resolver.Add("Courier New", courierNew);
        FakeGlyphRasterizer expected = new();
        FreeTypeGlyphRasterizerFactory factory = new(resolver, (_, _, _) => expected);

        IGlyphRasterizer rasterizer = factory.Create(
            "Missing",
            nominalPixelHeight: 16,
            antialiasEnabled: true
        );

        Assert.Same(expected, rasterizer);
        Assert.Equal(
            ["Missing", "$simsun.ttc", "$simsun.ttf", "Courier New"],
            resolver.ResolvedTokens
        );
        Assert.Equal(1, resolver.DefaultGuiResolveCount);
    }

    [Fact]
    public void Create_FontFaceCreationFailure_AdvancesToNextCandidate()
    {
        AssertRecoverableFailureAdvances(new FontFaceCreationException("Face failure."));
    }

    [Fact]
    public void Create_InvalidDataFailure_AdvancesToNextCandidate()
    {
        AssertRecoverableFailureAdvances(new InvalidDataException("Invalid font data."));
    }

    [Fact]
    public void Create_IoFailure_AdvancesToNextCandidate()
    {
        AssertRecoverableFailureAdvances(new IOException("I/O failure."));
    }

    [Fact]
    public void Create_UnauthorizedAccessFailure_AdvancesToNextCandidate()
    {
        AssertRecoverableFailureAdvances(new UnauthorizedAccessException("Access denied."));
    }

    [Fact]
    public void Create_UnexpectedCreationFailure_PropagatesImmediately()
    {
        ResolvedFont requestedFont = new("/fonts/requested.ttf", faceIndex: 0);
        ResolvedFont fallbackFont = new("/fonts/fallback.ttf", faceIndex: 0);
        FakeFontResolver resolver = new(fallbackFont);
        resolver.Add("Requested", requestedFont);
        InvalidOperationException expected = new("Unexpected failure.");
        int creationAttempts = 0;

        FreeTypeGlyphRasterizerFactory factory = new(
            resolver,
            (_, _, _) =>
            {
                creationAttempts++;
                throw expected;
            }
        );

        InvalidOperationException actual = Assert.Throws<InvalidOperationException>(() =>
            factory.Create("Requested", nominalPixelHeight: 16, antialiasEnabled: true)
        );

        Assert.Same(expected, actual);
        Assert.Equal(1, creationAttempts);
        Assert.Equal(0, resolver.DefaultGuiResolveCount);
    }

    [Fact]
    public void Create_CreatorReturningNull_ThrowsImmediately()
    {
        ResolvedFont requestedFont = new("/fonts/requested.ttf", faceIndex: 0);
        FakeFontResolver resolver = new();
        resolver.Add("Requested", requestedFont);
        FreeTypeGlyphRasterizerFactory factory = new(resolver, (_, _, _) => null!);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            factory.Create("Requested", nominalPixelHeight: 16, antialiasEnabled: true)
        );

        Assert.Equal("Glyph rasterizer creator returned null.", exception.Message);
        Assert.Equal(0, resolver.DefaultGuiResolveCount);
    }

    [Fact]
    public void Create_AllCandidatesFail_ThrowsWithLastCreationFailure()
    {
        ResolvedFont requestedFont = new("/fonts/requested.ttf", faceIndex: 0);
        ResolvedFont defaultGuiFont = new("/fonts/default.ttf", faceIndex: 0);
        ResolvedFont simSunCollection = new("/fonts/simsun.ttc", faceIndex: 0);
        ResolvedFont simSunFont = new("/fonts/simsun.ttf", faceIndex: 0);
        ResolvedFont courierNew = new("/fonts/courier.ttf", faceIndex: 0);

        FakeFontResolver resolver = new(defaultGuiFont);
        resolver.Add("Requested", requestedFont);
        resolver.Add("$simsun.ttc", simSunCollection);
        resolver.Add("$simsun.ttf", simSunFont);
        resolver.Add("Courier New", courierNew);

        UnauthorizedAccessException lastFailure = new("Final failure.");

        FreeTypeGlyphRasterizerFactory factory = new(
            resolver,
            (font, _, _) =>
            {
                if (ReferenceEquals(font, courierNew))
                {
                    throw lastFailure;
                }

                throw new FontFaceCreationException("Earlier failure.");
            }
        );

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            factory.Create("Requested", nominalPixelHeight: 16, antialiasEnabled: true)
        );

        Assert.Equal(
            "No usable font could be created from the requested font or the native fallback chain.",
            exception.Message
        );
        Assert.Same(lastFailure, exception.InnerException);
    }

    [Fact]
    public void Create_AllCandidatesUnresolved_ThrowsWithoutInnerException()
    {
        FakeFontResolver resolver = new();
        FreeTypeGlyphRasterizerFactory factory = new(
            resolver,
            (_, _, _) => throw new InvalidOperationException("Creator must not run.")
        );

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            factory.Create("Missing", nominalPixelHeight: 16, antialiasEnabled: true)
        );

        Assert.Null(exception.InnerException);
        Assert.Equal(
            ["Missing", "$simsun.ttc", "$simsun.ttf", "Courier New"],
            resolver.ResolvedTokens
        );
        Assert.Equal(1, resolver.DefaultGuiResolveCount);
    }

    [Fact]
    public void Create_DoesNotDeduplicateResolvedFallbackCandidates()
    {
        ResolvedFont sharedFont = new("/fonts/shared.ttf", faceIndex: 0);
        FakeFontResolver resolver = new(sharedFont);
        resolver.Add("Requested", sharedFont);
        resolver.Add("$simsun.ttc", sharedFont);
        resolver.Add("$simsun.ttf", sharedFont);
        resolver.Add("Courier New", sharedFont);

        int creationAttempts = 0;
        FakeGlyphRasterizer expected = new();

        FreeTypeGlyphRasterizerFactory factory = new(
            resolver,
            (_, _, _) =>
            {
                creationAttempts++;

                if (creationAttempts == 5)
                {
                    return expected;
                }

                throw new FontFaceCreationException("Expected creation failure.");
            }
        );

        IGlyphRasterizer rasterizer = factory.Create(
            "Requested",
            nominalPixelHeight: 16,
            antialiasEnabled: true
        );

        Assert.Same(expected, rasterizer);
        Assert.Equal(5, creationAttempts);
    }

    [Fact]
    public void Create_NegativeNominalHeight_IsPassedToCreator()
    {
        ResolvedFont requestedFont = new("/fonts/requested.ttf", faceIndex: 0);
        FakeFontResolver resolver = new();
        resolver.Add("Requested", requestedFont);
        FakeGlyphRasterizer expected = new();

        FreeTypeGlyphRasterizerFactory factory = new(
            resolver,
            (_, nominalPixelHeight, antialiasEnabled) =>
            {
                Assert.Equal(-1, nominalPixelHeight);
                Assert.False(antialiasEnabled);
                return expected;
            }
        );

        IGlyphRasterizer rasterizer = factory.Create(
            "Requested",
            nominalPixelHeight: -1,
            antialiasEnabled: false
        );

        Assert.Same(expected, rasterizer);
    }

    [Fact]
    public void Create_ZeroNominalHeight_ThrowsBeforeResolution()
    {
        FakeFontResolver resolver = new();
        FreeTypeGlyphRasterizerFactory factory = new(
            resolver,
            (_, _, _) => throw new InvalidOperationException("Creator must not run.")
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            factory.Create("Requested", nominalPixelHeight: 0, antialiasEnabled: true)
        );

        Assert.Empty(resolver.ResolvedTokens);
        Assert.Equal(0, resolver.DefaultGuiResolveCount);
    }

    [Fact]
    public void ProductionFactory_CreatesUsableRasterizer()
    {
        ResolvedFont font = new(FontFilePath, faceIndex: 0);
        HostFontCatalog.Entry entry = new(font, "Knewave");
        SystemFontResolver resolver = new([FontFilePath], [entry]);

        using FreeTypeLibrary library = new();
        FreeTypeGlyphRasterizerFactory factory = new(library, resolver);
        using IGlyphRasterizer rasterizer = factory.Create(
            "Knewave",
            nominalPixelHeight: 16,
            antialiasEnabled: true
        );

        Assert.True(rasterizer.TryRasterizeGlyph(new Rune('A'), out RasterizedGlyph? glyph));
        Assert.NotNull(glyph);
    }

    [Fact]
    public void Constructor_NullLibrary_Throws()
    {
        SystemFontResolver resolver = new([], []);

        Assert.Throws<ArgumentNullException>(() =>
            new FreeTypeGlyphRasterizerFactory(null!, resolver)
        );
    }

    [Fact]
    public void Constructor_NullFontResolver_Throws()
    {
        using FreeTypeLibrary library = new();

        Assert.Throws<ArgumentNullException>(() =>
            new FreeTypeGlyphRasterizerFactory(library, null!)
        );
    }

    [Fact]
    public void InternalConstructor_NullFontResolver_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new FreeTypeGlyphRasterizerFactory(null!, (_, _, _) => new FakeGlyphRasterizer())
        );
    }

    [Fact]
    public void InternalConstructor_NullCreator_Throws()
    {
        FakeFontResolver resolver = new();

        Assert.Throws<ArgumentNullException>(() =>
            new FreeTypeGlyphRasterizerFactory(resolver, null!)
        );
    }

    private static void AssertRecoverableFailureAdvances(Exception failure)
    {
        ResolvedFont requestedFont = new("/fonts/requested.ttf", faceIndex: 0);
        ResolvedFont defaultGuiFont = new("/fonts/default.ttf", faceIndex: 0);
        FakeFontResolver resolver = new(defaultGuiFont);
        resolver.Add("Requested", requestedFont);
        FakeGlyphRasterizer expected = new();
        int creationAttempts = 0;

        FreeTypeGlyphRasterizerFactory factory = new(
            resolver,
            (font, _, _) =>
            {
                creationAttempts++;

                if (ReferenceEquals(font, requestedFont))
                {
                    throw failure;
                }

                Assert.Same(defaultGuiFont, font);
                return expected;
            }
        );

        IGlyphRasterizer rasterizer = factory.Create(
            "Requested",
            nominalPixelHeight: 16,
            antialiasEnabled: true
        );

        Assert.Same(expected, rasterizer);
        Assert.Equal(2, creationAttempts);
        Assert.Equal(1, resolver.DefaultGuiResolveCount);
    }

    private sealed class FakeFontResolver : IFontResolver
    {
        private readonly Dictionary<string, ResolvedFont> _fonts = new(StringComparer.Ordinal);
        private readonly ResolvedFont? _defaultGuiFont;

        public FakeFontResolver(ResolvedFont? defaultGuiFont = null)
        {
            _defaultGuiFont = defaultGuiFont;
        }

        public List<string> ResolvedTokens { get; } = [];

        public int DefaultGuiResolveCount
        {
            get; private set;
        }

        public void Add(string fontToken, ResolvedFont font)
        {
            _fonts.Add(fontToken, font);
        }

        public bool TryResolve(string fontToken, out ResolvedFont? font)
        {
            ResolvedTokens.Add(fontToken);
            return _fonts.TryGetValue(fontToken, out font);
        }

        public bool TryResolveDefaultGuiFont(out ResolvedFont? font)
        {
            DefaultGuiResolveCount++;
            font = _defaultGuiFont;
            return font is not null;
        }
    }

    private sealed class FakeGlyphRasterizer : IGlyphRasterizer
    {
        public bool AntialiasEnabled => true;

        public bool TryRasterizeGlyph(Rune character, out RasterizedGlyph? glyph)
        {
            glyph = null;
            return false;
        }

        public void Dispose()
        {
        }
    }
}
