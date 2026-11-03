using OpenConquer.Content.Tool.CommandLine;

namespace OpenConquer.Content.Tool.Tests.CommandLine;

public sealed class ContentToolCommandLineTests
{
    private static readonly string s_workingDirectory = Path.Combine(
        Path.GetTempPath(),
        "content-tool-working"
    );

    [Fact]
    public void TryParse_ReadsMapAssetCompilation()
    {
        Assert.True(ContentToolCommandLine.TryParse(
            ["compile-map-asset", "--destination", "1010.ocmap", "--map-data-id", "1010", "--source", "assets"],
            s_workingDirectory, out ContentToolCommand? command, out string? errorMessage));

        Assert.Null(errorMessage);
        CompileMapAssetCommand compile = Assert.IsType<CompileMapAssetCommand>(command);
        Assert.Equal(1010u, compile.MapDataId);
        Assert.Equal(Path.Combine(s_workingDirectory, "assets"), compile.SourceRootPath);
        Assert.Equal(Path.Combine(s_workingDirectory, "1010.ocmap"), compile.DestinationPath);
    }

    [Fact]
    public void TryParse_RejectsAmbiguousMapAssetCompilation()
    {
        string[][] invalid =
        [
            ["compile-map-asset", "--source", "assets", "--destination", "map.ocmap"],
            ["compile-map-asset", "--source", "assets", "--destination", "map.ocmap", "--all-maps"],
            ["compile-map-asset", "--source", "assets", "--destination", "map.ocmap", "--map-data-id", "0"],
        ];

        foreach (string[] args in invalid)
        {
            Assert.False(ContentToolCommandLine.TryParse(args, s_workingDirectory,
                out ContentToolCommand? command, out _));
            Assert.Null(command);
        }
    }

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

    [Fact]
    public void TryParse_ImportsAvailableMaps()
    {
        Assert.True(ContentToolCommandLine.TryParse(
            ["import-client-content", "--destination", "out", "--available-maps", "--source", "in"],
            s_workingDirectory, out ContentToolCommand? command, out string? errorMessage));

        Assert.Null(errorMessage);
        ImportContentSetCommand import = Assert.IsType<ImportContentSetCommand>(command);
        Assert.True(import.AvailableMaps);
        Assert.False(import.AllMaps);
        Assert.Null(import.MapDataId);
    }

    [Fact]
    public void TryParse_VerifiesAvailableMaps()
    {
        Assert.True(ContentToolCommandLine.TryParse(
            ["verify-content-set", "--available-maps", "--content-set", "world"],
            s_workingDirectory, out ContentToolCommand? command, out string? errorMessage));

        Assert.Null(errorMessage);
        VerifyContentSetCommand verify = Assert.IsType<VerifyContentSetCommand>(command);
        Assert.True(verify.AvailableMaps);
        Assert.False(verify.AllMaps);
    }

    [Fact]
    public void TryParse_RejectsConflictingAvailableMapSelections()
    {
        string[][] invalid =
        [
            ["import-client-content", "--source", "in", "--destination", "out",
                "--available-maps", "--all-maps"],
            ["import-client-content", "--source", "in", "--destination", "out",
                "--map-data-id", "1010", "--available-maps"],
            ["verify-content-set", "--content-set", "world", "--available-maps", "--available-maps"],
        ];

        foreach (string[] args in invalid)
        {
            Assert.False(ContentToolCommandLine.TryParse(
                args, s_workingDirectory, out ContentToolCommand? command, out _));
            Assert.Null(command);
        }
    }

    [Fact]
    public void TryParse_RejectsAmbiguousAllMapsSelection()
    {
        string[][] invalidInvocations =
        [
            ["import-client-content", "--source", "in", "--destination", "out",
                "--map-data-id", "1010", "--all-maps"],
            ["verify-content-set", "--content-set", "world", "--all-maps", "--all-maps"],
            ["import-client-content", "--source", "in", "--all-maps"],
        ];

        foreach (string[] args in invalidInvocations)
        {
            Assert.False(ContentToolCommandLine.TryParse(
                args, s_workingDirectory, out ContentToolCommand? command, out string? errorMessage));
            Assert.Null(command);
            Assert.NotNull(errorMessage);
        }
    }

    [Fact]
    public void TryParse_ReadsContentAnalysisWithMapSelection()
    {
        Assert.True(ContentToolCommandLine.TryParse(
            ["analyze-content-set", "--available-maps", "--content-set", "content"],
            s_workingDirectory, out ContentToolCommand? command, out string? errorMessage));

        Assert.Null(errorMessage);
        AnalyzeContentSetCommand analyze = Assert.IsType<AnalyzeContentSetCommand>(command);
        Assert.Equal(Path.Combine(s_workingDirectory, "content"), analyze.ContentSetRootPath);
        Assert.True(analyze.AvailableMaps);
        Assert.False(analyze.AllMaps);
        Assert.Null(analyze.MapDataId);
    }

