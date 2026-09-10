using System.Runtime.ExceptionServices;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL;

internal sealed unsafe class OpenGLSpriteRenderer : IDisposable
{
    private const int FloatsPerVertex = 4;

    private static readonly uint[] s_indices = [0, 1, 2, 0, 2, 3];

    private readonly GL _gl;

    private OpenGLProgram? _program;
    private uint _vertexArray;
    private uint _vertexBuffer;
    private uint _indexBuffer;
    private int _textureUniform;
    private int _colorUniform;
    private bool _disposed;

    public OpenGLSpriteRenderer(GL gl)
    {
        ArgumentNullException.ThrowIfNull(gl);

        _gl = gl;

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
                // Preserve the original sprite-pipeline creation failure.
            }

            throw;
        }
    }

    public void Draw(OpenGLTexture2D texture, int targetWidth, int targetHeight, int x, int y)
    {
        Draw(texture, targetWidth, targetHeight, x, y, SpriteColor.White);
    }

    public void Draw(OpenGLTexture2D texture, int targetWidth, int targetHeight, int x, int y, SpriteColor color)
    {
        ValidateCommonDrawArguments(texture, targetWidth, targetHeight);

        SpriteSourceRectangle sourceRectangle = new(x: 0, y: 0, texture.Width, texture.Height);

        DrawCore(texture, targetWidth, targetHeight, sourceRectangle, x, y, texture.Width, texture.Height, color);
    }

    public void Draw(OpenGLTexture2D texture, int targetWidth, int targetHeight, SpriteSourceRectangle sourceRectangle, int x, int y, int width, int height)
    {
        Draw(texture, targetWidth, targetHeight, sourceRectangle, x, y, width, height, SpriteColor.White);
    }

    public void Draw(OpenGLTexture2D texture, int targetWidth, int targetHeight, SpriteSourceRectangle sourceRectangle, int x, int y, int width, int height, SpriteColor color)
    {
        ValidateCommonDrawArguments(texture, targetWidth, targetHeight);
        ValidateSourceRectangle(texture, sourceRectangle);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        DrawCore(texture, targetWidth, targetHeight, sourceRectangle, x, y, width, height, color);
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

    private void DrawCore(OpenGLTexture2D texture, int targetWidth, int targetHeight, SpriteSourceRectangle sourceRectangle, int x, int y, int width, int height, SpriteColor color)
    {
        long rightPixel = (long)x + width;
        long bottomPixel = (long)y + height;

        float left = ToNormalizedX(x, targetWidth);
        float right = ToNormalizedX(rightPixel, targetWidth);
        float top = ToNormalizedY(y, targetHeight);
        float bottom = ToNormalizedY(bottomPixel, targetHeight);

        float sourceLeft = ToNormalizedTextureCoordinate(sourceRectangle.X, texture.Width);
        float sourceRight = ToNormalizedTextureCoordinate(sourceRectangle.Right, texture.Width);
        float sourceTop = ToNormalizedTextureCoordinate(sourceRectangle.Y, texture.Height);
        float sourceBottom = ToNormalizedTextureCoordinate(sourceRectangle.Bottom, texture.Height);

        Span<float> vertices = [left, top, sourceLeft, sourceTop, right, top, sourceRight, sourceTop, right, bottom, sourceRight, sourceBottom, left, bottom, sourceLeft, sourceBottom];

        OpenGLProgram program = _program ?? throw new InvalidOperationException("The OpenGL sprite program is unavailable.");

        _gl.Disable(EnableCap.ScissorTest);
        _gl.Disable(EnableCap.DepthTest);
        _gl.DepthMask(false);
        _gl.Disable(EnableCap.CullFace);
        _gl.ColorMask(red: true, green: true, blue: true, alpha: true);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        program.Use();

        _gl.BindVertexArray(_vertexArray);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);

        fixed (float* vertexPointer = vertices)
        {
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, offset: 0, (nuint)(vertices.Length * sizeof(float)), vertexPointer);
        }

        _gl.ActiveTexture(TextureUnit.Texture0);
        texture.Bind();
        _gl.Uniform1(_textureUniform, 0);
        _gl.Uniform4(_colorUniform, ToNormalizedColorChannel(color.Red), ToNormalizedColorChannel(color.Green), ToNormalizedColorChannel(color.Blue), ToNormalizedColorChannel(color.Alpha));
        _gl.DrawElements(PrimitiveType.Triangles, (uint)s_indices.Length, DrawElementsType.UnsignedInt, null);

        _gl.BindTexture(TextureTarget.Texture2D, texture: 0);
        _gl.BindVertexArray(0);
        _gl.Disable(EnableCap.Blend);
        _gl.DepthMask(true);
    }

    private void ValidateCommonDrawArguments(OpenGLTexture2D texture, int targetWidth, int targetHeight)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetHeight);

        texture.ValidateOwner(_gl, nameof(texture));
    }

    private static void ValidateSourceRectangle(OpenGLTexture2D texture, SpriteSourceRectangle sourceRectangle)
    {
        if (sourceRectangle.X < 0 || sourceRectangle.Y < 0 || sourceRectangle.Width <= 0 || sourceRectangle.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRectangle), sourceRectangle, "The sprite source rectangle must have non-negative coordinates and positive dimensions.");
        }

        if (sourceRectangle.Right > texture.Width || sourceRectangle.Bottom > texture.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRectangle), sourceRectangle, $"The sprite source rectangle must fit within the {texture.Width}x{texture.Height} texture.");
        }
    }

    private void CreateResources()
    {
        _program = new OpenGLProgram(_gl, VertexShaderSource, FragmentShaderSource);
        _textureUniform = _program.GetRequiredUniformLocation("uTexture");
        _colorUniform = _program.GetRequiredUniformLocation("uColor");

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
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, FloatsPerVertex * sizeof(float), (void*)0);

        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, FloatsPerVertex * sizeof(float), (void*)(2 * sizeof(float)));

        _gl.BindVertexArray(0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer: 0);
    }

    private void DestroyResources()
    {
        ExceptionDispatchInfo? firstFailure = null;

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
                firstFailure = ExceptionDispatchInfo.Capture(exception);
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

        OpenGLProgram? program = _program;
        _program = null;

        if (program is not null)
        {
            try
            {
                program.Dispose();
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        firstFailure?.Throw();
    }

    private static float ToNormalizedX(long pixelX, int targetWidth)
    {
        return (float)(2.0 * pixelX / targetWidth - 1.0);
    }

    private static float ToNormalizedY(long pixelY, int targetHeight)
    {
        return (float)(1.0 - 2.0 * pixelY / targetHeight);
    }

    private static float ToNormalizedTextureCoordinate(long pixel, int textureExtent)
    {
        return (float)((double)pixel / textureExtent);
    }

    private static float ToNormalizedColorChannel(byte channel)
    {
        return channel / (float)byte.MaxValue;
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
        uniform vec4 uColor;

        void main()
        {
            outputColor = texture(uTexture, textureCoordinate) * uColor;
        }
        """;
}
