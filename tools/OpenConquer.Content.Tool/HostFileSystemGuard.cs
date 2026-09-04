namespace OpenConquer.Content.Tool;

internal static class HostFileSystemGuard
{
    public static DirectoryInfo RequireDirectory(string path, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        DirectoryInfo directory = new(path);

        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException($"The {description} '{path}' does not exist.");
        }

        RequireNotLinked(directory, description, path);

        return directory;
    }

    public static FileInfo RequireFile(string path, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        FileInfo file = new(path);

        if (!file.Exists)
        {
            throw new FileNotFoundException($"The {description} '{path}' does not exist.", path);
        }

        RequireNotLinked(file, description, path);

        return file;
    }

    public static void RequireNotLinked(FileSystemInfo entry, string description, string path)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.LinkTarget is not null || (entry.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"The {description} '{path}' is a symbolic link or reparse point.");
        }
    }
}
