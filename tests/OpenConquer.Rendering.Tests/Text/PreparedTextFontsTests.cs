using System.Text;
using OpenConquer.Rendering.Text.Fonts;
using OpenConquer.Rendering.Text.Fonts.FreeType;
using OpenConquer.Rendering.Text.Glyphs;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class PreparedTextFontsTests
{
    [Fact]
    public void TakenFaceSurvivesBatchDisposalAndCannotBeTakenTwice()
    {
        TestGlyphRasterizer takenFace = new(_ => (false, null));
        TestGlyphRasterizer abandonedFace = new(_ => (false, null));
        using PreparedTextFonts batch = PreparedTextFonts.Build([12, 20],
            height => height == 12 ? takenFace : abandonedFace, TestContext.Current.CancellationToken);
        using IGlyphRasterizer owned = batch.Take(0, out int height);
        Assert.Equal(12, height);
        Assert.Same(takenFace, owned);
        Assert.Throws<InvalidOperationException>(() => batch.Take(0, out _));

        batch.Dispose();

        Assert.False(owned.TryRasterizeGlyph(new Rune('A'), out _));
        Assert.Throws<ObjectDisposedException>(() => abandonedFace.TryRasterizeGlyph(new Rune('A'), out _));
        Assert.Throws<ObjectDisposedException>(() => batch.Take(1, out _));
    }

    [Fact]
    public void CancellationBetweenFacesDisposesTheFirstFace()
    {
        using CancellationTokenSource stop = new();
        TestGlyphRasterizer created = new(_ => (false, null));
        int requests = 0;

        Assert.Throws<OperationCanceledException>(() => PreparedTextFonts.Build([12, 20], _ =>
        {
            requests++;
            stop.Cancel();
            return created;
        }, stop.Token));

        Assert.Equal(1, requests);
        Assert.Throws<ObjectDisposedException>(() => created.TryRasterizeGlyph(new Rune('A'), out _));
    }

    [Fact]
    public async Task WorkerPreparedRealFaceMatchesSynchronousGlyphAfterLibraryOwnerCloses()
    {
        const int height = 16;
        CancellationToken token = TestContext.Current.CancellationToken;
        ResolvedFont font = new(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Fonts", "Knewave-Regular.ttf"), faceIndex: 0);

        using PreparedTextFonts batch = await Task.Run(() =>
        {
            using FreeTypeLibrary workerLibrary = new();
            return PreparedTextFonts.Build([height, 20], size => new FreeTypeGlyphRasterizer(workerLibrary, font, size,
                antialiasEnabled: true), token);
        }, token);

        using IGlyphRasterizer prepared = batch.Take(0, out int preparedHeight);
        batch.Dispose();
        using FreeTypeLibrary baselineLibrary = new();
        using FreeTypeGlyphRasterizer baseline = new(baselineLibrary, font, height, antialiasEnabled: true);
        Rune sample = new('A');

        Assert.Equal(height, preparedHeight);
        Assert.True(prepared.TryRasterizeGlyph(sample, out RasterizedGlyph? actual));
        Assert.True(baseline.TryRasterizeGlyph(sample, out RasterizedGlyph? expected));
        Assert.NotNull(actual);
        Assert.NotNull(expected);
        Assert.Equal((expected.WidthPixels, expected.HeightPixels, expected.AdvancePixels),
            (actual.WidthPixels, actual.HeightPixels, actual.AdvancePixels));
        Assert.Equal(expected.Coverage.ToArray(), actual.Coverage.ToArray());
    }
}
