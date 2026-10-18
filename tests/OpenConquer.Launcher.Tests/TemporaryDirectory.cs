namespace OpenConquer.Launcher.Tests;

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory(string prefix = "openconquer-launcher-tests")
    {
        RootPath = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath
    {
        get;
    }

    public string CreateDirectory(string name)
    {
        string path = Path.Combine(RootPath, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(RootPath, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