    [Fact]
    public void TryParse_ReadsContentReadProfileWithMapSelection()
    {
        Assert.True(ContentToolCommandLine.TryParse(
            ["profile-content-reads", "--content-set", "content", "--map-data-id", "1010"],
            s_workingDirectory, out ContentToolCommand? command, out string? errorMessage));

        Assert.Null(errorMessage);
        ProfileContentReadsCommand profile = Assert.IsType<ProfileContentReadsCommand>(command);
        Assert.Equal(Path.Combine(s_workingDirectory, "content"), profile.ContentSetRootPath);
        Assert.Equal(1010u, profile.MapDataId);
        Assert.False(profile.AllMaps);
        Assert.False(profile.AvailableMaps);
    }

    [Theory]
    [InlineData("--all-maps", false)]
    [InlineData("--available-maps", true)]
    public void TryParse_ReadsMapDependencyAnalysis(string selection, bool availableMaps)
    {
        Assert.True(ContentToolCommandLine.TryParse(
            ["analyze-map-dependencies", "--content-set", "content", selection],
            s_workingDirectory, out ContentToolCommand? command, out string? errorMessage));

        Assert.Null(errorMessage);
        AnalyzeMapDependenciesCommand analyze = Assert.IsType<AnalyzeMapDependenciesCommand>(command);
        Assert.Equal(Path.Combine(s_workingDirectory, "content"), analyze.ContentSetRootPath);
        Assert.Equal(availableMaps, analyze.AvailableMaps);
    }

    [Fact]
    public void TryParse_RejectsMapDependencyAnalysisWithoutCatalogCoverage()
    {
        string[][] invalid =
        [
            ["analyze-map-dependencies", "--content-set", "content"],
            ["analyze-map-dependencies", "--content-set", "content", "--map-data-id", "1010"],
            ["analyze-map-dependencies", "--content-set", "content", "--all-maps", "--available-maps"],
        ];

        foreach (string[] args in invalid)
        {
            Assert.False(ContentToolCommandLine.TryParse(args, s_workingDirectory,
                out ContentToolCommand? command, out _));
            Assert.Null(command);
        }
    }

    [Theory]
    [InlineData("--all-maps", true, false)]
    [InlineData("--available-maps", false, true)]
    public void TryParse_ReadsPackagingEvaluation(string selection, bool allMaps, bool availableMaps)
    {
        Assert.True(ContentToolCommandLine.TryParse(
            ["evaluate-content-packaging", "--scratch", "work", selection, "--content-set", "content",
                "--previous", "earlier"],
            s_workingDirectory, out ContentToolCommand? command, out string? errorMessage));

        Assert.Null(errorMessage);
        EvaluateContentPackagingCommand evaluation = Assert.IsType<EvaluateContentPackagingCommand>(command);
        Assert.Equal(Path.Combine(s_workingDirectory, "content"), evaluation.ContentSetRootPath);
        Assert.Equal(Path.Combine(s_workingDirectory, "work"), evaluation.ScratchRootPath);
        Assert.Equal(Path.Combine(s_workingDirectory, "earlier"), evaluation.PreviousRootPath);
        Assert.Equal(allMaps, evaluation.AllMaps);
        Assert.Equal(availableMaps, evaluation.AvailableMaps);
    }

    [Fact]
    public void TryParse_ReadsPackagingEvaluationForOneMap()
    {
        Assert.True(ContentToolCommandLine.TryParse(
            ["evaluate-content-packaging", "--content-set", "content", "--scratch", "work",
                "--map-data-id", "1010"],
            s_workingDirectory, out ContentToolCommand? command, out string? errorMessage));

        Assert.Null(errorMessage);
        EvaluateContentPackagingCommand evaluation = Assert.IsType<EvaluateContentPackagingCommand>(command);
        Assert.Equal(1010u, evaluation.MapDataId);
        Assert.Null(evaluation.PreviousRootPath);
    }

