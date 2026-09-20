namespace OpenConquer.Platform.OpenGL;

public interface IOpenGLContext
{
    nint GetProcAddress(string functionName);
}
