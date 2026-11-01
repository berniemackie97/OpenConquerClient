using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace OpenConquer.Content.Tool.CommandLine;

internal static class ContentToolCommandLine
{
    private const string ImportVerb = "import-client-content";
    private const string ValidateStartupVerb = "validate-startup";
    private const string VerifyContentSetVerb = "verify-content-set";
    private const string InspectServerDatVerb = "inspect-server-dat";
    private const string ValidateMapPuzzlesVerb = "validate-map-puzzles";
    private const string ValidateMapSceneryAniVerb = "validate-map-scenery-ani";
    private const string ValidateMapTerrainScenesVerb = "validate-map-terrain-scenes";
    private const string FileOption = "--file";
    private const string MapDataIdOption = "--map-data-id";

    private const string SourceOption = "--source";
    private const string DestinationOption = "--destination";
    private const string ContentRootOption = "--content-root";
    private const string ContentSetOption = "--content-set";

    public static IReadOnlyList<string> UsageLines
    {
        get;
    } =
    [
        "Usage:",
        $"  OpenConquer.Content.Tool {ImportVerb} {SourceOption} <client-root> {DestinationOption} <content-set-root> [{MapDataIdOption} <map-data-id>]",
        $"  OpenConquer.Content.Tool {ValidateStartupVerb} {ContentRootOption} <content-root>",
        $"  OpenConquer.Content.Tool {VerifyContentSetVerb} {ContentSetOption} <content-set-root> [{MapDataIdOption} <map-data-id>]",
        $"  OpenConquer.Content.Tool {InspectServerDatVerb} {FileOption} <server-dat>",
        $"  OpenConquer.Content.Tool {ValidateMapPuzzlesVerb} {SourceOption} <client-root> {MapDataIdOption} <map-data-id>",
        $"  OpenConquer.Content.Tool {ValidateMapSceneryAniVerb} {SourceOption} <client-root> {MapDataIdOption} <map-data-id>",
        $"  OpenConquer.Content.Tool {ValidateMapTerrainScenesVerb} {SourceOption} <client-root> {MapDataIdOption} <map-data-id>",
    ];

    public static bool TryParse(IReadOnlyList<string> args, string workingDirectoryPath, [NotNullWhen(true)] out ContentToolCommand? command, [NotNullWhen(false)] out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectoryPath);

        command = null;

        if (args.Count == 0)
        {
            errorMessage = "No command was specified.";
            return false;
        }

        string verb = args[0];
        IReadOnlyList<string> remainingArgs = [.. args.Skip(1)];

