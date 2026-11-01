namespace OpenConquer.Testing;

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory(string prefix = "openconquer-tests")
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
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
