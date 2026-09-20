using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Atlas;
using OpenConquer.Rendering.Text.Glyphs;
using OpenConquer.Rendering.Text.Layout;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class NativeTextBatchPlanTests
{
    private static readonly NativeTextRenderOptions s_normalOptions = new(
        NativeTextRenderStyle.Normal,
        new SpriteColor(10, 20, 30, 40),
        new SpriteColor(50, 60, 70, 80),
        0,
        0,
        NativeTextVertexColors.Solid(new SpriteColor(90, 100, 110, 120))
    );

    [Fact]
    public void Constructor_RejectsNonPositiveWorkBudget()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NativeTextBatchPlan(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NativeTextBatchPlan(-1));
    }

    [Fact]
    public void Prepare_EmptyLayoutWithExistingAtlasPagesProducesPreparedEmptyPlan()
    {
        NativeTextLayoutSource source = CreateSourceWithPages(2);
        NativeTextLayout layout = new(source, 0, 12, []);
        NativeTextBatchPlan plan = new(16);

        plan.Prepare(layout, s_normalOptions);

        Assert.Equal(16, plan.MaximumGlyphPasses);
        Assert.Equal(0, plan.GlyphCount);
        Assert.Equal(0, plan.GlyphPassCount);
        Assert.Equal(0, plan.UsedPageCount);
        Assert.True(plan.GetGlyphItemIndicesForPage(0).IsEmpty);
        Assert.True(plan.GetGlyphItemIndicesForPage(1).IsEmpty);
        Assert.Throws<ArgumentOutOfRangeException>(() => plan.GetUsedPageIndex(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => plan.GetGlyphItemIndicesForPage(2).ToArray());
    }

    [Fact]
    public void Prepare_GroupsGlyphsByAscendingPageWhilePreservingOrderWithinPage()
    {
        NativeTextLayoutSource source = CreateSourceWithPages(3);

        NativeTextLayoutItem[] items =
        [
            CreateGlyph(pageIndex: 2, glyphKey: 0x0041),
            CreateGlyph(pageIndex: 0, glyphKey: 0x0042),
            CreateGlyph(pageIndex: 2, glyphKey: 0x0043),
            CreateGlyph(pageIndex: 1, glyphKey: 0x0044),
            CreateGlyph(pageIndex: 0, glyphKey: 0x0045),
        ];

        NativeTextLayout layout = new(source, 5, 12, items);
        NativeTextBatchPlan plan = new(32);

        plan.Prepare(layout, s_normalOptions);

        Assert.Equal(5, plan.GlyphCount);
        Assert.Equal(5, plan.GlyphPassCount);
        Assert.Equal(3, plan.UsedPageCount);

        Assert.Equal(0, plan.GetUsedPageIndex(0));
        Assert.Equal(1, plan.GetUsedPageIndex(1));
        Assert.Equal(2, plan.GetUsedPageIndex(2));

        Assert.Equal([1, 4], plan.GetGlyphItemIndicesForPage(0).ToArray());
        Assert.Equal([3], plan.GetGlyphItemIndicesForPage(1).ToArray());
        Assert.Equal([0, 2], plan.GetGlyphItemIndicesForPage(2).ToArray());
    }

    [Fact]
    public void Prepare_ComputesGlyphPassCountFromResolvedStyle()
    {
        NativeTextLayoutSource source = CreateSourceWithPages(1);
        NativeTextLayout layout = new(source, 2, 12, [CreateGlyph(0, 0x0041), CreateGlyph(0, 0x0042)]);
        NativeTextRenderOptions options = new(
            NativeTextRenderStyle.MultiOffsetOutline,
            new SpriteColor(1, 2, 3, 4),
            new SpriteColor(5, 6, 7, 8),
            0,
            0,
            NativeTextVertexColors.Solid(new SpriteColor(9, 10, 11, 12))
        );
        NativeTextBatchPlan plan = new(32);

        plan.Prepare(layout, options);

        Assert.Equal(2, plan.GlyphCount);
        Assert.Equal(18, plan.GlyphPassCount);
        Assert.Equal(9, plan.PassSequence.Count);
    }

    [Fact]
    public void Prepare_DataIconThrowsBeforePlanBecomesReadable()
    {
        NativeTextLayoutSource source = CreateSourceWithPages(1);
        NativeTextLayout layout = new(source, 16, 12, [NativeTextLayoutItem.CreateDataIcon(7, 0, 0, 16)]);
        NativeTextBatchPlan plan = new(16);

        Assert.Throws<NotSupportedException>(() => plan.Prepare(layout, s_normalOptions));
        Assert.Throws<InvalidOperationException>(() => plan.GetUsedPageIndex(0));
        Assert.Throws<InvalidOperationException>(() => plan.GetGlyphItemIndicesForPage(0).ToArray());
    }

    [Fact]
    public void Prepare_GlyphReferencingMissingAtlasPageThrowsBeforePlanBecomesReadable()
    {
        NativeTextLayoutSource source = CreateSourceWithPages(1);
        NativeTextLayout layout = new(source, 1, 12, [CreateGlyph(pageIndex: 1, glyphKey: 0x0041)]);
        NativeTextBatchPlan plan = new(16);

        Assert.Throws<InvalidOperationException>(() => plan.Prepare(layout, s_normalOptions));
        Assert.Throws<InvalidOperationException>(() => plan.GetUsedPageIndex(0));
    }

    [Fact]
    public void Prepare_WorkBudgetExceededThrowsBeforePlanBecomesReadable()
    {
        NativeTextLayoutSource source = CreateSourceWithPages(1);
        NativeTextLayout layout = new(source, 2, 12, [CreateGlyph(0, 0x0041), CreateGlyph(0, 0x0042)]);
        NativeTextRenderOptions options = new(
            NativeTextRenderStyle.MultiOffsetOutline,
            new SpriteColor(1, 2, 3, 4),
            new SpriteColor(5, 6, 7, 8),
            0,
            0,
            NativeTextVertexColors.Solid(new SpriteColor(9, 10, 11, 12))
        );
        NativeTextBatchPlan plan = new(17);

        Assert.Throws<InvalidOperationException>(() => plan.Prepare(layout, options));
        Assert.Throws<InvalidOperationException>(() => plan.GetUsedPageIndex(0));
    }

    [Fact]
    public void Prepare_AfterFailureCanBeReusedSuccessfully()
    {
        NativeTextLayoutSource source = CreateSourceWithPages(1);
        NativeTextLayout invalidLayout = new(source, 16, 12, [NativeTextLayoutItem.CreateDataIcon(7, 0, 0, 16)]);
        NativeTextLayout validLayout = new(source, 1, 12, [CreateGlyph(0, 0x0041)]);
        NativeTextBatchPlan plan = new(16);

        Assert.Throws<NotSupportedException>(() => plan.Prepare(invalidLayout, s_normalOptions));

        plan.Prepare(validLayout, s_normalOptions);

        Assert.Equal(1, plan.GlyphCount);
        Assert.Equal(1, plan.GlyphPassCount);
        Assert.Equal(1, plan.UsedPageCount);
        Assert.Equal(0, plan.GetUsedPageIndex(0));
        Assert.Equal([0], plan.GetGlyphItemIndicesForPage(0).ToArray());
    }

    [Fact]
    public void PreparedPlan_ValidatesPageAccess()
    {
        NativeTextLayoutSource source = CreateSourceWithPages(2);
        NativeTextLayout layout = new(source, 1, 12, [CreateGlyph(1, 0x0041)]);
        NativeTextBatchPlan plan = new(16);

        plan.Prepare(layout, s_normalOptions);

        Assert.Throws<ArgumentOutOfRangeException>(() => plan.GetUsedPageIndex(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => plan.GetUsedPageIndex(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => plan.GetGlyphItemIndicesForPage(-1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => plan.GetGlyphItemIndicesForPage(2).ToArray());
        Assert.True(plan.GetGlyphItemIndicesForPage(0).IsEmpty);
        Assert.Equal([0], plan.GetGlyphItemIndicesForPage(1).ToArray());
    }

    [Fact]
    public void WarmedPrepare_ReusesExistingStorageWithoutManagedAllocation()
    {
        NativeTextLayoutSource source = CreateSourceWithPages(2);
        NativeTextLayout layout = new(
            source,
            4,
            12,
            [
                CreateGlyph(1, 0x0041),
                CreateGlyph(0, 0x0042),
                CreateGlyph(1, 0x0043),
                CreateGlyph(0, 0x0044),
            ]
        );
        NativeTextBatchPlan plan = new(64);

        plan.Prepare(layout, s_normalOptions);
        plan.Prepare(layout, s_normalOptions);

        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int iteration = 0; iteration < 256; iteration++)
        {
            plan.Prepare(layout, s_normalOptions);
        }

        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0L, allocatedBytes);
    }

    private static NativeTextLayoutSource CreateSourceWithPages(int pageCount)
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        GlyphAtlas atlas = new();

        for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            RasterizedGlyph glyph = new(
                GlyphAtlasPage.SizePixels,
                GlyphAtlasPage.SizePixels,
                bearingLeftPixels: 0,
                topOffsetPixels: 0,
                advancePixels: GlyphAtlasPage.SizePixels,
                new byte[GlyphAtlasPage.SizePixels * GlyphAtlasPage.SizePixels]
            );

            GlyphAtlasRegion? region = atlas.Add(glyph);
            Assert.NotNull(region);
            Assert.Equal(pageIndex, region.Value.PageIndex);
        }

        return new NativeTextLayoutSource(font, atlas);
    }

    private static NativeTextLayoutItem CreateGlyph(int pageIndex, ushort glyphKey)
    {
        return NativeTextLayoutItem.CreateGlyph(glyphKey, 0, 0, new GlyphAtlasRegion(pageIndex, 0, 0, 1, 1));
    }
}
