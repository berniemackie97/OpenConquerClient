using System.Numerics;
using System.Runtime.ExceptionServices;
using OpenConquer.Rendering.OpenGL.Resources;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL.Meshes;

internal sealed unsafe class OpenGLMeshRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly OpenGLDrawStateCleanup _cleanup;
    private OpenGLProgram? _texturedProgram;
    private OpenGLProgram? _shadowProgram;
    private uint _sampler;
    private readonly int _textureUniform;
    private readonly int _texturedMatrixUniform;
    private readonly int _shadowMatrixUniform;
    private readonly int _shadowAlphaUniform;
    private bool _disposed;

    internal OpenGLMeshRenderer(GL gl)
    {
        _gl = gl;
        _cleanup = new OpenGLDrawStateCleanup(gl);

        try
        {
            _texturedProgram = new OpenGLProgram(gl, VertexShader, TexturedFragmentShader);
            _textureUniform = _texturedProgram.GetRequiredUniformLocation("uTexture");
            _texturedMatrixUniform = _texturedProgram.GetRequiredUniformLocation("uClipMatrix");

            _shadowProgram = new OpenGLProgram(gl, VertexShader, ShadowFragmentShader);
            _shadowMatrixUniform = _shadowProgram.GetRequiredUniformLocation("uClipMatrix");
            _shadowAlphaUniform = _shadowProgram.GetRequiredUniformLocation("uShadowAlpha");

            _sampler = gl.GenSampler();
            gl.SamplerParameter(_sampler, SamplerParameterI.MinFilter, (int)GLEnum.Linear);
            gl.SamplerParameter(_sampler, SamplerParameterI.MagFilter, (int)GLEnum.Linear);
            gl.SamplerParameter(_sampler, SamplerParameterI.WrapS, (int)GLEnum.Repeat);
            gl.SamplerParameter(_sampler, SamplerParameterI.WrapT, (int)GLEnum.Repeat);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal void Draw(OpenGLIndexedMesh mesh, OpenGLTexture2D texture,
        Matrix4x4 clipMatrix, MeshCullMode cullMode) =>
        DrawCore(mesh, texture, clipMatrix, cullMode, shadowAlpha: null);

    internal void DrawShadow(OpenGLIndexedMesh mesh, Matrix4x4 clipMatrix,
        MeshCullMode cullMode, float alpha) =>
        DrawCore(mesh, null, clipMatrix, cullMode, alpha);

    private void DrawCore(OpenGLIndexedMesh mesh, OpenGLTexture2D? texture,
        Matrix4x4 clipMatrix, MeshCullMode cullMode, float? shadowAlpha)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        mesh.ValidateOwner(_gl);
        if (shadowAlpha is float alpha)
        {
            if (!float.IsFinite(alpha) || alpha is < 0f or > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(shadowAlpha));
            }
        }
        else
        {
            (texture ?? throw new ArgumentNullException(nameof(texture)))
                .ValidateOwner(_gl, nameof(texture));
        }
        if (!Enum.IsDefined(cullMode))
        {
            throw new ArgumentOutOfRangeException(nameof(cullMode));
        }

        OpenGLProgram program = shadowAlpha.HasValue
            ? _shadowProgram ?? throw new ObjectDisposedException(nameof(OpenGLMeshRenderer))
            : _texturedProgram ?? throw new ObjectDisposedException(nameof(OpenGLMeshRenderer));
        Matrix4x4 glClip = clipMatrix * new Matrix4x4(
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 2, 0,
            0, 0, -1, 1);

        ExceptionDispatchInfo? failure = null;
        try
        {
            _gl.Enable(EnableCap.DepthTest);
            _gl.DepthMask(!shadowAlpha.HasValue);
            _gl.DepthFunc(shadowAlpha.HasValue ? DepthFunction.Less : DepthFunction.Lequal);

            if (cullMode == MeshCullMode.None)
            {
                _gl.Disable(EnableCap.CullFace);
            }
            else
            {
                _gl.Enable(EnableCap.CullFace);
                _gl.CullFace(TriangleFace.Back);
                _gl.FrontFace(cullMode == MeshCullMode.Clockwise
                    ? FrontFaceDirection.Ccw : FrontFaceDirection.CW);
            }

            _gl.Enable(EnableCap.Blend);
            _gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
            if (shadowAlpha.HasValue)
            {
                _gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha,
                    BlendingFactor.Zero, BlendingFactor.One);
            }
            else
            {
                _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            }
            _gl.ColorMask(true, true, true, true);

            program.Use();
            _gl.UniformMatrix4(shadowAlpha.HasValue ? _shadowMatrixUniform : _texturedMatrixUniform,
                1, false, (float*)&glClip);

            if (shadowAlpha is float shadow)
            {
                _gl.Uniform1(_shadowAlphaUniform, shadow);
            }
            else
            {
                _gl.Uniform1(_textureUniform, 0);
                _gl.ActiveTexture(TextureUnit.Texture0);
                texture!.Bind();
                _gl.BindSampler(0, _sampler);
            }
            mesh.Bind();
            _gl.DrawElements(PrimitiveType.Triangles, (uint)mesh.IndexCount,
                DrawElementsType.UnsignedShort, null);
        }
        catch (Exception exception)
        {
            failure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _gl.Disable(EnableCap.DepthTest);
            _gl.Disable(EnableCap.CullFace);
            _gl.DepthFunc(DepthFunction.Lequal);
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }

        _cleanup.RestoreAfterDraw(failure, usesTexture: texture is not null);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (_sampler != 0)
            {
                _gl.DeleteSampler(_sampler);
                _sampler = 0;
            }

            _shadowProgram?.Dispose();
            _shadowProgram = null;
            _texturedProgram?.Dispose();
            _texturedProgram = null;
        }
        finally
        {
            _disposed = true;
        }
    }

    private const string VertexShader = """
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        layout (location = 1) in vec3 aNormal;
        layout (location = 2) in vec4 aPackedColor;
        layout (location = 3) in vec2 aTextureCoordinate;

        uniform mat4 uClipMatrix;
        out vec4 vertexColor;
        out vec2 textureCoordinate;

        void main()
        {
            gl_Position = uClipMatrix * vec4(aPosition, 1.0);
            vertexColor = aPackedColor.bgra;
            textureCoordinate = aTextureCoordinate;
        }
        """;

    private const string TexturedFragmentShader = """
        #version 330 core
        in vec4 vertexColor;
        in vec2 textureCoordinate;
        uniform sampler2D uTexture;
        out vec4 outputColor;

        void main()
        {
            outputColor = texture(uTexture, textureCoordinate) * vertexColor;
            if (outputColor.a < (8.0 / 255.0))
            {
                discard;
            }
        }
        """;

    private const string ShadowFragmentShader = """
        #version 330 core
        uniform float uShadowAlpha;
        out vec4 outputColor;

        void main()
        {
            outputColor = vec4(0.0, 0.0, 0.0, uShadowAlpha);
        }
        """;
}
