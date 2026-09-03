using System.Text;

namespace OpenConquer.Content.Tests;

internal sealed class TemporaryContentDirectory : IDisposable
{
    public TemporaryContentDirectory()
    {
        RootPath = Path.Combine(
            Path.GetTempPath(),
            "OpenConquer.Content.Tests",
            Guid.NewGuid().ToString("N")
        );

        Directory.CreateDirectory(RootPath);
    }

    public string RootPath
    {
        get;
    }

    public string WriteFile(string relativePath, string contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(contents);

        string normalizedRelativePath = relativePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        string filePath = Path.Combine(RootPath, normalizedRelativePath);
        string? directoryPath = Path.GetDirectoryName(filePath);

        if (directoryPath is not null)
        {
            Directory.CreateDirectory(directoryPath);
        }

        File.WriteAllText(filePath, contents, Encoding.Latin1);

        return filePath;
    }

    public string WriteFile(string relativePath, ReadOnlySpan<byte> contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string normalizedRelativePath = relativePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

        string filePath = Path.Combine(RootPath, normalizedRelativePath);
        string? directoryPath = Path.GetDirectoryName(filePath);

        if (directoryPath is not null)
        {
            Directory.CreateDirectory(directoryPath);
        }

        File.WriteAllBytes(filePath, contents);

        return filePath;
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
