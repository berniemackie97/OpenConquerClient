using System.Runtime.ExceptionServices;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL;

internal sealed class OpenGLRenderTarget : IDisposable
{
    private static readonly ColorFormatCandidate[] s_colorFormatCandidates =
    [
        new(
            InternalFormat.Rgb565,
            RedBits: 5,
            GreenBits: 6,
            BlueBits: 5,
            RequiresRgb565Support: true
        ),
        new(
            InternalFormat.Rgb5,
            RedBits: 5,
            GreenBits: 5,
            BlueBits: 5,
            RequiresRgb565Support: false
        ),
    ];

    private readonly GL _gl;
    private readonly int _width;
    private readonly int _height;

    private uint _framebuffer;
    private uint _colorTexture;
    private uint _depthRenderbuffer;
    private bool _disposed;

    public OpenGLRenderTarget(GL gl, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(gl);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        _gl = gl;
        _width = width;
        _height = height;

        try
        {
            CreateResources();
        }
        catch
        {
            try
            {
                DestroyResources();
            }
            catch
            {
                // Preserve the original resource-creation failure.
            }

            throw;
        }
    }

    public void BeginFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, instance: this);

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        _gl.Viewport(0, 0, (uint)_width, (uint)_height);

        _gl.Disable(EnableCap.ScissorTest);
        _gl.Disable(EnableCap.Dither);
        _gl.ColorMask(red: true, green: true, blue: true, alpha: true);
        _gl.DepthMask(flag: true);

        _gl.ClearColor(red: 0f, green: 0f, blue: 0f, alpha: 1f);
        _gl.ClearDepth(1.0);
        _gl.Clear(mask: (uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
    }

    public void BindForRead()
    {
        ObjectDisposedException.ThrowIf(_disposed, instance: this);

        _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _framebuffer);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            DestroyResources();
        }
        finally
        {
            _disposed = true;
        }
    }

    private void CreateResources()
    {
        CreateDepthRenderbuffer();

        bool supportsRgb565 = SupportsRgb565();

        foreach (ColorFormatCandidate candidate in s_colorFormatCandidates)
        {
            if (candidate.RequiresRgb565Support && !supportsRgb565)
            {
                continue;
            }

            if (TryCreateColorFramebuffer(candidate))
            {
                return;
            }
        }

        throw new NotSupportedException(
            "The OpenGL implementation cannot create a complete 16-bit RGB render target "
                + "compatible with the retail 5517 color-buffer formats."
        );
    }

    private void CreateDepthRenderbuffer()
    {
        _depthRenderbuffer = _gl.GenRenderbuffer();

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthRenderbuffer);

            _gl.RenderbufferStorage(
                RenderbufferTarget.Renderbuffer,
                InternalFormat.DepthComponent16,
                (uint)_width,
                (uint)_height
            );

            _gl.GetRenderbufferParameter(
                RenderbufferTarget.Renderbuffer,
                pname: RenderbufferParameterName.DepthSize,
                out int depthBits
            );

            if (depthBits != 16)
            {
                throw new NotSupportedException(
                    $"The OpenGL implementation allocated a {depthBits}-bit depth renderbuffer; "
                        + "retail 5517 requires exactly 16-bit depth precision."
                );
            }
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, renderbuffer: 0);
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstFailure?.Throw();
    }

    private bool TryCreateColorFramebuffer(ColorFormatCandidate candidate)
    {
        bool created = false;

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _colorTexture = _gl.GenTexture();

            _gl.BindTexture(TextureTarget.Texture2D, _colorTexture);

            _gl.TexImage2D(
                TextureTarget.Texture2D,
                level: 0,
                candidate.InternalFormat,
                (uint)_width,
                (uint)_height,
                border: 0,
                PixelFormat.Rgb,
                PixelType.UnsignedByte,
                ReadOnlySpan<byte>.Empty
            );

            if (HasExpectedColorPrecision(candidate))
            {
                _framebuffer = _gl.GenFramebuffer();

                _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);

                _gl.FramebufferTexture2D(
                    FramebufferTarget.Framebuffer,
                    FramebufferAttachment.ColorAttachment0,
                    textarget: TextureTarget.Texture2D,
                    _colorTexture,
                    level: 0
                );

                _gl.FramebufferRenderbuffer(
                    FramebufferTarget.Framebuffer,
                    FramebufferAttachment.DepthAttachment,
                    RenderbufferTarget.Renderbuffer,
                    _depthRenderbuffer
                );

                created =
                    _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer)
                    == GLEnum.FramebufferComplete;
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
            _gl.BindTexture(TextureTarget.Texture2D, texture: 0);
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        if (!created)
        {
            try
            {
                DestroyColorResources();
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        firstFailure?.Throw();

        return created;
    }

    private bool HasExpectedColorPrecision(ColorFormatCandidate candidate)
    {
        _gl.GetTexLevelParameter(
            TextureTarget.Texture2D,
            level: 0,
            pname: GetTextureParameter.TextureRedSize,
            out int redBits
        );
        _gl.GetTexLevelParameter(
            TextureTarget.Texture2D,
            level: 0,
            pname: GetTextureParameter.TextureGreenSize,
            out int greenBits
        );
        _gl.GetTexLevelParameter(
            TextureTarget.Texture2D,
            level: 0,
            pname: GetTextureParameter.TextureBlueSize,
            out int blueBits
        );
        _gl.GetTexLevelParameter(
            TextureTarget.Texture2D,
            level: 0,
            pname: GetTextureParameter.TextureAlphaSize,
            out int alphaBits
        );

        return redBits == candidate.RedBits
            && greenBits == candidate.GreenBits
            && blueBits == candidate.BlueBits
            && alphaBits == 0;
    }

    private bool SupportsRgb565()
    {
        _gl.GetInteger(pname: GetPName.MajorVersion, out int majorVersion);
        _gl.GetInteger(pname: GetPName.MinorVersion, out int minorVersion);

        bool hasCoreSupport = majorVersion > 4 || majorVersion == 4 && minorVersion >= 2;

        return hasCoreSupport || _gl.IsExtensionPresent("GL_ARB_ES2_compatibility");
    }

    private void DestroyResources()
    {
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            DestroyColorResources();
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        uint depthRenderbuffer = _depthRenderbuffer;

        _depthRenderbuffer = 0;

        if (depthRenderbuffer != 0)
        {
            try
            {
                _gl.DeleteRenderbuffer(depthRenderbuffer);
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        firstFailure?.Throw();
    }

    private void DestroyColorResources()
    {
        ExceptionDispatchInfo? firstFailure = null;

        uint framebuffer = _framebuffer;

        _framebuffer = 0;

        if (framebuffer != 0)
        {
            try
            {
                _gl.DeleteFramebuffer(framebuffer);
            }
            catch (Exception exception)
            {
                firstFailure = ExceptionDispatchInfo.Capture(exception);
            }
        }

        uint colorTexture = _colorTexture;

        _colorTexture = 0;

        if (colorTexture != 0)
        {
            try
            {
                _gl.DeleteTexture(colorTexture);
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        firstFailure?.Throw();
    }

    private readonly record struct ColorFormatCandidate(
        InternalFormat InternalFormat,
        int RedBits,
        int GreenBits,
        int BlueBits,
        bool RequiresRgb565Support
    );
}
