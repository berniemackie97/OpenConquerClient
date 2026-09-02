using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL;

public sealed class OpenGLRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly LogicalRenderSize _logicalRenderSize;
    private readonly OpenGLRenderTarget _renderTarget;

    private int _framebufferWidth;
    private int _framebufferHeight;
    private bool _hostFramebufferValidated;
    private bool _disposed;

    internal OpenGLRenderer(GL gl, LogicalRenderSize logicalRenderSize, int framebufferWidth, int framebufferHeight)
    {
        ArgumentNullException.ThrowIfNull(gl);
        ArgumentOutOfRangeException.ThrowIfNegative(framebufferWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(framebufferHeight);

        _gl = gl;
        _logicalRenderSize = logicalRenderSize;
        _renderTarget = new OpenGLRenderTarget(gl, logicalRenderSize.Width, logicalRenderSize.Height);

        _framebufferWidth = framebufferWidth;
        _framebufferHeight = framebufferHeight;
    }

    public void ResizeHostFramebuffer(int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        _framebufferWidth = width;
        _framebufferHeight = height;
    }

    public void RenderFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _renderTarget.BeginFrame();
        BlitToHostFramebuffer();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _renderTarget.Dispose();
        }
        finally
        {
            _disposed = true;
        }
    }

    private void BlitToHostFramebuffer()
    {
        try
        {
            if (_framebufferWidth == 0 || _framebufferHeight == 0)
            {
                return;
            }

            _gl.Disable(EnableCap.ScissorTest);
            _gl.Disable(EnableCap.FramebufferSrgb);

            _renderTarget.BindForRead();
            _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, framebuffer: 0);

            ValidateHostFramebuffer();

            _gl.BlitFramebuffer(srcX0: 0, srcY0: 0, srcX1: _logicalRenderSize.Width, srcY1: _logicalRenderSize.Height, dstX0: 0, dstY0: 0,
                dstX1: _framebufferWidth, dstY1: _framebufferHeight, (uint)ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
        }
        finally
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer: 0);
            _gl.Viewport(0, 0, (uint)_framebufferWidth, (uint)_framebufferHeight);
        }
    }

    private void ValidateHostFramebuffer()
    {
        if (_hostFramebufferValidated)
        {
            return;
        }

        _gl.GetInteger(pname: GetPName.SampleBuffers, out int sampleBufferCount);

        if (sampleBufferCount != 0)
        {
            throw new NotSupportedException($"The current presentation path requires a single-sampled desktop framebuffer, but OpenGL reports {sampleBufferCount} sample buffer(s).");
        }

        _hostFramebufferValidated = true;
    }
}
