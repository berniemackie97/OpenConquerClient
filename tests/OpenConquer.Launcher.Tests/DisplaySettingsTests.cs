using System.Diagnostics;
using OpenConquer.Launcher.Settings;

namespace OpenConquer.Launcher.Tests;

public sealed class DisplaySettingsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("oc-settings-").FullName;
    private string FilePath => Path.Combine(_root, "display.json");
    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task MissingSettings_UseDefaultsWithoutCreatingFiles()
    {
        DisplaySettingsStore store = new(Path.Combine(_root, "missing"));
        SettingsReadResult.Loaded loaded = Assert.IsType<SettingsReadResult.Loaded>(await store.LoadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(DisplayPreferences.Default, loaded.Preferences);
        Assert.Null(loaded.Revision);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public async Task Save_RoundTripsValidatedPreferencesAndRestrictsUnixFile(int mode, int scaling)
    {
        DisplaySettingsStore store = new(_root);
        DisplayPreferences preferences = new(2560, 1440, (GameWindowMode)mode, (GamePresentation)scaling);
        Assert.Null(await store.SaveAsync(preferences, null, false, TestContext.Current.CancellationToken));
        SettingsReadResult.Loaded loaded = Assert.IsType<SettingsReadResult.Loaded>(await new DisplaySettingsStore(_root).LoadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(preferences, loaded.Preferences);
        Assert.NotNull(loaded.Revision);
        Assert.Single(Directory.EnumerateFiles(_root));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(FilePath));
        }
    }

    [Theory]
    [InlineData(0, 720)]
    [InlineData(1280, -1)]
    [InlineData(16385, 1)]
    [InlineData(7680, 4321)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void Dimensions_RejectUnsupportedSizesWithoutOverflow(int width, int height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new DisplayPreferences(width, height, GameWindowMode.Resizable, GamePresentation.Fit));

    [Fact]
    public void Preferences_RejectUndefinedEnumsAndAcceptMaximumArea()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DisplayPreferences(1280, 720, (GameWindowMode)4, GamePresentation.Fit));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DisplayPreferences(1280, 720, GameWindowMode.Resizable, (GamePresentation)4));
        Assert.Equal(7680, new DisplayPreferences(7680, 4320, GameWindowMode.Fullscreen, GamePresentation.Fit).Width);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"schemaVersion\":\"1\"}")]
    [InlineData("{\"schemaVersion\":1,\"width\":1280,\"height\":720,\"windowMode\":0,\"presentation\":\"fit\"}")]
    [InlineData("{\"schemaVersion\":1,\"width\":1280,\"height\":720,\"windowMode\":\"resizable\",\"presentation\":\"fit\",\"extra\":true}")]
    [InlineData("{\"schemaVersion\":1,\"width\":1280,\"height\":720,\"windowMode\":\"resizable\",\"presentation\":\"fit\",\"width\":1}")]
    [InlineData("{\"schemaVersion\":1,\"width\":1280,\"height\":720,\"windowMode\":\"resizable\",\"presentation\":\"unknown\"}")]
    [InlineData("{\"schemaVersion\":1,\"width\":1280.5,\"height\":720,\"windowMode\":\"resizable\",\"presentation\":\"fit\"}")]
    [InlineData("{\"schemaVersion\":1,\"width\":16384,\"height\":16384,\"windowMode\":\"resizable\",\"presentation\":\"fit\"}")]
    public async Task DamagedSettings_ArePreservedUntilExplicitReset(string json)
    {
        await File.WriteAllTextAsync(FilePath, json, TestContext.Current.CancellationToken);
        DisplaySettingsStore store = new(_root);
        SettingsReadResult.Rejected rejected = Assert.IsType<SettingsReadResult.Rejected>(await store.LoadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(SettingsIssue.Invalid, rejected.Issue);
        Assert.Equal(SettingsIssue.Invalid, await store.SaveAsync(DisplayPreferences.Default, rejected.Revision, false, TestContext.Current.CancellationToken));
        Assert.Equal(json, await File.ReadAllTextAsync(FilePath, TestContext.Current.CancellationToken));
        Assert.Null(await store.SaveAsync(DisplayPreferences.Default, rejected.Revision, true, TestContext.Current.CancellationToken));
        Assert.IsType<SettingsReadResult.Loaded>(await store.LoadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NewerSettings_AreNeverOverwrittenEvenByReset()
    {
        const string json = "{\"schemaVersion\":2,\"futureData\":[1,2,3]}";
        await File.WriteAllTextAsync(FilePath, json, TestContext.Current.CancellationToken);
        DisplaySettingsStore store = new(_root);
        SettingsReadResult.Rejected rejected = Assert.IsType<SettingsReadResult.Rejected>(await store.LoadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(SettingsIssue.NewerVersion, rejected.Issue);
        Assert.Equal(SettingsIssue.NewerVersion, await store.SaveAsync(DisplayPreferences.Default, rejected.Revision, true, TestContext.Current.CancellationToken));
        Assert.Equal(json, await File.ReadAllTextAsync(FilePath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OversizedAndLinkedSettings_AreNotReadOrReplaced()
    {
        byte[] bytes = new byte[DisplaySettingsDocument.MaximumLength + 1];
        await File.WriteAllBytesAsync(FilePath, bytes, TestContext.Current.CancellationToken);
        DisplaySettingsStore store = new(_root);
        Assert.Equal(SettingsIssue.Unavailable, Assert.IsType<SettingsReadResult.Rejected>(await store.LoadAsync(TestContext.Current.CancellationToken)).Issue);
        Assert.Equal(SettingsIssue.Unavailable, await store.SaveAsync(DisplayPreferences.Default, null, true, TestContext.Current.CancellationToken));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(FilePath, TestContext.Current.CancellationToken));
        if (OperatingSystem.IsWindows())
        {
            return; // Symlink creation requires privileges not granted to all Windows test users.
        }

        File.Delete(FilePath);
        string target = Path.Combine(_root, "target");
        File.WriteAllText(target, "preserve");
        File.CreateSymbolicLink(FilePath, target);
        Assert.Equal(SettingsIssue.Unavailable, await store.SaveAsync(DisplayPreferences.Default, null, true, TestContext.Current.CancellationToken));
        Assert.Equal("preserve", File.ReadAllText(target));
        Assert.Equal(target, new FileInfo(FilePath).LinkTarget);
    }

    [Fact]
    public async Task ConcurrentSaves_RejectStaleDraftWithoutLosingCommittedPreferences()
    {
        DisplaySettingsStore store = new(_root);
        Task<SettingsIssue?>[] saves = Enumerable.Range(0, 8).Select(index => store.SaveAsync(
            new DisplayPreferences(1280 + index, 720, GameWindowMode.Resizable, GamePresentation.Fit), null, false, TestContext.Current.CancellationToken)).ToArray();
        SettingsIssue?[] results = await Task.WhenAll(saves);
        int winner = Array.FindIndex(results, result => result is null);
        Assert.Single(results, result => result is null);
        Assert.All(results.Where(result => result is not null), result => Assert.Equal(SettingsIssue.Changed, result));
        SettingsReadResult.Loaded loaded = Assert.IsType<SettingsReadResult.Loaded>(await store.LoadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1280 + winner, loaded.Preferences.Width);
        Assert.Single(Directory.EnumerateFiles(_root));
    }

    [Fact]
    public async Task CanceledSave_PreservesExistingBytesAndLeavesNoTemporaryFiles()
    {
        DisplaySettingsStore store = new(_root);
        Assert.Null(await store.SaveAsync(DisplayPreferences.Default, null, false, TestContext.Current.CancellationToken));
        byte[] before = await File.ReadAllBytesAsync(FilePath, TestContext.Current.CancellationToken);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(DisplayPreferences.Default, null, false, cancellation.Token));
        Assert.Equal(before, await File.ReadAllBytesAsync(FilePath, TestContext.Current.CancellationToken));
        Assert.Single(Directory.EnumerateFiles(_root));
    }

    [Fact]
    public async Task ExternalEdit_InvalidatesTheLoadedDraft()
    {
        DisplaySettingsStore store = new(_root);
        Assert.Null(await store.SaveAsync(DisplayPreferences.Default, null, false, TestContext.Current.CancellationToken));
        SettingsReadResult.Loaded original = Assert.IsType<SettingsReadResult.Loaded>(await store.LoadAsync(TestContext.Current.CancellationToken));
        byte[] external = DisplaySettingsDocument.Serialize(new DisplayPreferences(1920, 1080, GameWindowMode.Fixed, GamePresentation.Stretch));
        await File.WriteAllBytesAsync(FilePath, external, TestContext.Current.CancellationToken);
        Assert.Equal(SettingsIssue.Changed, await store.SaveAsync(DisplayPreferences.Default, original.Revision, false, TestContext.Current.CancellationToken));
        Assert.Equal(external, await File.ReadAllBytesAsync(FilePath, TestContext.Current.CancellationToken));
        await File.WriteAllTextAsync(FilePath, "damaged by external editor", TestContext.Current.CancellationToken);
        Assert.Equal(SettingsIssue.Changed, await store.SaveAsync(DisplayPreferences.Default, original.Revision, false, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Readers_ObserveCompleteDocumentsDuringReplacement()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        DisplayPreferences[] generations = [.. Enumerable.Range(0, 4).Select(index => new DisplayPreferences(1280 + index, 720, GameWindowMode.Resizable, GamePresentation.Fit))];
        DisplaySettingsStore writer = new(_root);
        Assert.Null(await writer.SaveAsync(generations[0], null, false, token));
        List<FileStream> readers = [];
        try
        {
            for (int index = 1; index < generations.Length; index++)
            {
                // Each reader is part-way through one generation when the next replaces it.
                FileStream reader = SettingsFile.OpenRead(FilePath);
                readers.Add(reader);
                Assert.Equal('{', reader.ReadByte());
                SettingsReadResult.Loaded previous = Assert.IsType<SettingsReadResult.Loaded>(await writer.LoadAsync(token));
                Assert.Null(await writer.SaveAsync(generations[index], previous.Revision, false, token));
            }

            for (int index = 0; index < readers.Count; index++)
            {
                byte[] observed = new byte[readers[index].Length];
                readers[index].Position = 0;
                readers[index].ReadExactly(observed);
                Assert.Equal(DisplaySettingsDocument.Serialize(generations[index]), observed);
            }
        }
        finally
        {
            foreach (FileStream reader in readers)
            {
                reader.Dispose();
            }
        }

        SettingsReadResult.Loaded current = Assert.IsType<SettingsReadResult.Loaded>(await new DisplaySettingsStore(_root).LoadAsync(token));
        Assert.Equal(generations[^1], current.Preferences);
        Assert.Single(Directory.EnumerateFiles(_root));
    }

    [Fact]
    public async Task ConcurrentReaders_NeverObserveAMissingOrLockedDocument()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        DisplaySettingsStore writer = new(_root);
        Assert.Null(await writer.SaveAsync(DisplayPreferences.Default, null, false, token));
        TaskCompletionSource replaced = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource[] ready = [new(TaskCreationOptions.RunContinuationsAsynchronously), new(TaskCreationOptions.RunContinuationsAsynchronously)];
        TaskCompletionSource[] observedReplacement = [new(TaskCreationOptions.RunContinuationsAsynchronously), new(TaskCreationOptions.RunContinuationsAsynchronously)];
        async Task ReadAsync(int index)
        {
            DisplaySettingsStore reader = new(_root);
            while (!replaced.Task.IsCompleted)
            {
                // A missing document reads as the defaults without a revision.
                SettingsReadResult.Loaded read = Assert.IsType<SettingsReadResult.Loaded>(await reader.LoadAsync(token));
                Assert.NotNull(read.Revision);
                Assert.InRange(read.Preferences.Width, 1280, 1330);
                ready[index].TrySetResult();
                if (read.Preferences.Width > 1280)
                {
                    observedReplacement[index].TrySetResult();
                }
            }
        }

        Task[] readers = [ReadAsync(0), ReadAsync(1)];
        try
        {
            // Require completed reads before writing, then prove both readers see a replacement
            // while further writes remain. Readers continue independently throughout the burst.
            await Task.WhenAll(ready.Select(signal => signal.Task)).WaitAsync(TimeSpan.FromSeconds(10), token);
            for (int index = 1; index <= 50; index++)
            {
                SettingsReadResult.Loaded previous = Assert.IsType<SettingsReadResult.Loaded>(await writer.LoadAsync(token));
                Assert.Null(await writer.SaveAsync(new DisplayPreferences(1280 + index, 720, GameWindowMode.Resizable, GamePresentation.Fit), previous.Revision, false, token));
                if (index == 1)
                {
                    await Task.WhenAll(observedReplacement.Select(signal => signal.Task)).WaitAsync(TimeSpan.FromSeconds(10), token);
                }
            }
        }
        finally
        {
            replaced.SetResult();
            await Task.WhenAll(readers);
        }

        Assert.Single(Directory.EnumerateFiles(_root));
    }

    [Fact]
    public async Task ReaderWithoutDeleteSharing_BlocksReplacementWithoutLosingTheDocument()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // Only Windows enforces share modes.
        }

        CancellationToken token = TestContext.Current.CancellationToken;
        DisplaySettingsStore store = new(_root);
        Assert.Null(await store.SaveAsync(DisplayPreferences.Default, null, false, token));
        SettingsReadResult.Loaded loaded = Assert.IsType<SettingsReadResult.Loaded>(await store.LoadAsync(token));
        byte[] before = await File.ReadAllBytesAsync(FilePath, token);
        using (FileStream reader = new(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            DisplayPreferences next = new(1920, 1080, GameWindowMode.Fixed, GamePresentation.Stretch);
            Assert.Equal(SettingsIssue.Unavailable, await store.SaveAsync(next, loaded.Revision, false, token));
        }

        Assert.Equal(before, await File.ReadAllBytesAsync(FilePath, token));
        Assert.Single(Directory.EnumerateFiles(_root));
    }

    [Fact]
    public async Task UnavailableDestination_DoesNotClaimSuccessOrModifyOtherFiles()
    {
        File.WriteAllText(FilePath, "not a directory");
        DisplaySettingsStore store = new(FilePath);
        Assert.Equal(SettingsIssue.Unavailable, await store.SaveAsync(DisplayPreferences.Default, null, false, TestContext.Current.CancellationToken));
        Assert.Equal("not a directory", File.ReadAllText(FilePath));
        Assert.Equal(SettingsIssue.Unavailable, await new DisplaySettingsStore(null).SaveAsync(DisplayPreferences.Default, null, false, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UnixFifo_IsRejectedWithoutWaitingForAWriter()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        ProcessStartInfo start = new("mkfifo")
        {
            UseShellExecute = false
        };
        start.ArgumentList.Add(FilePath);
        using Process process = Process.Start(start)!;
        Assert.True(process.WaitForExit(5000));
        Assert.Equal(0, process.ExitCode);
        SettingsReadResult result = await new DisplaySettingsStore(_root).LoadAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(SettingsIssue.Unavailable, Assert.IsType<SettingsReadResult.Rejected>(result).Issue);
    }

    [Theory]
    [InlineData("/custom/config", "/home/player", "/custom/config")]
    [InlineData("relative", "/home/player", "/home/player/.config")]
    [InlineData(null, "/home/player", "/home/player/.config")]
    public void LinuxConfiguration_UsesAbsoluteXdgOrHomeFallback(string? configured, string home, string expected)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(expected, LauncherConfigurationDirectory.LinuxConfigurationHome(configured, home));
        }
    }
}
