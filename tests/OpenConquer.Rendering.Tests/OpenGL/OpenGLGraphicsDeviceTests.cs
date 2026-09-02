using OpenConquer.Rendering.OpenGL;

namespace OpenConquer.Rendering.Tests.OpenGL;

/// <summary>
/// Covers the parts of <see cref="OpenGLGraphicsDevice"/> that are reachable without a live
/// OpenGL context.
/// </summary>
/// <remarks>
/// The constructor calls <c>GL.GetApi</c> immediately after argument validation, and every
/// member beyond that point issues real driver calls. Those paths — version and profile
/// validation, the driver string reads, renderer creation and disposal — need a current
/// context and are not exercised here. Standing them up means a headless context (EGL
/// surfaceless or an offscreen window) in a separate integration suite; asserting them
/// against a stub resolver would only test the stub.
/// </remarks>
public sealed class OpenGLGraphicsDeviceTests
{
    [Fact]
    public void Constructor_ThrowsArgumentNullExceptionWhenResolverIsNull()
    {
        // Validation runs before GL.GetApi, so this is observable with no context present.
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => new OpenGLGraphicsDevice(resolveProcAddress: null!));

        Assert.Equal("resolveProcAddress", exception.ParamName);
    }
}
