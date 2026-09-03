using System.Text;

namespace OpenConquer.Content.Tool.Tests;

/// <summary>
/// A disposable directory used to build synthetic source snapshots and content sets.
/// </summary>
internal sealed class TemporarySourceTree : IDisposable
{
    public TemporarySourceTree()
    {
        RootPath = Path.Combine(
            Path.GetTempPath(),
            "OpenConquer.Content.Tool.Tests",
            Guid.NewGuid().ToString("N")
        );

        Directory.CreateDirectory(RootPath);
    }

    public string RootPath
    {
        get;
    }

    /// <summary>A path under this tree that has deliberately not been created.</summary>
    public string ChildPath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        return Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    public void WriteText(string relativePath, string contents)
    {
        ArgumentNullException.ThrowIfNull(contents);

        WriteBytes(relativePath, Encoding.Latin1.GetBytes(contents));
    }

    public void WriteBytes(string relativePath, ReadOnlySpan<byte> contents)
    {
        string filePath = ChildPath(relativePath);
        string directoryPath = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException($"'{relativePath}' has no parent directory.");

        Directory.CreateDirectory(directoryPath);
        File.WriteAllBytes(filePath, contents);
    }

    /// <summary>
    /// Writes the files a valid 5517 source snapshot needs for the implemented closure.
    /// </summary>
    public void WriteStartupSnapshot(string backgroundFormat = "Data/Main/Logo%d.bmp")
    {
        WriteText("version.dat", "5517");
        WriteText("ini/GameSetUp.ini", "[ScreenMode]\nScreenModeRecord=2\n");
        WriteText("ini/info.ini", $"[DlgLogo]\nBgFormat={backgroundFormat}\n");
        WriteText("ini/package.ini", "data.wdf\nc3.wdf\ndata3.wdf\n");
        WriteBytes("data/main/Logo1.bmp", TestBitmap.CreateTwoByTwo());
        WriteBytes("data/main/Logo2.bmp", TestBitmap.CreateTwoByTwo());
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
