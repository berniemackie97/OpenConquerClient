namespace OpenConquer.Platform;

public interface IOpenGlContext
{
    bool IsCurrent
    {
        get;
    }

    nint GetProcAddress(string functionName);

    void MakeCurrent();

    void ClearCurrent();
}
