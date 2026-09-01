using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGl;

public sealed class OpenGlGraphicsDevice : IDisposable
{
    private readonly GL _gl;
    private bool _disposed;

    public OpenGlGraphicsDevice(OpenGlProcAddressResolver resolveProcAddress)
    {
        ArgumentNullException.ThrowIfNull(resolveProcAddress);

        _gl = GL.GetApi(functionName => resolveProcAddress(functionName));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _gl.Dispose();
        _disposed = true;
    }
}
