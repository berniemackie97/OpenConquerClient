namespace OpenConquer.Platform;

public interface IOpenGLContext
{
    nint GetProcAddress(string functionName);
}
