using System.Globalization;

namespace OpenConquer.Product.Tool.Tests;

public sealed class DevelopmentReleaseSequenceTests
{
    [Fact]
    public void ReserveStartsAtOneAndPersistsReservation()
    {
        using TemporaryDirectory temporary = new();

        ulong sequence = DevelopmentReleaseSequence.Reserve(temporary.RootPath);

        Assert.Equal(1UL, sequence);
        Assert.Equal(
            "1",
            File.ReadAllText(Path.Combine(temporary.RootPath, DevelopmentReleaseSequence.FileName))
        );
    }

    [Fact]
    public void ReserveIncrementsPersistedSequence()
    {
        using TemporaryDirectory temporary = new();

        Assert.Equal(1UL, DevelopmentReleaseSequence.Reserve(temporary.RootPath));
        Assert.Equal(2UL, DevelopmentReleaseSequence.Reserve(temporary.RootPath));
        Assert.Equal(3UL, DevelopmentReleaseSequence.Reserve(temporary.RootPath));

        Assert.Equal(
            "3",
            File.ReadAllText(Path.Combine(temporary.RootPath, DevelopmentReleaseSequence.FileName))
        );
    }

    [Fact]
    public void ReserveContinuesExistingSequence()
    {
        using TemporaryDirectory temporary = new();

        string sequencePath = Path.Combine(temporary.RootPath, DevelopmentReleaseSequence.FileName);

        File.WriteAllText(sequencePath, "41");

        Assert.Equal(42UL, DevelopmentReleaseSequence.Reserve(temporary.RootPath));
        Assert.Equal("42", File.ReadAllText(sequencePath));
    }

    [Fact]
    public void ReserveAdvancesBeyondMinimumWhenPersistentStateIsMissing()
    {
        using TemporaryDirectory temporary = new();

        Assert.Equal(
            42UL,
            DevelopmentReleaseSequence.Reserve(temporary.RootPath, minimumExclusive: 41)
        );

        Assert.Equal(
            "42",
            File.ReadAllText(Path.Combine(temporary.RootPath, DevelopmentReleaseSequence.FileName))
        );
    }

    [Fact]
    public void ReserveAdvancesBeyondMinimumWhenMinimumExceedsPersistentState()
    {
        using TemporaryDirectory temporary = new();

        string sequencePath = Path.Combine(temporary.RootPath, DevelopmentReleaseSequence.FileName);

        File.WriteAllText(sequencePath, "12");

        Assert.Equal(
            42UL,
            DevelopmentReleaseSequence.Reserve(temporary.RootPath, minimumExclusive: 41)
        );

        Assert.Equal("42", File.ReadAllText(sequencePath));
    }

