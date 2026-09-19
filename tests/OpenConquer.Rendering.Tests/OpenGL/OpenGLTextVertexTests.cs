using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Text;

namespace OpenConquer.Rendering.Tests.OpenGL;

public sealed class OpenGLTextVertexTests
{
    [Fact]
    public void Size_IsExactlyTwentyBytes()
    {
        Assert.Equal(20, Unsafe.SizeOf<OpenGLTextVertex>());
    }

    [Fact]
    public void FieldOffsets_MatchGpuAttributeContract()
    {
        Assert.Equal(0, Marshal.OffsetOf<OpenGLTextVertex>(nameof(OpenGLTextVertex.ScreenX)).ToInt32());
        Assert.Equal(4, Marshal.OffsetOf<OpenGLTextVertex>(nameof(OpenGLTextVertex.ScreenY)).ToInt32());
        Assert.Equal(8, Marshal.OffsetOf<OpenGLTextVertex>(nameof(OpenGLTextVertex.TextureU)).ToInt32());
        Assert.Equal(12, Marshal.OffsetOf<OpenGLTextVertex>(nameof(OpenGLTextVertex.TextureV)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<OpenGLTextVertex>(nameof(OpenGLTextVertex.Red)).ToInt32());
        Assert.Equal(17, Marshal.OffsetOf<OpenGLTextVertex>(nameof(OpenGLTextVertex.Green)).ToInt32());
        Assert.Equal(18, Marshal.OffsetOf<OpenGLTextVertex>(nameof(OpenGLTextVertex.Blue)).ToInt32());
        Assert.Equal(19, Marshal.OffsetOf<OpenGLTextVertex>(nameof(OpenGLTextVertex.Alpha)).ToInt32());
    }

    [Fact]
    public void Constructor_PreservesGeometryTextureCoordinatesAndColor()
    {
        NativeTextVertex source = new(12.5f, -3.25f, 0.125f, 0.875f, new SpriteColor(10, 20, 30, 40));

        OpenGLTextVertex vertex = new(source);

        Assert.Equal(source.ScreenX, vertex.ScreenX);
        Assert.Equal(source.ScreenY, vertex.ScreenY);
        Assert.Equal(source.TextureU, vertex.TextureU);
        Assert.Equal(source.TextureV, vertex.TextureV);
        Assert.Equal(source.Color.Red, vertex.Red);
        Assert.Equal(source.Color.Green, vertex.Green);
        Assert.Equal(source.Color.Blue, vertex.Blue);
        Assert.Equal(source.Color.Alpha, vertex.Alpha);
    }
}
