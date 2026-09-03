using OpenConquer.Content.Tool.CommandLine;

namespace OpenConquer.Content.Tool.Tests.CommandLine;

public sealed class ContentToolCommandLineTests
{
    private static readonly string s_workingDirectory = Path.Combine(Path.GetTempPath(), "content-tool-working");

    [Fact]
    public void TryParse_ReadsAnImportCommandInEitherOptionOrder()
    {
        Assert.True(ContentToolCommandLine.TryParse(
            ["import-retail-5517", "--destination", "out", "--source", "in"],
            s_workingDirectory,
            out ContentToolCommand? command,
            out string? errorMessage
        ));

        Assert.Null(errorMessage);

        ImportContentSetCommand import = Assert.IsType<ImportContentSetCommand>(command);

        Assert.Equal(Path.Combine(s_workingDirectory, "in"), import.SourceRootPath);
        Assert.Equal(Path.Combine(s_workingDirectory, "out"), import.DestinationRootPath);
    }

    [Fact]
    public void TryParse_ReadsAValidateStartupCommand()
    {
        Assert.True(ContentToolCommandLine.TryParse(
            ["validate-startup", "--content-root", "client"],
            s_workingDirectory,
            out ContentToolCommand? command,
            out _
        ));

        Assert.Equal(
            Path.Combine(s_workingDirectory, "client"),
            Assert.IsType<ValidateStartupCommand>(command).ContentRootPath
        );
    }

    [Fact]
    public void TryParse_ReadsAVerifyContentSetCommand()
    {
        Assert.True(ContentToolCommandLine.TryParse(
            ["verify-content-set", "--content-set", "set"],
            s_workingDirectory,
            out ContentToolCommand? command,
            out _
        ));

        Assert.Equal(
            Path.Combine(s_workingDirectory, "set"),
            Assert.IsType<VerifyContentSetCommand>(command).ContentSetRootPath
        );
    }

    [Theory]
    [InlineData(new string[0], "No command was specified.")]
    [InlineData(new[] { "unknown-verb" }, "Unknown command 'unknown-verb'.")]
    [InlineData(new[] { "verify-content-set", "--content-set" }, "Expected --content-set, each with one value.")]
    [InlineData(new[] { "verify-content-set", "--wrong", "set" }, "Unexpected argument '--wrong'.")]
    [InlineData(new[] { "verify-content-set", "--content-set", "--content-root" }, "Option '--content-set' requires a path value.")]
    [InlineData(new[] { "import-retail-5517", "--source", "in", "--source", "other" }, "Option '--source' was specified more than once.")]
    [InlineData(new[] { "import-retail-5517", "--source", "in" }, "Expected --source and --destination, each with one value.")]
    public void TryParse_RejectsMalformedInvocations(string[] args, string expectedError)
    {
        Assert.False(ContentToolCommandLine.TryParse(args, s_workingDirectory, out ContentToolCommand? command, out string? errorMessage));

        Assert.Null(command);
        Assert.Equal(expectedError, errorMessage);
    }

    [Fact]
    public void UsageLines_DescribeEveryVerb()
    {
        Assert.Contains(ContentToolCommandLine.UsageLines, line => line.Contains("import-retail-5517", StringComparison.Ordinal));
        Assert.Contains(ContentToolCommandLine.UsageLines, line => line.Contains("validate-startup", StringComparison.Ordinal));
        Assert.Contains(ContentToolCommandLine.UsageLines, line => line.Contains("verify-content-set", StringComparison.Ordinal));
    }
}
