using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL;

/// <summary>
/// Renders the one-shot retail startup logo directly to a startup window's framebuffer.
/// </summary>
public sealed class OpenGLStartupLogoRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly OpenGLStartupImage _image;
    private bool _disposed;

    internal OpenGLStartupLogoRenderer(GL gl, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        ArgumentNullException.ThrowIfNull(gl);

        _gl = gl;
        _image = new OpenGLStartupImage(gl, width, height, rgbaPixels);
    }

    public void Render(int framebufferWidth, int framebufferHeight)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentOutOfRangeException.ThrowIfNegative(framebufferWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(framebufferHeight);

        if (framebufferWidth == 0 || framebufferHeight == 0)
        {
            return;
        }

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer: 0);
        _gl.Disable(EnableCap.ScissorTest);
        _gl.Disable(EnableCap.FramebufferSrgb);
        _gl.Viewport(0, 0, (uint)framebufferWidth, (uint)framebufferHeight);
        _gl.ColorMask(red: true, green: true, blue: true, alpha: true);
        _gl.ClearColor(red: 0f, green: 0f, blue: 0f, alpha: 1f);
        _gl.Clear(mask: (uint)ClearBufferMask.ColorBufferBit);
        _image.DrawContained(framebufferWidth, framebufferHeight);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _image.Dispose();
        _disposed = true;
    }
}
