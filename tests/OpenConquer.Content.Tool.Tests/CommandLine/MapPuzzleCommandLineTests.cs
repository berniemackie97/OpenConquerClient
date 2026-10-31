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
}
