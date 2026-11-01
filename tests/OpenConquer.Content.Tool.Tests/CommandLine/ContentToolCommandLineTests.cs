using OpenConquer.Content.Tool.CommandLine;

namespace OpenConquer.Content.Tool.Tests.CommandLine;

public sealed class ContentToolCommandLineTests
{
    private static readonly string s_workingDirectory = Path.Combine(
        Path.GetTempPath(),
        "content-tool-working"
    );

    [Fact]
    public void TryParse_ReadsAnImportCommandInEitherOptionOrder()
    {
        Assert.True(
            ContentToolCommandLine.TryParse(
                ["import-client-content", "--destination", "out", "--source", "in"],
                s_workingDirectory,
                out ContentToolCommand? command,
                out string? errorMessage
            )
        );

        Assert.Null(errorMessage);

        ImportContentSetCommand import = Assert.IsType<ImportContentSetCommand>(command);

        Assert.Equal(Path.Combine(s_workingDirectory, "in"), import.SourceRootPath);

        Assert.Equal(Path.Combine(s_workingDirectory, "out"), import.DestinationRootPath);
    }

    [Fact]
    public void TryParse_ImportsSelectedMapInEitherOptionOrder()
    {
        Assert.True(ContentToolCommandLine.TryParse(
            ["import-client-content", "--map-data-id", "1010", "--destination", "out", "--source", "in"],
            s_workingDirectory, out ContentToolCommand? command, out string? errorMessage));

        Assert.Null(errorMessage);
        ImportContentSetCommand import = Assert.IsType<ImportContentSetCommand>(command);
        Assert.Equal(1010u, import.MapDataId);
        Assert.Equal(Path.Combine(s_workingDirectory, "in"), import.SourceRootPath);
        Assert.Equal(Path.Combine(s_workingDirectory, "out"), import.DestinationRootPath);
    }

    [Fact]
    public void TryParse_VerifiesSelectedMap()
    {
        Assert.True(ContentToolCommandLine.TryParse(
            ["verify-content-set", "--content-set", "world", "--map-data-id", "1010"],
            s_workingDirectory, out ContentToolCommand? command, out string? errorMessage));

        Assert.Null(errorMessage);
        VerifyContentSetCommand verify = Assert.IsType<VerifyContentSetCommand>(command);
        Assert.Equal(1010u, verify.MapDataId);
        Assert.Equal(Path.Combine(s_workingDirectory, "world"), verify.ContentSetRootPath);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("invalid")]
    [InlineData("4294967296")]
    public void TryParse_RejectsInvalidSelectedMap(string value)
    {
        Assert.False(ContentToolCommandLine.TryParse(
            ["import-client-content", "--source", "in", "--destination", "out", "--map-data-id", value],
            s_workingDirectory, out ContentToolCommand? command, out _));
        Assert.Null(command);
    }

    [Fact]
    public void TryParse_RejectsRepeatedMapSelection()
    {
        Assert.False(ContentToolCommandLine.TryParse(
            ["verify-content-set", "--content-set", "world", "--map-data-id", "1010",
                "--map-data-id", "1010"],
            s_workingDirectory, out ContentToolCommand? command, out _));
        Assert.Null(command);
    }

    [Fact]
    public void TryParse_ImportsAllIndexedMaps()
    {
        Assert.True(ContentToolCommandLine.TryParse(
            ["import-client-content", "--all-maps", "--destination", "out", "--source", "in"],
            s_workingDirectory, out ContentToolCommand? command, out string? errorMessage));

        Assert.Null(errorMessage);
        ImportContentSetCommand import = Assert.IsType<ImportContentSetCommand>(command);
        Assert.True(import.AllMaps);
        Assert.Null(import.MapDataId);
        Assert.Equal(Path.Combine(s_workingDirectory, "in"), import.SourceRootPath);
        Assert.Equal(Path.Combine(s_workingDirectory, "out"), import.DestinationRootPath);
    }

    [Fact]
    public void TryParse_VerifiesAllIndexedMaps()
    {
        Assert.True(ContentToolCommandLine.TryParse(
            ["verify-content-set", "--content-set", "world", "--all-maps"],
            s_workingDirectory, out ContentToolCommand? command, out string? errorMessage));

        Assert.Null(errorMessage);
        VerifyContentSetCommand verify = Assert.IsType<VerifyContentSetCommand>(command);
        Assert.True(verify.AllMaps);
        Assert.Null(verify.MapDataId);
    }

    [Theory]
    [InlineData(new[] { "import-client-content", "--source", "in", "--destination", "out",
        "--map-data-id", "1010", "--all-maps" })]
    [InlineData(new[] { "verify-content-set", "--content-set", "world", "--all-maps", "--all-maps" })]
    [InlineData(new[] { "import-client-content", "--source", "in", "--all-maps" })]
    public void TryParse_RejectsAmbiguousAllMapsSelection(string[] args)
    {
        Assert.False(ContentToolCommandLine.TryParse(
            args, s_workingDirectory, out ContentToolCommand? command, out string? errorMessage));
        Assert.Null(command);
        Assert.NotNull(errorMessage);
    }