    [Fact]
    public void TryParse_RejectsInvalidPackagingEvaluationOptions()
    {
        string[][] invalid =
        [
            ["evaluate-content-packaging", "--content-set", "content"],
            ["evaluate-content-packaging", "--content-set", "content", "--scratch", "work", "--previous"],
            ["evaluate-content-packaging", "--content-set", "content", "--scratch", "work",
                "--previous", "first", "--previous", "second"],
            ["evaluate-content-packaging", "--content-set", "content", "--scratch", "work",
                "--all-maps", "--available-maps"],
        ];

        foreach (string[] args in invalid)
        {
            Assert.False(ContentToolCommandLine.TryParse(args, s_workingDirectory,
                out ContentToolCommand? command, out _));
            Assert.Null(command);
        }
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

    [Theory]
    [InlineData("--all-maps", true, false)]
    [InlineData("--available-maps", false, true)]
    public void TryParse_ReadsContentSetComparisonWithMapCoverage(string option, bool allMaps, bool availableMaps)
    {
        Assert.True(ContentToolCommandLine.TryParse(
            ["compare-content-sets", "--candidate", "candidate", option, "--source", "previous"],
            s_workingDirectory, out ContentToolCommand? command, out string? errorMessage));

        Assert.Null(errorMessage);
        CompareContentSetsCommand compare = Assert.IsType<CompareContentSetsCommand>(command);
        Assert.Equal(Path.Combine(s_workingDirectory, "previous"), compare.PreviousRootPath);
        Assert.Equal(Path.Combine(s_workingDirectory, "candidate"), compare.CandidateRootPath);
        Assert.Null(compare.MapDataId);
        Assert.Equal(allMaps, compare.AllMaps);
        Assert.Equal(availableMaps, compare.AvailableMaps);
    }

    [Fact]
    public void TryParse_ReadsContentSetComparisonWithSingleMap()
    {
        Assert.True(ContentToolCommandLine.TryParse(
            ["compare-content-sets", "--source", "previous", "--map-data-id", "1010", "--candidate", "candidate"],
            s_workingDirectory, out ContentToolCommand? command, out string? errorMessage));

        Assert.Null(errorMessage);
        Assert.Equal(1010u, Assert.IsType<CompareContentSetsCommand>(command).MapDataId);
    }

    [Fact]
    public void TryParse_RejectsAmbiguousContentSetComparisonOptions()
    {
        string[][] invalid =
        [
            ["compare-content-sets", "--source", "previous"],
            ["compare-content-sets", "--source", "previous", "--candidate", "candidate", "--all-maps", "--available-maps"],
            ["compare-content-sets", "--source", "previous", "--candidate", "candidate", "--map-data-id", "0"],
        ];

        foreach (string[] args in invalid)
        {
            Assert.False(ContentToolCommandLine.TryParse(args, s_workingDirectory,
                out ContentToolCommand? command, out _));
            Assert.Null(command);
        }
    }

    [Fact]
    public void TryParse_ReadsMapComparisonCommandInAnyOptionOrder()
    {
        Assert.True(ContentToolCommandLine.TryParse(
            ["compare-map-semantics", "--map-data-id", "1010", "--candidate", "candidate", "--source", "source"],
            s_workingDirectory, out ContentToolCommand? command, out string? errorMessage));

        Assert.Null(errorMessage);
        CompareMapSemanticsCommand compare = Assert.IsType<CompareMapSemanticsCommand>(command);
        Assert.Equal(1010u, compare.MapDataId);
        Assert.Equal(Path.Combine(s_workingDirectory, "source"), compare.SourceRootPath);
        Assert.Equal(Path.Combine(s_workingDirectory, "candidate"), compare.CandidateRootPath);
    }

    [Fact]
    public void TryParse_RejectsMapComparisonWithoutSingleMapId()
    {
        string[][] invalid =
        [
            ["compare-map-semantics", "--source", "source", "--candidate", "candidate"],
            ["compare-map-semantics", "--source", "source", "--candidate", "candidate", "--map-data-id", "0"],
            ["compare-map-semantics", "--source", "source", "--candidate", "candidate", "--all-maps"],
        ];

        foreach (string[] args in invalid)
        {
            Assert.False(ContentToolCommandLine.TryParse(args, s_workingDirectory,
                out ContentToolCommand? command, out _));
            Assert.Null(command);
        }
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
    [InlineData(new string[] { "unknown-verb" }, "Unknown command 'unknown-verb'.")]
    [InlineData(
        new[] { "verify-content-set", "--content-set" },
        "Expected --content-set, each with one value."
    )]
    [InlineData(new string[] { "verify-content-set", "--wrong", "set" }, "Unexpected argument '--wrong'.")]
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
    [InlineData(new string[] { "inspect-server-dat", "--file" }, "Expected --file, each with one value.")]
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
            line => line.Contains("compare-map-semantics", StringComparison.Ordinal));
        Assert.Contains(ContentToolCommandLine.UsageLines,
            line => line.Contains("compare-content-sets", StringComparison.Ordinal));
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
        Assert.Contains(ContentToolCommandLine.UsageLines,
            line => line.Contains("analyze-content-set", StringComparison.Ordinal));
        Assert.Contains(ContentToolCommandLine.UsageLines,
            line => line.Contains("analyze-map-dependencies", StringComparison.Ordinal));
        Assert.Contains(ContentToolCommandLine.UsageLines,
            line => line.Contains("evaluate-content-packaging", StringComparison.Ordinal));
        Assert.Contains(ContentToolCommandLine.UsageLines,
            line => line.Contains("profile-content-reads", StringComparison.Ordinal));

        Assert.Contains(
            ContentToolCommandLine.UsageLines,
            line => line.Contains("inspect-server-dat", StringComparison.Ordinal)
        );
    }
}
