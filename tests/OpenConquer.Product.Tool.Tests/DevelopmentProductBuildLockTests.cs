namespace OpenConquer.Product.Tool.Tests;

public sealed class DevelopmentProductBuildLockTests
{
    [Fact]
    public void AcquireCreatesDevelopmentStateDirectoryAndLock()
    {
        using TemporaryDirectory temporary = new();

        string stateRootPath = Path.Combine(temporary.RootPath, "development");

        using FileStream buildLock = DevelopmentProductBuildLock.Acquire(stateRootPath);

        Assert.True(Directory.Exists(stateRootPath));
        Assert.True(File.Exists(Path.Combine(stateRootPath, DevelopmentProductBuildLock.FileName)));
    }

    [Fact]
    public void AcquireRejectsConcurrentBuild()
    {
        using TemporaryDirectory temporary = new();

        string stateRootPath = Path.Combine(temporary.RootPath, "development");

        using FileStream first = DevelopmentProductBuildLock.Acquire(stateRootPath);

        Assert.Throws<InvalidOperationException>(() =>
            DevelopmentProductBuildLock.Acquire(stateRootPath)
        );
    }

    [Fact]
    public void AcquireCanBeTakenAgainAfterRelease()
    {
        using TemporaryDirectory temporary = new();

        string stateRootPath = Path.Combine(temporary.RootPath, "development");

        using (DevelopmentProductBuildLock.Acquire(stateRootPath))
        {
        }

        using FileStream second = DevelopmentProductBuildLock.Acquire(stateRootPath);

        Assert.True(second.CanRead);
        Assert.True(second.CanWrite);
    }

    [Fact]
    public void AcquireRejectsLinkedStateDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory temporary = new();

        string targetPath = Path.Combine(temporary.RootPath, "target");
        string linkedPath = Path.Combine(temporary.RootPath, "development");

        Directory.CreateDirectory(targetPath);
        Directory.CreateSymbolicLink(linkedPath, targetPath);

        Assert.Throws<InvalidDataException>(() => DevelopmentProductBuildLock.Acquire(linkedPath));
    }

    [Fact]
    public void AcquireRejectsLinkedLockFile()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory temporary = new();

        string stateRootPath = Path.Combine(temporary.RootPath, "development");
        Directory.CreateDirectory(stateRootPath);

        string targetPath = Path.Combine(temporary.RootPath, "lock-target");
        string lockPath = Path.Combine(stateRootPath, DevelopmentProductBuildLock.FileName);

        File.WriteAllText(targetPath, string.Empty);
        File.CreateSymbolicLink(lockPath, targetPath);

        Assert.Throws<InvalidDataException>(() =>
            DevelopmentProductBuildLock.Acquire(stateRootPath)
        );
    }

    [Fact]
    public void AcquireRejectsDirectoryAtLockPath()
    {
        using TemporaryDirectory temporary = new();

        string stateRootPath = Path.Combine(temporary.RootPath, "development");
        Directory.CreateDirectory(stateRootPath);
        Directory.CreateDirectory(
            Path.Combine(stateRootPath, DevelopmentProductBuildLock.FileName)
        );

        Assert.Throws<InvalidDataException>(() =>
            DevelopmentProductBuildLock.Acquire(stateRootPath)
        );
    }

    [Fact]
    public void AcquireEnforcesPrivateUnixPermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory temporary = new();

        string stateRootPath = Path.Combine(temporary.RootPath, "development");

        using FileStream buildLock = DevelopmentProductBuildLock.Acquire(stateRootPath);

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(stateRootPath)
        );

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(Path.Combine(stateRootPath, DevelopmentProductBuildLock.FileName))
        );
    }

    [Fact]
    public void AcquireRepairsExistingUnixPermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory temporary = new();

        string stateRootPath = Path.Combine(temporary.RootPath, "development");
        Directory.CreateDirectory(stateRootPath);

        string lockPath = Path.Combine(stateRootPath, DevelopmentProductBuildLock.FileName);

        File.WriteAllText(lockPath, string.Empty);

        File.SetUnixFileMode(
            stateRootPath,
            UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead
                | UnixFileMode.OtherExecute
        );

        File.SetUnixFileMode(
            lockPath,
            UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.GroupRead
                | UnixFileMode.OtherRead
        );

        using FileStream buildLock = DevelopmentProductBuildLock.Acquire(stateRootPath);

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(stateRootPath)
        );

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(lockPath)
        );
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            $"openconquer-development-build-lock-{Guid.NewGuid():N}"
        );

        public TemporaryDirectory()
        {
            Directory.CreateDirectory(_path);
        }

        public string RootPath => _path;

        public void Dispose()
        {
            try
            {
                Directory.Delete(_path, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
