using System.Text;

namespace OpenConquer.Client.Tests.UI.Hud;

internal sealed class TemporaryContentDirectory : IDisposable
{
    public TemporaryContentDirectory()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "OpenConquer.Client.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath
    {
        get;
    }

    public void WriteText(string relativePath, string contents) => WriteBytes(relativePath, Encoding.Latin1.GetBytes(contents));

    public void WriteBytes(string relativePath, ReadOnlySpan<byte> contents)
    {
        string path = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        string directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException($"'{relativePath}' has no parent directory.");

        Directory.CreateDirectory(directory);
        File.WriteAllBytes(path, contents);
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
