using System.Runtime.ExceptionServices;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL;

internal sealed unsafe class OpenGLStartupImage : IDisposable
{
    private const int FloatsPerVertex = 4;

    private static readonly uint[] s_indices = [0, 1, 2, 0, 2, 3];

    private readonly GL _gl;
    private readonly int _width;
    private readonly int _height;

    private uint _program;
    private uint _vertexArray;
    private uint _vertexBuffer;
    private uint _indexBuffer;
    private uint _texture;
    private int _textureUniform;
    private bool _disposed;

    public OpenGLStartupImage(GL gl, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        ArgumentNullException.ThrowIfNull(gl);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        int expectedLength = checked(width * height * 4);

        if (rgbaPixels.Length != expectedLength)
        {
            throw new ArgumentException(
                $"Expected {expectedLength} RGBA bytes, but received {rgbaPixels.Length}.",
                nameof(rgbaPixels)
            );
        }

        _gl = gl;
        _width = width;
        _height = height;

        try
        {
            CreatePipeline(rgbaPixels);
        }
        catch
        {
            try
            {
                DestroyResources();
            }
            catch
            {
                // Preserve the original pipeline-creation failure.
            }

            throw;
        }
    }

    /// <summary>
    /// Draws the image into the top-left corner of the viewport at the requested device size.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Retail paints the logo through a <c>CreatePatternBrush</c> (<c>0x4B0AA2</c>) returned from
    /// <c>WM_CTLCOLORDLG</c> (<c>0x4B0B26</c>). On Windows NT and later a pattern brush tiles the
    /// bitmap at its <b>natural</b> size from the client origin, so there is no stretch, scale,
    /// aspect fit, or letterbox.
    /// </para>
    /// <para>
    /// <paramref name="destinationWidth"/> and <paramref name="destinationHeight"/> are the natural
    /// size expressed in device pixels. On a display with a device pixel ratio of 1 they equal the
    /// image dimensions; on a scaled display the caller multiplies by the ratio so the logo still
    /// occupies its natural size in logical units.
    /// </para>
    /// </remarks>
    public void DrawTopLeft(
        int viewportWidth,
        int viewportHeight,
        int destinationWidth,
        int destinationHeight
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(viewportWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(viewportHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(destinationWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(destinationHeight);

        float left = ToNormalizedX(0, viewportWidth);
        float right = ToNormalizedX(destinationWidth, viewportWidth);
        float top = ToNormalizedY(0, viewportHeight);
        float bottom = ToNormalizedY(destinationHeight, viewportHeight);

        Span<float> vertices =
        [
            left,
            top,
            0f,
            0f,
            right,
            top,
            1f,
            0f,
            right,
            bottom,
            1f,
            1f,
            left,
            bottom,
            0f,
            1f,
        ];

        _gl.Disable(EnableCap.DepthTest);
        _gl.DepthMask(false);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.Blend);
        _gl.UseProgram(_program);
        _gl.BindVertexArray(_vertexArray);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);

        fixed (float* vertexPointer = vertices)
        {
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, offset: 0, (nuint)(vertices.Length * sizeof(float)), vertexPointer);
        }

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _texture);
        _gl.Uniform1(_textureUniform, 0);
        _gl.DrawElements(
            PrimitiveType.Triangles,
            (uint)s_indices.Length,
            DrawElementsType.UnsignedInt,
            null
        );

        _gl.BindVertexArray(0);
        _gl.DepthMask(true);
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

