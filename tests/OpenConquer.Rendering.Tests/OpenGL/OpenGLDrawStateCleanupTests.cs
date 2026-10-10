using System.Runtime.ExceptionServices;
using OpenConquer.Rendering.OpenGL;

namespace OpenConquer.Rendering.Tests.OpenGL;

public sealed class OpenGLDrawStateCleanupTests
{
    private static readonly string[] s_vertexBindings =
    [
        "UnbindVertexArray",
        "UnbindArrayBuffer",
    ];

    private static readonly string[] s_primitiveDraw =
    [
        "UnbindVertexArray",
        "UnbindArrayBuffer",
        "UnbindProgram",
        "DisableBlending",
        "EnableDepthWrites",
    ];

    private static readonly string[] s_spriteDraw =
    [
        "ActivateTextureZero",
        "UnbindSamplerZero",
        "UnbindTexture2D",
        "UnbindVertexArray",
        "UnbindArrayBuffer",
        "UnbindProgram",
        "DisableBlending",
        "EnableDepthWrites",
    ];

    private static readonly string[] s_spriteDrawWithoutTextureUnbind =
    [
        "ActivateTextureZero",
        "UnbindSamplerZero",
        "UnbindVertexArray",
        "UnbindArrayBuffer",
        "UnbindProgram",
        "DisableBlending",
        "EnableDepthWrites",
    ];

    [Fact]
    public void InitializationSuccess_ReleasesBothVertexBindings()
    {
        RecordingOperations operations = new();
        OpenGLDrawStateCleanup cleanup = new(operations);

        cleanup.RestoreAfterInitialization(null);

        Assert.Equal(s_vertexBindings, operations.Calls);
    }

    [Fact]
    public void InitializationFailure_PreservesOriginalExceptionDespiteCleanupFailures()
    {
        RecordingOperations operations = new();
        operations.Fail("UnbindVertexArray", new InvalidOperationException("vertex array cleanup"));
        operations.Fail("UnbindArrayBuffer", new InvalidOperationException("array buffer cleanup"));

        OpenGLDrawStateCleanup cleanup = new(operations);
        Exception primary = new InvalidOperationException("resource initialization failed");

        Exception thrown = Assert.Throws<InvalidOperationException>(() =>
            cleanup.RestoreAfterInitialization(CaptureFromThrow(primary)));

        Assert.Same(primary, thrown);
        Assert.Contains(nameof(CaptureFromThrow), thrown.StackTrace);
        Assert.Equal(s_vertexBindings, operations.Calls);
    }

    [Fact]
    public void InitializationCleanup_ReportsFirstFailureWhenInitializationSucceeded()
    {
        RecordingOperations operations = new();
        InvalidOperationException first = new("vertex array cleanup");

        operations.Fail("UnbindVertexArray", first);
        operations.Fail("UnbindArrayBuffer", new InvalidOperationException("array buffer cleanup"));

        OpenGLDrawStateCleanup cleanup = new(operations);

        Exception thrown = Assert.Throws<InvalidOperationException>(() =>
            cleanup.RestoreAfterInitialization(null));

        Assert.Same(first, thrown);
        Assert.Equal(s_vertexBindings, operations.Calls);
    }

    [Fact]
    public void PrimitiveDrawSuccess_RestoresCompleteNonTextureBaseline()
    {
        RecordingOperations operations = new();
        OpenGLDrawStateCleanup cleanup = new(operations);

        cleanup.RestoreAfterDraw(null, usesTexture: false);

        Assert.Equal(s_primitiveDraw, operations.Calls);
    }

    [Fact]
    public void SpriteDrawSuccess_RestoresTextureAndRenderBaseline()
    {
        RecordingOperations operations = new();
        OpenGLDrawStateCleanup cleanup = new(operations);

        cleanup.RestoreAfterDraw(null, usesTexture: true);

        Assert.Equal(s_spriteDraw, operations.Calls);
    }

    [Fact]
    public void SpriteDrawFailure_AttemptsEveryApplicableCleanupOperationAndPreservesOriginalException()
    {
        RecordingOperations operations = new();
        operations.Fail("UnbindSamplerZero", new InvalidOperationException("sampler"));
        operations.Fail("UnbindTexture2D", new InvalidOperationException("texture"));
        operations.Fail("UnbindVertexArray", new InvalidOperationException("vertex array"));
        operations.Fail("UnbindArrayBuffer", new InvalidOperationException("array buffer"));
        operations.Fail("UnbindProgram", new InvalidOperationException("program"));
        operations.Fail("DisableBlending", new InvalidOperationException("blending"));
        operations.Fail("EnableDepthWrites", new InvalidOperationException("depth"));

        OpenGLDrawStateCleanup cleanup = new(operations);
        Exception primary = new InvalidOperationException("draw submission failed");

        Exception thrown = Assert.Throws<InvalidOperationException>(() =>
            cleanup.RestoreAfterDraw(CaptureFromThrow(primary), usesTexture: true));

        Assert.Same(primary, thrown);
        Assert.Contains(nameof(CaptureFromThrow), thrown.StackTrace);
        Assert.Equal(s_spriteDraw, operations.Calls);
    }

