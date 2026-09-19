using OpenConquer.Rendering.Text.Atlas;
using OpenConquer.Rendering.Text.Layout;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class NativeTextGeometryBuilderTests
{
    [Fact]
    public void WriteGlyph_WritesLogicalGeometryWithVerifiedTriangleDiagonal()
    {
        GlyphAtlasRegion region = new(pageIndex: 0, xPixels: 64, yPixels: 128, widthPixels: 16, heightPixels: 24);
        NativeTextLayoutItem item = NativeTextLayoutItem.CreateGlyph(glyphKey: 0x0041, xPixels: 10, yPixels: 20, region);
        NativeTextVertexColors colors = new(new SpriteColor(1, 2, 3, 4),
            new SpriteColor(5, 6, 7, 8),
            new SpriteColor(9, 10, 11, 12),
            new SpriteColor(13, 14, 15, 16));
        NativeTextRenderPass pass = new(OffsetXPixels: 3, OffsetYPixels: -2, colors);
        NativeTextVertex[] vertices = new NativeTextVertex[NativeTextGeometryBuilder.VerticesPerGlyphPass];

        NativeTextGeometryBuilder.WriteGlyph(vertices, item, pass, originXPixels: 100, originYPixels: 200);

        NativeTextVertex[] expected =
        [
            new(113f, 218f, 0.125f, 0.25f, colors.TopLeft),
            new(113f, 242f, 0.125f, 0.296875f, colors.BottomLeft),
            new(129f, 218f, 0.15625f, 0.25f, colors.TopRight),
            new(129f, 242f, 0.15625f, 0.296875f, colors.BottomRight),
            new(129f, 218f, 0.15625f, 0.25f, colors.TopRight),
            new(113f, 242f, 0.125f, 0.296875f, colors.BottomLeft),
        ];

        Assert.Equal(expected, vertices);
    }

    [Fact]
    public void WriteGlyph_WritesOnlyRequiredDestinationVertices()
    {
        GlyphAtlasRegion region = new(pageIndex: 0, xPixels: 0, yPixels: 0, widthPixels: 8, heightPixels: 8);
        NativeTextLayoutItem item = NativeTextLayoutItem.CreateGlyph(glyphKey: 0x0041, xPixels: 0, yPixels: 0, region);
        NativeTextRenderPass pass = new(0, 0, NativeTextVertexColors.Solid(SpriteColor.White));
        NativeTextVertex sentinel = new(999f, 999f, 1f, 1f, new SpriteColor(1, 2, 3, 4));
        NativeTextVertex[] vertices = Enumerable.Repeat(sentinel, NativeTextGeometryBuilder.VerticesPerGlyphPass + 2).ToArray();

        NativeTextGeometryBuilder.WriteGlyph(vertices, item, pass, originXPixels: 0, originYPixels: 0);

        Assert.NotEqual(sentinel, vertices[0]);
        Assert.Equal(sentinel, vertices[^2]);
        Assert.Equal(sentinel, vertices[^1]);
    }

    [Fact]
    public void WriteGlyph_DestinationSmallerThanOneGlyphPassThrows()
    {
        GlyphAtlasRegion region = new(pageIndex: 0, xPixels: 0, yPixels: 0, widthPixels: 8, heightPixels: 8);
        NativeTextLayoutItem item = NativeTextLayoutItem.CreateGlyph(glyphKey: 0x0041, xPixels: 0, yPixels: 0, region);
        NativeTextRenderPass pass = new(0, 0, NativeTextVertexColors.Solid(SpriteColor.White));
        NativeTextVertex[] vertices = new NativeTextVertex[NativeTextGeometryBuilder.VerticesPerGlyphPass - 1];

        Assert.Throws<ArgumentException>(() => NativeTextGeometryBuilder.WriteGlyph(vertices, item, pass, originXPixels: 0, originYPixels: 0));
    }

    [Fact]
    public void WriteGlyph_DataIconLayoutItemThrows()
    {
        NativeTextLayoutItem item = NativeTextLayoutItem.CreateDataIcon(dataIconIndex: 7, xPixels: 10, yPixels: 20, widthPixels: 16);
        NativeTextRenderPass pass = new(0, 0, NativeTextVertexColors.Solid(SpriteColor.White));
        NativeTextVertex[] vertices = new NativeTextVertex[NativeTextGeometryBuilder.VerticesPerGlyphPass];

        Assert.Throws<ArgumentException>(() => NativeTextGeometryBuilder.WriteGlyph(vertices, item, pass, originXPixels: 0, originYPixels: 0));
    }
}
