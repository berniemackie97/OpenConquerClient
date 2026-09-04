using System.Runtime.ExceptionServices;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL;

public sealed class OpenGLRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly LogicalRenderSize _logicalRenderSize;
    private readonly OpenGLRenderTarget _renderTarget;
    private readonly PresentationPolicy _presentationPolicy;

    private int _framebufferWidth;
    private int _framebufferHeight;
    private PresentationViewport _viewport;
    private bool _hostFramebufferValidated;
    private bool _disposed;

    internal OpenGLRenderer(GL gl, LogicalRenderSize logicalRenderSize, int framebufferWidth, int framebufferHeight, PresentationPolicy presentationPolicy)
    {
        ArgumentNullException.ThrowIfNull(gl);
        ArgumentOutOfRangeException.ThrowIfNegative(framebufferWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(framebufferHeight);

        _gl = gl;
        _logicalRenderSize = logicalRenderSize;
        _presentationPolicy = presentationPolicy;
        _renderTarget = new OpenGLRenderTarget(gl, logicalRenderSize.Width, logicalRenderSize.Height);

        _framebufferWidth = framebufferWidth;
        _framebufferHeight = framebufferHeight;
        _viewport = PresentationViewport.Compute(logicalRenderSize, framebufferWidth, framebufferHeight, presentationPolicy);
    }

    /// <summary>
    /// Where the logical frame is currently being presented inside the host framebuffer.
    /// </summary>
    public PresentationViewport Viewport => _viewport;

    public void ResizeHostFramebuffer(int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        _framebufferWidth = width;
        _framebufferHeight = height;
        _viewport = PresentationViewport.Compute(_logicalRenderSize, width, height, _presentationPolicy);
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
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            if (!_viewport.IsEmpty)
            {
                _gl.Disable(EnableCap.ScissorTest);
                _gl.Disable(EnableCap.FramebufferSrgb);

                _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, framebuffer: 0);

                ValidateHostFramebuffer();
                ClearLetterboxBars();

                _renderTarget.BindForRead();

                _gl.BlitFramebuffer(srcX0: 0, srcY0: 0, srcX1: _logicalRenderSize.Width, srcY1: _logicalRenderSize.Height,
                    dstX0: _viewport.OffsetX, dstY0: _viewport.OffsetY, dstX1: _viewport.OffsetX + _viewport.Width,
                    dstY1: _viewport.OffsetY + _viewport.Height, (uint)ClearBufferMask.ColorBufferBit,
                    ToBlitFilter(_viewport.Filter));
            }
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer: 0);
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _gl.Viewport(0, 0, (uint)_framebufferWidth, (uint)_framebufferHeight);
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstFailure?.Throw();
    }

    /// <summary>
    /// Clears the host framebuffer when the presented rectangle does not fill it.
    /// </summary>
    private void ClearLetterboxBars()
    {
        if (_viewport.CoversHostFramebuffer())
        {
            return;
        }

        _gl.Viewport(0, 0, (uint)_framebufferWidth, (uint)_framebufferHeight);
        _gl.ColorMask(red: true, green: true, blue: true, alpha: true);
        _gl.ClearColor(red: 0f, green: 0f, blue: 0f, alpha: 1f);
        _gl.Clear(mask: (uint)ClearBufferMask.ColorBufferBit);
    }

    private static BlitFramebufferFilter ToBlitFilter(PresentationFilter filter)
    {
        return filter switch
        {
            PresentationFilter.Nearest => BlitFramebufferFilter.Nearest,
            PresentationFilter.Linear => BlitFramebufferFilter.Linear,

            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Unknown presentation filter."),
        };
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
