using System.Text;
using OpenConquer.Launcher.Play;

namespace OpenConquer.Launcher.Tests.Play;

public sealed class GameModeStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("oc-play-").FullName;

    private string FilePath => Path.Combine(_root, "play.json");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task MissingFile_ReadsAsUnusedWithoutCreatingFiles()
    {
        GameModeStore store = new(Path.Combine(_root, "missing"));

        GameModeRecord record = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new GameModeRecord(default, GameMode.Standard), record);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
    }

    [Fact]
    public async Task RecordStart_CountsAndRemembersTheLastMode()
    {
        GameModeStore store = new(_root);

        Assert.True(await store.RecordStartAsync(GameMode.Low, TestContext.Current.CancellationToken));
        Assert.True(await store.RecordStartAsync(GameMode.High, TestContext.Current.CancellationToken));

        GameModeRecord record = await new GameModeStore(_root).LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new GameModeRecord(new GameModeUsage(1, 0, 1), GameMode.High), record);
        Assert.Single(Directory.EnumerateFiles(_root));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(FilePath));
        }
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"schemaVersion\":1,\"highTimes\":-1,\"standardTimes\":0,\"lowTimes\":0,\"lastMode\":\"high\"}")]
    [InlineData("{\"schemaVersion\":1,\"highTimes\":0,\"standardTimes\":0,\"lowTimes\":0,\"lastMode\":\"ultra\"}")]
    [InlineData("{\"schemaVersion\":1,\"highTimes\":0,\"highTimes\":0,\"standardTimes\":0,\"lowTimes\":0,\"lastMode\":\"low\"}")]
    [InlineData("{\"schemaVersion\":1,\"highTimes\":0,\"standardTimes\":0,\"lowTimes\":0,\"lastMode\":\"low\",\"extra\":1}")]
    public async Task DamagedFile_ReadsAsUnusedAndIsReplacedOnTheNextStart(string contents)
    {
        await File.WriteAllTextAsync(FilePath, contents, TestContext.Current.CancellationToken);
        GameModeStore store = new(_root);

        Assert.Equal(default, (await store.LoadAsync(TestContext.Current.CancellationToken)).Usage);
        Assert.True(await store.RecordStartAsync(GameMode.Standard, TestContext.Current.CancellationToken));

        Assert.Equal(new GameModeUsage(0, 1, 0), (await store.LoadAsync(TestContext.Current.CancellationToken)).Usage);
    }

    [Fact]
    public async Task NewerOrOversizedFile_IsNeverOverwritten()
    {
        const string newer = "{\"schemaVersion\":2}";
        await File.WriteAllTextAsync(FilePath, newer, TestContext.Current.CancellationToken);
        Assert.False(await new GameModeStore(_root).RecordStartAsync(GameMode.High, TestContext.Current.CancellationToken));
        Assert.Equal(newer, await File.ReadAllTextAsync(FilePath, TestContext.Current.CancellationToken));

        string oversized = new(' ', GameModeStore.MaximumLength + 1);
        await File.WriteAllTextAsync(FilePath, oversized, TestContext.Current.CancellationToken);
        Assert.False(await new GameModeStore(_root).RecordStartAsync(GameMode.High, TestContext.Current.CancellationToken));
        Assert.Equal(oversized, await File.ReadAllTextAsync(FilePath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Serialize_RoundTripsThroughParse()
    {
        GameModeRecord record = new(new GameModeUsage(3, 2, 1), GameMode.Low);

        byte[] bytes = GameModeStore.Serialize(record);

        Assert.Equal("{\"schemaVersion\":1,\"highTimes\":3,\"standardTimes\":2,\"lowTimes\":1,\"lastMode\":\"low\"}", Encoding.UTF8.GetString(bytes));
        Assert.Equal(record, GameModeStore.Parse(bytes, out bool newer));
        Assert.False(newer);
    }
}
