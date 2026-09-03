using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL;

/// <summary>
/// Renders the one-shot retail startup surface directly to a startup window's framebuffer.
/// </summary>
/// <remarks>
/// <para>
/// The surface carries a logo bitmap when one could be loaded and is otherwise a plain cleared
/// window. Both are native states: retail stores a null <c>HBITMAP</c> at <c>0x4B0A8E</c> without
/// checking it, so <c>WM_CTLCOLORDLG</c> (<c>0x4B0B26</c>) can legitimately return a null brush and
/// leave the dialog painted with its default background.
/// </para>
/// <para>
/// When a bitmap is present it is painted at its natural size anchored to the client origin, with
/// no stretch, scale, aspect fit, or letterbox. See <see cref="OpenGLStartupImage.DrawTopLeft"/>
/// for the pattern-brush evidence.
/// </para>
/// </remarks>
public sealed class OpenGLStartupSurfaceRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly OpenGLStartupImage? _image;
    private readonly int _imageWidth;
    private readonly int _imageHeight;
    private bool _disposed;

    internal OpenGLStartupSurfaceRenderer(GL gl)
    {
        ArgumentNullException.ThrowIfNull(gl);

        _gl = gl;
    }

    internal OpenGLStartupSurfaceRenderer(GL gl, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        ArgumentNullException.ThrowIfNull(gl);

        _gl = gl;
        _imageWidth = width;
        _imageHeight = height;
        _image = new OpenGLStartupImage(gl, width, height, rgbaPixels);
    }

    /// <summary>
    /// Clears the framebuffer and, when a bitmap is present, paints it at natural size in the
    /// top-left corner.
    /// </summary>
    /// <param name="framebufferWidth">Framebuffer width in device pixels.</param>
    /// <param name="framebufferHeight">Framebuffer height in device pixels.</param>
    /// <param name="logicalWidth">Window client width in logical units.</param>
    /// <param name="logicalHeight">Window client height in logical units.</param>
    /// <remarks>
    /// Natural size is defined in logical units, so on a display whose framebuffer is larger than
    /// its logical client area the destination is scaled by the device pixel ratio. The logo then
    /// occupies the same logical area at every display density instead of shrinking into a corner.
    /// </remarks>
    public void Render(int framebufferWidth, int framebufferHeight, int logicalWidth, int logicalHeight)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentOutOfRangeException.ThrowIfNegative(framebufferWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(framebufferHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(logicalWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(logicalHeight);

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

        if (_image is null)
        {
            return;
        }

        StartupSurfacePlacement placement = StartupSurfacePlacement.Compute(framebufferWidth, framebufferHeight, logicalWidth, logicalHeight, _imageWidth, _imageHeight);

        _image.DrawTopLeft(framebufferWidth, framebufferHeight, placement.Width, placement.Height);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _image?.Dispose();
        _disposed = true;
    }
}
