using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Text.Atlas;
using OpenConquer.Rendering.Text.Layout;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Rendering.Tests.OpenGL;

public sealed class OpenGLTextVertexStagingBufferTests
{
    [Fact]
    public void Constructor_RejectsInvalidOrOverflowingCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OpenGLTextVertexStagingBuffer(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OpenGLTextVertexStagingBuffer(-1));
        Assert.Throws<OverflowException>(() => new OpenGLTextVertexStagingBuffer(int.MaxValue));
    }

    [Fact]
    public void Constructor_ExposesBoundedVertexCapacity()
    {
        OpenGLTextVertexStagingBuffer buffer = new(2);

        Assert.Equal(2, buffer.GlyphPassCapacity);
        Assert.Equal(12, buffer.VertexCapacity);
        Assert.Equal(0, buffer.VertexCount);
        Assert.True(buffer.Vertices.IsEmpty);
    }

    [Fact]
    public void TryAppendGlyphPass_WritesVerifiedGeometryAndColorMapping()
    {
        OpenGLTextVertexStagingBuffer buffer = new(1);
        GlyphAtlasRegion region = new(2, 10, 20, 2, 3);
        NativeTextLayoutItem item = NativeTextLayoutItem.CreateGlyph(0x0041, 4, 5, region);
        NativeTextVertexColors colors = new(new SpriteColor(1, 2, 3, 4), new SpriteColor(5, 6, 7, 8), new SpriteColor(9, 10, 11, 12), new SpriteColor(13, 14, 15, 16));
        NativeTextRenderPass pass = new(1, -2, colors);

        bool appended = buffer.TryAppendGlyphPass(item, pass, originXPixels: 100, originYPixels: 200);
        ReadOnlySpan<OpenGLTextVertex> vertices = buffer.Vertices;

        Assert.True(appended);
        Assert.Equal(NativeTextGeometryBuilder.VerticesPerGlyphPass, buffer.VertexCount);
        Assert.Equal(6, vertices.Length);

        AssertVertex(vertices[0], 105f, 203f, 10f / 512f, 20f / 512f, colors.TopLeft);
        AssertVertex(vertices[1], 105f, 206f, 10f / 512f, 23f / 512f, colors.BottomLeft);
        AssertVertex(vertices[2], 107f, 203f, 12f / 512f, 20f / 512f, colors.TopRight);
        AssertVertex(vertices[3], 107f, 206f, 12f / 512f, 23f / 512f, colors.BottomRight);
        AssertVertex(vertices[4], 107f, 203f, 12f / 512f, 20f / 512f, colors.TopRight);
        AssertVertex(vertices[5], 105f, 206f, 10f / 512f, 23f / 512f, colors.BottomLeft);
    }

    [Fact]
    public void TryAppendGlyphPass_WhenFullReturnsFalseWithoutMutation()
    {
        OpenGLTextVertexStagingBuffer buffer = new(1);
        NativeTextLayoutItem item = CreateGlyphItem();
        NativeTextRenderPass pass = CreatePass();

        Assert.True(buffer.TryAppendGlyphPass(item, pass, originXPixels: 10, originYPixels: 20));
        OpenGLTextVertex firstVertex = buffer.Vertices[0];

        bool appended = buffer.TryAppendGlyphPass(item, pass, originXPixels: 100, originYPixels: 200);

        Assert.False(appended);
        Assert.Equal(NativeTextGeometryBuilder.VerticesPerGlyphPass, buffer.VertexCount);
        AssertVertexEqual(firstVertex, buffer.Vertices[0]);
    }

    [Fact]
    public void Clear_ResetsUsedRangeAndAllowsReuse()
    {
        OpenGLTextVertexStagingBuffer buffer = new(1);
        NativeTextLayoutItem item = CreateGlyphItem();
        NativeTextRenderPass pass = CreatePass();

        Assert.True(buffer.TryAppendGlyphPass(item, pass, originXPixels: 10, originYPixels: 20));

        buffer.Clear();

        Assert.Equal(0, buffer.VertexCount);
        Assert.True(buffer.Vertices.IsEmpty);
        Assert.True(buffer.TryAppendGlyphPass(item, pass, originXPixels: 30, originYPixels: 40));
        Assert.Equal(6, buffer.VertexCount);
        Assert.Equal(31f, buffer.Vertices[0].ScreenX);
        Assert.Equal(41f, buffer.Vertices[0].ScreenY);
    }

    [Fact]
    public void TryAppendGlyphPass_DataIconThrowsWithoutMutation()
    {
        OpenGLTextVertexStagingBuffer buffer = new(1);
        NativeTextLayoutItem item = NativeTextLayoutItem.CreateDataIcon(7, 0, 0, 16);
        NativeTextRenderPass pass = CreatePass();

        Assert.Throws<ArgumentException>(() => buffer.TryAppendGlyphPass(item, pass, originXPixels: 0, originYPixels: 0));
        Assert.Equal(0, buffer.VertexCount);
        Assert.True(buffer.Vertices.IsEmpty);
    }

    [Fact]
    public void WarmedAppendAndClearPath_DoesNotAllocateManagedMemory()
    {
        OpenGLTextVertexStagingBuffer buffer = new(1);
        NativeTextLayoutItem item = CreateGlyphItem();
        NativeTextRenderPass pass = CreatePass();

        Assert.True(buffer.TryAppendGlyphPass(item, pass, originXPixels: 0, originYPixels: 0));
        buffer.Clear();

        long before = GC.GetAllocatedBytesForCurrentThread();
        bool succeeded = true;

        for (int iteration = 0; iteration < 256; iteration++)
        {
            succeeded &= buffer.TryAppendGlyphPass(item, pass, originXPixels: iteration, originYPixels: iteration);
            buffer.Clear();
        }

        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(succeeded);
        Assert.Equal(0L, allocatedBytes);
    }

    private static NativeTextLayoutItem CreateGlyphItem()
    {
        return NativeTextLayoutItem.CreateGlyph(0x0041, 1, 1, new GlyphAtlasRegion(0, 0, 0, 2, 3));
    }

    private static NativeTextRenderPass CreatePass()
    {
        return new NativeTextRenderPass(0, 0, NativeTextVertexColors.Solid(new SpriteColor(10, 20, 30, 40)));
    }

    private static void AssertVertex(OpenGLTextVertex vertex, float screenX, float screenY, float textureU, float textureV, SpriteColor color)
    {
        Assert.Equal(screenX, vertex.ScreenX);
        Assert.Equal(screenY, vertex.ScreenY);
        Assert.Equal(textureU, vertex.TextureU);
        Assert.Equal(textureV, vertex.TextureV);
        Assert.Equal(color.Red, vertex.Red);
        Assert.Equal(color.Green, vertex.Green);
        Assert.Equal(color.Blue, vertex.Blue);
        Assert.Equal(color.Alpha, vertex.Alpha);
    }

    private static void AssertVertexEqual(OpenGLTextVertex expected, OpenGLTextVertex actual)
    {
        Assert.Equal(expected.ScreenX, actual.ScreenX);
        Assert.Equal(expected.ScreenY, actual.ScreenY);
        Assert.Equal(expected.TextureU, actual.TextureU);
        Assert.Equal(expected.TextureV, actual.TextureV);
        Assert.Equal(expected.Red, actual.Red);
        Assert.Equal(expected.Green, actual.Green);
        Assert.Equal(expected.Blue, actual.Blue);
        Assert.Equal(expected.Alpha, actual.Alpha);
    }
}