    [Fact]
    public void SpriteDrawFailure_TextureActivationFailureSkipsUnsafeTextureUnbind()
    {
        RecordingOperations operations = new();
        operations.Fail("ActivateTextureZero", new InvalidOperationException("texture activation"));
        operations.Fail("UnbindSamplerZero", new InvalidOperationException("sampler"));
        operations.Fail("UnbindTexture2D", new InvalidOperationException("must not execute"));
        operations.Fail("UnbindVertexArray", new InvalidOperationException("vertex array"));
        operations.Fail("UnbindArrayBuffer", new InvalidOperationException("array buffer"));
        operations.Fail("UnbindProgram", new InvalidOperationException("program"));
        operations.Fail("DisableBlending", new InvalidOperationException("blending"));
        operations.Fail("EnableDepthWrites", new InvalidOperationException("depth"));

        OpenGLDrawStateCleanup cleanup = new(operations);
        Exception primary = new InvalidOperationException("draw submission failed");

        Exception thrown = Assert.Throws<InvalidOperationException>(() =>
            cleanup.RestoreAfterDraw(CaptureFromThrow(primary), usesTexture: true));

        Assert.Same(primary, thrown);
        Assert.Contains(nameof(CaptureFromThrow), thrown.StackTrace);
        Assert.Equal(s_spriteDrawWithoutTextureUnbind, operations.Calls);
    }

    [Fact]
    public void TextureActivationFailureWithoutDrawFailure_PreservesActivationFailure()
    {
        RecordingOperations operations = new();
        InvalidOperationException expected = new("texture activation failed");

        operations.Fail("ActivateTextureZero", expected);
        operations.Fail("UnbindTexture2D", new InvalidOperationException("must not execute"));
        operations.Fail("UnbindProgram", new InvalidOperationException("program cleanup"));

        OpenGLDrawStateCleanup cleanup = new(operations);

        Exception thrown = Assert.Throws<InvalidOperationException>(() =>
            cleanup.RestoreAfterDraw(null, usesTexture: true));

        Assert.Same(expected, thrown);
        Assert.Equal(s_spriteDrawWithoutTextureUnbind, operations.Calls);
    }

    [Fact]
    public void PrimitiveDrawFailure_DoesNotAttemptTextureCleanup()
    {
        RecordingOperations operations = new();
        operations.Fail("UnbindVertexArray", new InvalidOperationException("vertex array"));

        OpenGLDrawStateCleanup cleanup = new(operations);
        Exception primary = new InvalidOperationException("vertex upload failed");

        Exception thrown = Assert.Throws<InvalidOperationException>(() => cleanup.RestoreAfterDraw(CaptureFromThrow(primary), usesTexture: false));

        Assert.Same(primary, thrown);
        Assert.Equal(s_primitiveDraw, operations.Calls);
    }

    [Theory]
    [InlineData("UnbindSamplerZero")]
    [InlineData("UnbindTexture2D")]
    [InlineData("UnbindVertexArray")]
    [InlineData("UnbindArrayBuffer")]
    [InlineData("UnbindProgram")]
    [InlineData("DisableBlending")]
    [InlineData("EnableDepthWrites")]
    public void CleanupFailureWithoutDrawFailure_PropagatesExactFailure(string operation)
    {
        RecordingOperations operations = new();
        InvalidOperationException expected = new(operation);

        operations.Fail(operation, expected);

        OpenGLDrawStateCleanup cleanup = new(operations);

        Exception thrown = Assert.Throws<InvalidOperationException>(() =>
            cleanup.RestoreAfterDraw(null, usesTexture: true));

        Assert.Same(expected, thrown);
        Assert.Equal(s_spriteDraw, operations.Calls);
    }

    [Fact]
    public void MultipleCleanupFailuresWithoutDrawFailure_PreserveFirstFailure()
    {
        RecordingOperations operations = new();
        InvalidOperationException first = new("sampler cleanup");

        operations.Fail("UnbindSamplerZero", first);
        operations.Fail("UnbindProgram", new InvalidOperationException("program cleanup"));
        operations.Fail("EnableDepthWrites", new InvalidOperationException("depth cleanup"));

        OpenGLDrawStateCleanup cleanup = new(operations);

        Exception thrown = Assert.Throws<InvalidOperationException>(() =>
            cleanup.RestoreAfterDraw(null, usesTexture: true));

        Assert.Same(first, thrown);
        Assert.Equal(s_spriteDraw, operations.Calls);
    }

    private static ExceptionDispatchInfo CaptureFromThrow(Exception exception)
    {
        try
        {
            throw exception;
        }
        catch (Exception caught)
        {
            return ExceptionDispatchInfo.Capture(caught);
        }
    }

    private sealed class RecordingOperations : IOpenGLDrawStateOperations
    {
        private readonly Dictionary<string, Exception> _failures = [];

        public List<string> Calls { get; } = [];

        public void Fail(string operation, Exception exception) => _failures.Add(operation, exception);

        public void ActivateTextureZero() => Record(nameof(ActivateTextureZero));
        public void UnbindSamplerZero() => Record(nameof(UnbindSamplerZero));
        public void UnbindTexture2D() => Record(nameof(UnbindTexture2D));
        public void UnbindVertexArray() => Record(nameof(UnbindVertexArray));
        public void UnbindArrayBuffer() => Record(nameof(UnbindArrayBuffer));
        public void UnbindProgram() => Record(nameof(UnbindProgram));
        public void DisableBlending() => Record(nameof(DisableBlending));
        public void EnableDepthWrites() => Record(nameof(EnableDepthWrites));

        private void Record(string operation)
        {
            Calls.Add(operation);

            if (_failures.TryGetValue(operation, out Exception? failure))
            {
                throw failure;
            }
        }
    }
}
