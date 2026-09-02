using OpenConquer.Rendering.OpenGL;

namespace OpenConquer.Rendering.Tests.OpenGL;

/// <summary>
/// Pins the shape of the seam between Rendering and whichever platform supplies OpenGL
/// entry points.
/// </summary>
/// <remarks>
/// This delegate is the only thing Rendering asks of a host, so its signature is a contract:
/// widening it later would pull windowing concerns across the boundary the Platform project
/// exists to hold.
/// </remarks>
public sealed class OpenGLProcAddressResolverTests
{
    [Fact]
    public void Invocation_PassesTheRequestedFunctionNameThrough()
    {
        string? requested = null;

        OpenGLProcAddressResolver resolver = functionName =>
        {
            requested = functionName;
            return 0x1234;
        };

        nint address = resolver("glClear");

        Assert.Equal("glClear", requested);
        Assert.Equal(0x1234, address);
    }

    [Fact]
    public void Invocation_PropagatesAZeroAddressForAnUnresolvedFunction()
    {
        // A driver reports "no such entry point" by returning a null address rather than
        // failing, so the delegate must be able to carry that without special casing.
        OpenGLProcAddressResolver resolver = _ => nint.Zero;

        Assert.Equal(nint.Zero, resolver("glNotAReal_Function"));
    }

    [Fact]
    public void Invocation_PropagatesResolverFailures()
    {
        OpenGLProcAddressResolver resolver = _ => throw new InvalidOperationException("no context");

        Assert.Throws<InvalidOperationException>(() => resolver("glClear"));
    }
}