    [Fact]
    public void ReserveUsesPersistentStateWhenItExceedsMinimum()
    {
        using TemporaryDirectory temporary = new();

        string sequencePath = Path.Combine(temporary.RootPath, DevelopmentReleaseSequence.FileName);

        File.WriteAllText(sequencePath, "50");

        Assert.Equal(
            51UL,
            DevelopmentReleaseSequence.Reserve(temporary.RootPath, minimumExclusive: 41)
        );

        Assert.Equal("51", File.ReadAllText(sequencePath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("01")]
    [InlineData("+1")]
    [InlineData("1\n")]
    [InlineData("not-a-sequence")]
    public void ReserveRejectsInvalidExistingSequenceWithoutReplacingIt(string value)
    {
        using TemporaryDirectory temporary = new();

        string sequencePath = Path.Combine(temporary.RootPath, DevelopmentReleaseSequence.FileName);

        File.WriteAllText(sequencePath, value);

        Assert.Throws<InvalidDataException>(() =>
            DevelopmentReleaseSequence.Reserve(temporary.RootPath)
        );

        Assert.Equal(value, File.ReadAllText(sequencePath));
    }

    [Fact]
    public void ReserveRejectsExhaustedSequenceWithoutReplacingIt()
    {
        using TemporaryDirectory temporary = new();

        string sequencePath = Path.Combine(temporary.RootPath, DevelopmentReleaseSequence.FileName);

        string maximum = ulong.MaxValue.ToString(CultureInfo.InvariantCulture);

        File.WriteAllText(sequencePath, maximum);

        Assert.Throws<InvalidOperationException>(() =>
            DevelopmentReleaseSequence.Reserve(temporary.RootPath)
        );

        Assert.Equal(maximum, File.ReadAllText(sequencePath));
    }

    [Fact]
    public void ReserveRejectsExhaustedMinimumWithoutCreatingState()
    {
        using TemporaryDirectory temporary = new();

        Assert.Throws<InvalidOperationException>(() =>
            DevelopmentReleaseSequence.Reserve(temporary.RootPath, minimumExclusive: ulong.MaxValue)
        );

        Assert.False(
            File.Exists(Path.Combine(temporary.RootPath, DevelopmentReleaseSequence.FileName))
        );
    }

    [Fact]
    public void ReserveRejectsMissingStateRoot()
    {
        using TemporaryDirectory temporary = new();

        string missingRoot = Path.Combine(temporary.RootPath, "missing");

        Assert.Throws<DirectoryNotFoundException>(() =>
            DevelopmentReleaseSequence.Reserve(missingRoot)
        );
    }

    [Fact]
    public void ReserveRejectsLinkedSequenceState()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory temporary = new();

        string targetPath = Path.Combine(temporary.RootPath, "sequence-target.txt");
        string sequencePath = Path.Combine(temporary.RootPath, DevelopmentReleaseSequence.FileName);

        File.WriteAllText(targetPath, "17");
        File.CreateSymbolicLink(sequencePath, targetPath);

        Assert.Throws<InvalidDataException>(() =>
            DevelopmentReleaseSequence.Reserve(temporary.RootPath)
        );

        Assert.Equal("17", File.ReadAllText(targetPath));
    }

    [Fact]
    public void ReserveRejectsDirectoryAtSequenceStatePath()
    {
        using TemporaryDirectory temporary = new();

        Directory.CreateDirectory(
            Path.Combine(temporary.RootPath, DevelopmentReleaseSequence.FileName)
        );

        Assert.Throws<InvalidDataException>(() =>
            DevelopmentReleaseSequence.Reserve(temporary.RootPath)
        );
    }

    [Fact]
    public void ReserveRejectsLinkedLock()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory temporary = new();

        string targetPath = Path.Combine(temporary.RootPath, "lock-target");
        string lockPath = Path.Combine(temporary.RootPath, DevelopmentReleaseSequence.LockFileName);

        File.WriteAllText(targetPath, string.Empty);
        File.CreateSymbolicLink(lockPath, targetPath);

        Assert.Throws<InvalidDataException>(() =>
            DevelopmentReleaseSequence.Reserve(temporary.RootPath)
        );
    }

    [Fact]
    public void ReserveRejectsConcurrentReservation()
    {
        using TemporaryDirectory temporary = new();

        string lockPath = Path.Combine(temporary.RootPath, DevelopmentReleaseSequence.LockFileName);

        using FileStream heldLock = new(
            lockPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None
        );

        Assert.Throws<InvalidOperationException>(() =>
            DevelopmentReleaseSequence.Reserve(temporary.RootPath)
        );

        Assert.False(
            File.Exists(Path.Combine(temporary.RootPath, DevelopmentReleaseSequence.FileName))
        );
    }

    [Fact]
    public void ReserveEnforcesPrivateUnixPermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory temporary = new();

        _ = DevelopmentReleaseSequence.Reserve(temporary.RootPath);

        UnixFileMode expected = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        Assert.Equal(
            expected,
            File.GetUnixFileMode(
                Path.Combine(temporary.RootPath, DevelopmentReleaseSequence.FileName)
            )
        );

        Assert.Equal(
            expected,
            File.GetUnixFileMode(
                Path.Combine(temporary.RootPath, DevelopmentReleaseSequence.LockFileName)
            )
        );
    }

    [Fact]
    public void ReserveRepairsExistingUnixPermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory temporary = new();

        string sequencePath = Path.Combine(temporary.RootPath, DevelopmentReleaseSequence.FileName);

        string lockPath = Path.Combine(temporary.RootPath, DevelopmentReleaseSequence.LockFileName);

        File.WriteAllText(sequencePath, "9");
        File.WriteAllText(lockPath, string.Empty);

        UnixFileMode permissive =
            UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.GroupRead
            | UnixFileMode.OtherRead;

        File.SetUnixFileMode(sequencePath, permissive);
        File.SetUnixFileMode(lockPath, permissive);

        Assert.Equal(10UL, DevelopmentReleaseSequence.Reserve(temporary.RootPath));

        UnixFileMode expected = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        Assert.Equal(expected, File.GetUnixFileMode(sequencePath));
        Assert.Equal(expected, File.GetUnixFileMode(lockPath));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            $"openconquer-development-sequence-{Guid.NewGuid():N}"
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