    private void CreatePipeline(ReadOnlySpan<byte> rgbaPixels)
    {
        _program = CreateProgram(VertexShaderSource, FragmentShaderSource);

        _textureUniform = _gl.GetUniformLocation(_program, "uTexture");

        if (_textureUniform < 0)
        {
            throw new InvalidOperationException(
                "The startup image shader does not expose its texture uniform."
            );
        }

        _vertexArray = _gl.GenVertexArray();
        _vertexBuffer = _gl.GenBuffer();
        _indexBuffer = _gl.GenBuffer();

        _gl.BindVertexArray(_vertexArray);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(4 * FloatsPerVertex * sizeof(float)), null, BufferUsageARB.DynamicDraw);

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _indexBuffer);

        fixed (uint* indexPointer = s_indices)
        {
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(s_indices.Length * sizeof(uint)), indexPointer, BufferUsageARB.StaticDraw);
        }

        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(
            0,
            2,
            VertexAttribPointerType.Float,
            false,
            FloatsPerVertex * sizeof(float),
            (void*)0
        );

        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(
            1,
            2,
            VertexAttribPointerType.Float,
            false,
            FloatsPerVertex * sizeof(float),
            (void*)(2 * sizeof(float))
        );

        _gl.BindVertexArray(0);

        _texture = _gl.GenTexture();

        _gl.BindTexture(TextureTarget.Texture2D, _texture);
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter,
            (int)GLEnum.Nearest
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter,
            (int)GLEnum.Nearest
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureWrapS,
            (int)GLEnum.ClampToEdge
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureWrapT,
            (int)GLEnum.ClampToEdge
        );
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);

        fixed (byte* pixelPointer = rgbaPixels)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, level: 0, InternalFormat.Rgba8, (uint)_width, (uint)_height, border: 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixelPointer);
        }

        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    private uint CreateProgram(string vertexSource, string fragmentSource)
    {
        uint vertexShader = 0;
        uint fragmentShader = 0;
        uint program = 0;

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            vertexShader = CompileShader(ShaderType.VertexShader, vertexSource);

            fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource);

            program = _gl.CreateProgram();

            _gl.AttachShader(program, vertexShader);
            _gl.AttachShader(program, fragmentShader);
            _gl.LinkProgram(program);

            _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int status);

            if (status == 0)
            {
                throw new InvalidOperationException(
                    $"Failed to link the startup image shader: {_gl.GetProgramInfoLog(program)}"
                );
            }
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        if (vertexShader != 0)
        {
            try
            {
                _gl.DeleteShader(vertexShader);
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        if (fragmentShader != 0)
        {
            try
            {
                _gl.DeleteShader(fragmentShader);
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        if (firstFailure is not null)
        {
            if (program != 0)
            {
                try
                {
                    _gl.DeleteProgram(program);
                }
                catch
                {
                    // Preserve the earlier compile, link, or shader-cleanup failure.
                }
            }

            firstFailure.Throw();
        }

        return program;
    }

    private uint CompileShader(ShaderType type, string source)
    {
        uint shader = _gl.CreateShader(type);

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _gl.ShaderSource(shader, source);
            _gl.CompileShader(shader);

            _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);

            if (status == 0)
            {
                throw new InvalidOperationException(
                    $"Failed to compile the startup image shader: {_gl.GetShaderInfoLog(shader)}"
                );
            }
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        if (firstFailure is not null)
        {
            try
            {
                _gl.DeleteShader(shader);
            }
            catch
            {
                // Preserve the shader-creation or compilation failure.
            }

            firstFailure.Throw();
        }

        return shader;
    }

    private void DestroyResources()
    {
        ExceptionDispatchInfo? firstFailure = null;

        uint texture = _texture;
        _texture = 0;

        if (texture != 0)
        {
            try
            {
                _gl.DeleteTexture(texture);
            }
            catch (Exception exception)
            {
                firstFailure = ExceptionDispatchInfo.Capture(exception);
            }
        }

        uint indexBuffer = _indexBuffer;
        _indexBuffer = 0;

        if (indexBuffer != 0)
        {
            try
            {
                _gl.DeleteBuffer(indexBuffer);
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        uint vertexBuffer = _vertexBuffer;
        _vertexBuffer = 0;

        if (vertexBuffer != 0)
        {
            try
            {
                _gl.DeleteBuffer(vertexBuffer);
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        uint vertexArray = _vertexArray;
        _vertexArray = 0;

        if (vertexArray != 0)
        {
            try
            {
                _gl.DeleteVertexArray(vertexArray);
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        uint program = _program;
        _program = 0;

        if (program != 0)
        {
            try
            {
                _gl.DeleteProgram(program);
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        firstFailure?.Throw();
    }

    private static float ToNormalizedX(int pixelX, int viewportWidth)
    {
        return 2f * pixelX / viewportWidth - 1f;
    }

    private static float ToNormalizedY(int pixelY, int viewportHeight)
    {
        return 1f - 2f * pixelY / viewportHeight;
    }

    private const string VertexShaderSource = """
        #version 330 core
        layout (location = 0) in vec2 aPosition;
        layout (location = 1) in vec2 aTextureCoordinate;
        out vec2 textureCoordinate;

        void main()
        {
            textureCoordinate = aTextureCoordinate;
            gl_Position = vec4(aPosition, 0.0, 1.0);
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        in vec2 textureCoordinate;
        out vec4 outputColor;
        uniform sampler2D uTexture;

        void main()
        {
            outputColor = texture(uTexture, textureCoordinate);
        }
        """;
}
