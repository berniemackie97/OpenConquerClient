using OpenConquer.Content.Tool.CommandLine;

namespace OpenConquer.Content.Tool.Tests.CommandLine;

public sealed class MapPuzzleCommandLineTests
{
    [Theory]
    [InlineData("1010", 1010u)]
    [InlineData("4294967295", uint.MaxValue)]
    public void AcceptsValidMapDataIds(string value, uint expected)
    {
        bool accepted = ContentToolCommandLine.TryParse(
            ["validate-map-puzzles", "--source", "retail", "--map-data-id", value],
            Path.GetTempPath(), out ContentToolCommand? command, out _);

        Assert.True(accepted);
        ValidateMapPuzzlesCommand parsed = Assert.IsType<ValidateMapPuzzlesCommand>(command);
        Assert.Equal(expected, parsed.MapDataId);
    }

    [Fact]
    public void AcceptsEitherOptionOrder()
    {
        bool accepted = ContentToolCommandLine.TryParse(
            ["validate-map-puzzles", "--map-data-id", "1010", "--source", "retail"],
            Path.GetTempPath(), out ContentToolCommand? command, out _);

        Assert.True(accepted);
        Assert.Equal(1010u, Assert.IsType<ValidateMapPuzzlesCommand>(command).MapDataId);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("4294967296")]
    [InlineData("not-a-number")]
    public void RejectsInvalidMapDataIds(string value)
    {
        Assert.False(ContentToolCommandLine.TryParse(
            ["validate-map-puzzles", "--source", "retail", "--map-data-id", value],
            Path.GetTempPath(), out ContentToolCommand? command, out string? error));

        Assert.Null(command);
        Assert.NotNull(error);
    }

    [Fact]
    public void RejectsOptionTokenAsSourcePath()
    {
        Assert.False(ContentToolCommandLine.TryParse(
            ["validate-map-puzzles", "--source", "--unexpected", "--map-data-id", "1010"],
            Path.GetTempPath(), out ContentToolCommand? command, out string? error));

        Assert.Null(command);
        Assert.NotNull(error);
    }
}