        switch (verb)
        {
            case ImportVerb:
                {
                    if (!TryParseContentSetOptions(remainingArgs, [SourceOption, DestinationOption],
                            workingDirectoryPath, out IReadOnlyList<string>? values, out uint? mapDataId, out errorMessage))
                    {
                        return false;
                    }

                    command = new ImportContentSetCommand(values[0], values[1], mapDataId);
                    return true;
                }

            case ValidateStartupVerb:
                {
                    if (!TryParseOptions(remainingArgs, [ContentRootOption], workingDirectoryPath, out IReadOnlyList<string>? values, out errorMessage))
                    {
                        return false;
                    }

                    command = new ValidateStartupCommand(values[0]);
                    return true;
                }

            case VerifyContentSetVerb:
                {
                    if (!TryParseContentSetOptions(remainingArgs, [ContentSetOption],
                            workingDirectoryPath, out IReadOnlyList<string>? values, out uint? mapDataId, out errorMessage))
                    {
                        return false;
                    }

                    command = new VerifyContentSetCommand(values[0], mapDataId);
                    return true;
                }

            case ValidateMapPuzzlesVerb:
                {
                    if (!TryParseMapSourceOptions(remainingArgs, workingDirectoryPath,
                        out string? sourceRoot, out uint mapDataId, out errorMessage))
                    {
                        return false;
                    }

                    command = new ValidateMapPuzzlesCommand(sourceRoot, mapDataId);
                    return true;
                }

            case ValidateMapSceneryAniVerb:
                {
                    if (!TryParseMapSourceOptions(remainingArgs, workingDirectoryPath,
                        out string? sourceRoot, out uint mapDataId, out errorMessage))
                    {
                        return false;
                    }

                    command = new ValidateMapSceneryAniCommand(sourceRoot, mapDataId);
                    return true;
                }

            case ValidateMapTerrainScenesVerb:
                {
                    if (!TryParseMapSourceOptions(remainingArgs, workingDirectoryPath,
                        out string? sourceRoot, out uint mapDataId, out errorMessage))
                    {
                        return false;
                    }

                    command = new ValidateMapTerrainScenesCommand(sourceRoot, mapDataId);
                    return true;
                }

            case InspectServerDatVerb:
                {
                    if (!TryParseOptions(remainingArgs, [FileOption], workingDirectoryPath, out IReadOnlyList<string>? values, out errorMessage))
                    {
                        return false;
                    }

                    command = new InspectServerDatCommand(values[0]);
                    return true;
                }

            default:
                errorMessage = $"Unknown command '{verb}'.";
                return false;
        }
    }

    private static bool TryParseContentSetOptions(IReadOnlyList<string> args,
        IReadOnlyList<string> requiredOptions, string workingDirectoryPath,
        [NotNullWhen(true)] out IReadOnlyList<string>? values, out uint? mapDataId, out string? errorMessage)
    {
        mapDataId = null;
        if (args.Count == requiredOptions.Count * 2)
        {
            return TryParseOptions(args, requiredOptions, workingDirectoryPath, out values, out errorMessage);
        }

        if (args.Count != (requiredOptions.Count + 1) * 2)
        {
            return TryParseOptions(args, requiredOptions, workingDirectoryPath, out values, out errorMessage);
        }

        List<string> paths = [];
        uint selectedMapDataId = 0;
        bool hasMapDataId = false;
        for (int index = 0; index < args.Count; index += 2)
        {
            if (args[index] == MapDataIdOption)
            {
                if (hasMapDataId || !uint.TryParse(args[index + 1], NumberStyles.None,
                    CultureInfo.InvariantCulture, out selectedMapDataId) || selectedMapDataId == 0)
                {
                    values = null;
                    errorMessage = "Invalid or repeated --map-data-id value.";
                    return false;
                }

                hasMapDataId = true;
            }
            else
            {
                paths.Add(args[index]);
                paths.Add(args[index + 1]);
            }
        }

        if (!hasMapDataId)
        {
            values = null;
            errorMessage = "Expected a single --map-data-id value.";
            return false;
        }

        if (!TryParseOptions(paths, requiredOptions, workingDirectoryPath, out values, out errorMessage))
        {
            return false;
        }

        mapDataId = selectedMapDataId;
        return true;
    }

    private static bool TryParseMapSourceOptions(IReadOnlyList<string> args, string workingDirectoryPath,
        [NotNullWhen(true)] out string? sourceRoot, out uint mapDataId, [NotNullWhen(false)] out string? errorMessage)
    {
        sourceRoot = null;
        mapDataId = 0;
        errorMessage = null;

        if (args.Count != 4)
        {
            errorMessage = "Expected --source and --map-data-id, each with one value.";
            return false;
        }

        bool hasMapDataId = false;
        for (int i = 0; i < args.Count; i += 2)
        {
            if (args[i] == SourceOption && sourceRoot is null &&
                !args[i + 1].StartsWith("--", StringComparison.Ordinal) &&
                TryResolvePath(args[i + 1], workingDirectoryPath, out string? resolved))
            {
                sourceRoot = resolved;
            }
            else if (args[i] == MapDataIdOption && !hasMapDataId &&
                uint.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out uint parsed) && parsed != 0)
            {
                mapDataId = parsed;
                hasMapDataId = true;
            }
            else
            {
                errorMessage = $"Invalid, repeated or missing value for '{args[i]}'.";
                return false;
            }
        }

        if (sourceRoot is null || !hasMapDataId)
        {
            errorMessage = "Both --source and --map-data-id are required.";
            return false;
        }

        return true;
    }

    private static bool TryParseOptions(IReadOnlyList<string> args, IReadOnlyList<string> expectedOptions, string workingDirectoryPath, [NotNullWhen(true)] out IReadOnlyList<string>? values, [NotNullWhen(false)] out string? errorMessage)
    {
        values = null;
        string?[] parsedValues = new string?[expectedOptions.Count];

        if (args.Count != expectedOptions.Count * 2)
        {
            errorMessage = $"Expected {string.Join(" and ", expectedOptions)}, each with one value.";
            return false;
        }

        for (int index = 0; index < args.Count; index += 2)
        {
            int optionIndex = IndexOfOption(expectedOptions, args[index]);

            if (optionIndex < 0)
            {
                errorMessage = $"Unexpected argument '{args[index]}'.";
                return false;
            }

            if (parsedValues[optionIndex] is not null)
            {
                errorMessage = $"Option '{expectedOptions[optionIndex]}' was specified more than once.";
                return false;
            }

            string value = args[index + 1];

            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal))
            {
                errorMessage = $"Option '{expectedOptions[optionIndex]}' requires a path value.";
                return false;
            }

            if (!TryResolvePath(value, workingDirectoryPath, out string? resolvedPath))
            {
                errorMessage = $"Option '{expectedOptions[optionIndex]}' value '{value}' is not a valid path.";
                return false;
            }

            parsedValues[optionIndex] = resolvedPath;
        }

        values = [.. parsedValues.Select(static value => value!)];
        errorMessage = null;

        return true;
    }

    private static int IndexOfOption(IReadOnlyList<string> expectedOptions, string candidate)
    {
        for (int index = 0; index < expectedOptions.Count; index++)
        {
            if (string.Equals(expectedOptions[index], candidate, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryResolvePath(string value, string workingDirectoryPath, [NotNullWhen(true)] out string? resolvedPath)
    {
        try
        {
            resolvedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value, workingDirectoryPath));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            resolvedPath = null;
            return false;
        }
    }
}
