using OpenConquer.Content.Tool.CommandLine;

namespace OpenConquer.Content.Tool.Tests.CommandLine;

public sealed class LoginLoadingCommandLineTests
{
    [Fact]
    public void ParsesLoadingMediaInEitherOptionOrder()
    {
        string root = Path.Combine(Path.GetTempPath(), "loading-command-line");
        Assert.True(ContentToolCommandLine.TryParse(
            ["import-client-content", "--loading-media", "prepared", "--map-data-id", "1010",
             "--destination", "out", "--source", "source"],
            root, out ContentToolCommand? result, out string? error), error);
        ImportContentSetCommand command = Assert.IsType<ImportContentSetCommand>(result);
        Assert.Equal(Path.Combine(root, "prepared"), command.LoadingMediaRootPath);
        Assert.Equal(1010u, command.MapDataId);
    }

    [Fact]
    public void RejectsInvalidLoadingMediaPathAsArgumentError()
    {
        string root = Path.Combine(Path.GetTempPath(), "loading-command-line");
        Assert.False(ContentToolCommandLine.TryParse(
            ["import-client-content", "--source", "source", "--destination", "out",
             "--loading-media", "invalid\0path"],
            root, out ContentToolCommand? result, out string? error));

        Assert.Null(result);
        Assert.Contains("--loading-media", error, StringComparison.Ordinal);
        Assert.Contains("not a valid path", error, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsDuplicateLoadingMediaArguments()
    {
        string root = Path.Combine(Path.GetTempPath(), "loading-command-line");
        Assert.False(ContentToolCommandLine.TryParse(
            ["import-client-content", "--source", "source", "--destination", "out",
             "--loading-media", "one", "--loading-media", "two"],
            root, out _, out _));
    }
}