    [Fact]
    public void TryParse_ReadsAValidateStartupCommand()
    {
        Assert.True(
            ContentToolCommandLine.TryParse(
                ["validate-startup", "--content-root", "client"],
                s_workingDirectory,
                out ContentToolCommand? command,
                out _
            )
        );

        Assert.Equal(
            Path.Combine(s_workingDirectory, "client"),
            Assert.IsType<ValidateStartupCommand>(command).ContentRootPath
        );
    }

    [Fact]
    public void TryParse_ReadsAVerifyContentSetCommand()
    {
        Assert.True(
            ContentToolCommandLine.TryParse(
                ["verify-content-set", "--content-set", "set"],
                s_workingDirectory,
                out ContentToolCommand? command,
                out _
            )
        );

        Assert.Equal(
            Path.Combine(s_workingDirectory, "set"),
            Assert.IsType<VerifyContentSetCommand>(command).ContentSetRootPath
        );
    }

    [Fact]
    public void TryParse_ReadsAnInspectServerDatCommand()
    {
        Assert.True(
            ContentToolCommandLine.TryParse(
                ["inspect-server-dat", "--file", "Server.dat"],
                s_workingDirectory,
                out ContentToolCommand? command,
                out _
            )
        );

        Assert.Equal(
            Path.Combine(s_workingDirectory, "Server.dat"),
            Assert.IsType<InspectServerDatCommand>(command).FilePath
        );
    }

    [Fact]
    public void TryParse_ReadsValidateMapSceneryAniCommand()
    {
        Assert.True(ContentToolCommandLine.TryParse(
            ["validate-map-scenery-ani", "--map-data-id", "1010", "--source", "client"],
            s_workingDirectory, out ContentToolCommand? command, out string? errorMessage));

        Assert.Null(errorMessage);
        ValidateMapSceneryAniCommand validate = Assert.IsType<ValidateMapSceneryAniCommand>(command);
        Assert.Equal(1010u, validate.MapDataId);
        Assert.Equal(Path.Combine(s_workingDirectory, "client"), validate.SourceRootPath);
    }

    [Fact]
    public void TryParse_ReadsValidateMapTerrainScenesCommand()
    {
        Assert.True(ContentToolCommandLine.TryParse(
            ["validate-map-terrain-scenes", "--map-data-id", "1010", "--source", "client"],
            s_workingDirectory, out ContentToolCommand? command, out string? errorMessage));

        Assert.Null(errorMessage);
        ValidateMapTerrainScenesCommand validate = Assert.IsType<ValidateMapTerrainScenesCommand>(command);
        Assert.Equal(1010u, validate.MapDataId);
        Assert.Equal(Path.Combine(s_workingDirectory, "client"), validate.SourceRootPath);
    }

    [Theory]
    [InlineData(new string[0], "No command was specified.")]
    [InlineData(new[] { "unknown-verb" }, "Unknown command 'unknown-verb'.")]
    [InlineData(
        new[] { "verify-content-set", "--content-set" },
        "Expected --content-set, each with one value."
    )]
    [InlineData(new[] { "verify-content-set", "--wrong", "set" }, "Unexpected argument '--wrong'.")]
    [InlineData(
        new[] { "verify-content-set", "--content-set", "--content-root" },
        "Option '--content-set' requires a path value."
    )]
    [InlineData(
        new[] { "import-client-content", "--source", "in", "--source", "other" },
        "Option '--source' was specified more than once."
    )]
    [InlineData(
        new[] { "import-client-content", "--source", "in" },
        "Expected --source and --destination, each with one value."
    )]
    [InlineData(new[] { "inspect-server-dat", "--file" }, "Expected --file, each with one value.")]
    [InlineData(
        new[] { "inspect-server-dat", "--wrong", "Server.dat" },
        "Unexpected argument '--wrong'."
    )]
    public void TryParse_RejectsMalformedInvocations(string[] args, string expectedError)
    {
        Assert.False(
            ContentToolCommandLine.TryParse(
                args,
                s_workingDirectory,
                out ContentToolCommand? command,
                out string? errorMessage
            )
        );

        Assert.Null(command);
        Assert.Equal(expectedError, errorMessage);
    }

    [Fact]
    public void UsageLines_DescribeEveryVerb()
    {
        Assert.Contains(ContentToolCommandLine.UsageLines,
            line => line.Contains("validate-map-scenery-ani", StringComparison.Ordinal));
        Assert.Contains(ContentToolCommandLine.UsageLines,
            line => line.Contains("validate-map-terrain-scenes", StringComparison.Ordinal));

        Assert.Contains(
            ContentToolCommandLine.UsageLines,
            line => line.Contains("import-client-content", StringComparison.Ordinal)
        );

        Assert.Contains(
            ContentToolCommandLine.UsageLines,
            line => line.Contains("validate-startup", StringComparison.Ordinal)
        );

        Assert.Contains(
            ContentToolCommandLine.UsageLines,
            line => line.Contains("verify-content-set", StringComparison.Ordinal)
        );

        Assert.Contains(
            ContentToolCommandLine.UsageLines,
            line => line.Contains("inspect-server-dat", StringComparison.Ordinal)
        );
    }
}
