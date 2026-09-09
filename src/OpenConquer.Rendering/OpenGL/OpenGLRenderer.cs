using System.Runtime.ExceptionServices;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL;

public sealed class OpenGLRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly LogicalRenderSize _logicalRenderSize;
    private readonly OpenGLRenderTarget _renderTarget;
    private readonly OpenGLSpriteRenderer _spriteRenderer;
    private readonly PresentationPolicy _presentationPolicy;

    private int _framebufferWidth;
    private int _framebufferHeight;
    private PresentationViewport _viewport;
    private bool _hostFramebufferValidated;
    private bool _frameActive;
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

        try
        {
            _spriteRenderer = new OpenGLSpriteRenderer(gl);
        }
        catch
        {
            try
            {
                _renderTarget.Dispose();
            }
            catch
            {
                // Preserve the original sprite-renderer creation failure.
            }

            throw;
        }

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

    public void BeginFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_frameActive)
        {
            throw new InvalidOperationException("A rendering frame is already active.");
        }

        _renderTarget.BeginFrame();
        _frameActive = true;
    }

    public void DrawSprite(OpenGLTexture2D texture, int x, int y)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(texture);
        EnsureFrameActiveForDrawing();

        _spriteRenderer.Draw(texture, _logicalRenderSize.Width, _logicalRenderSize.Height, x, y);
    }

    public void DrawSprite(OpenGLTexture2D texture, int x, int y, int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(texture);
        EnsureFrameActiveForDrawing();

        SpriteSourceRectangle sourceRectangle = new(x: 0, y: 0, texture.Width, texture.Height);

        _spriteRenderer.Draw(texture, _logicalRenderSize.Width, _logicalRenderSize.Height, sourceRectangle, x, y, width, height);
    }

    public void DrawSprite(OpenGLTexture2D texture, SpriteSourceRectangle sourceRectangle, int x, int y, int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(texture);
        EnsureFrameActiveForDrawing();

        _spriteRenderer.Draw(texture, _logicalRenderSize.Width, _logicalRenderSize.Height, sourceRectangle, x, y, width, height);
    }

    internal byte[] ReadFrameTopLeftRgba()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_frameActive)
        {
            throw new InvalidOperationException("A rendering frame must be active before reading its color buffer.");
        }

        return _renderTarget.ReadColorTopLeftRgba();
    }

    public void EndFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_frameActive)
        {
            throw new InvalidOperationException("No rendering frame is active.");
        }

        try
        {
            BlitToHostFramebuffer();
        }
        finally
        {
            _frameActive = false;
        }
    }

    public void RenderFrame()
    {
        BeginFrame();
        EndFrame();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _spriteRenderer.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _renderTarget.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            _frameActive = false;
            _disposed = true;
        }

        firstFailure?.Throw();
    }

    private void EnsureFrameActiveForDrawing()
    {
        if (!_frameActive)
        {
            throw new InvalidOperationException("A rendering frame must be active before drawing.");
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
                    dstY1: _viewport.OffsetY + _viewport.Height, (uint)ClearBufferMask.ColorBufferBit, ToBlitFilter(_viewport.Filter));
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
