using System.Diagnostics.CodeAnalysis;

namespace OpenConquer.Content.Tool.CommandLine;

/// <summary>
/// Parses the content tool's command line into a typed command.
/// </summary>
internal static class ContentToolCommandLine
{
    private const string ImportVerb = "import-retail-5517";
    private const string ValidateStartupVerb = "validate-startup";
    private const string VerifyContentSetVerb = "verify-content-set";
    private const string InspectServerDatVerb = "inspect-server-dat";
    private const string FileOption = "--file";

    private const string SourceOption = "--source";
    private const string DestinationOption = "--destination";
    private const string ContentRootOption = "--content-root";
    private const string ContentSetOption = "--content-set";

    /// <summary>The usage text shown when parsing fails.</summary>
    public static IReadOnlyList<string> UsageLines
    {
        get;
    } =
    [
        "Usage:",
        $"  OpenConquer.Content.Tool {ImportVerb} {SourceOption} <retail-root> {DestinationOption} <content-set-root>",
        $"  OpenConquer.Content.Tool {ValidateStartupVerb} {ContentRootOption} <content-root>",
        $"  OpenConquer.Content.Tool {VerifyContentSetVerb} {ContentSetOption} <content-set-root>",
        $"  OpenConquer.Content.Tool {InspectServerDatVerb} {FileOption} <server-dat>",
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
                    if (!TryParseOptions(remainingArgs, [SourceOption, DestinationOption], workingDirectoryPath, out IReadOnlyList<string>? values, out errorMessage))
                    {
                        return false;
                    }

                    command = new ImportContentSetCommand(values[0], values[1]);
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
                    if (!TryParseOptions(remainingArgs, [ContentSetOption], workingDirectoryPath, out IReadOnlyList<string>? values, out errorMessage))
                    {
                        return false;
                    }

                    command = new VerifyContentSetCommand(values[0]);
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
