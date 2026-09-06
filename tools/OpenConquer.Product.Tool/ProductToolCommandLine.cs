using System.Diagnostics.CodeAnalysis;

namespace OpenConquer.Product.Tool;

internal static class ProductToolCommandLine
{
    public const string Usage =
        "Usage: stage-managed-product --launcher-publish <path> --client-publish <path> --output <path>";

    public static bool TryParse(
        IReadOnlyList<string> args,
        string workingDirectoryPath,
        [NotNullWhen(true)] out ProductStageOptions? options,
        [NotNullWhen(false)] out string? errorMessage
    )
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectoryPath);

        string? launcherPath = null;
        string? clientPath = null;
        string? outputPath = null;

        bool commandSeen = false;
        bool launcherPathSeen = false;
        bool clientPathSeen = false;
        bool outputPathSeen = false;

        for (int index = 0; index < args.Count; index++)
        {
            string option = args[index];

            switch (option)
            {
                case "stage-managed-product":
                    if (index != 0 || commandSeen)
                    {
                        return Fail(
                            "The command must be the first argument.",
                            out options,
                            out errorMessage
                        );
                    }

                    commandSeen = true;

                    break;

                case "--launcher-publish":
                    if (!commandSeen || launcherPathSeen)
                    {
                        return Fail(
                            "Option '--launcher-publish' may only be specified once after the staging command.",
                            out options,
                            out errorMessage
                        );
                    }

                    launcherPathSeen = true;

                    if (!TryReadValue(args, ref index, option, out launcherPath, out errorMessage))
                    {
                        return Fail(errorMessage, out options, out errorMessage);
                    }

                    break;

                case "--client-publish":
                    if (!commandSeen || clientPathSeen)
                    {
                        return Fail(
                            "Option '--client-publish' may only be specified once after the staging command.",
                            out options,
                            out errorMessage
                        );
                    }

                    clientPathSeen = true;

                    if (!TryReadValue(args, ref index, option, out clientPath, out errorMessage))
                    {
                        return Fail(errorMessage, out options, out errorMessage);
                    }

                    break;

                case "--output":
                    if (!commandSeen || outputPathSeen)
                    {
                        return Fail(
                            "Option '--output' may only be specified once after the staging command.",
                            out options,
                            out errorMessage
                        );
                    }

                    outputPathSeen = true;

                    if (!TryReadValue(args, ref index, option, out outputPath, out errorMessage))
                    {
                        return Fail(errorMessage, out options, out errorMessage);
                    }

                    break;

                default:
                    return Fail($"Unknown argument '{option}'.", out options, out errorMessage);
            }
        }

        if (!commandSeen || launcherPath is null || clientPath is null || outputPath is null)
        {
            return Fail(
                "Launcher publish, client publish, and output paths are required.",
                out options,
                out errorMessage
            );
        }

        if (
            !TryNormalizeAbsolutePath(
                launcherPath,
                workingDirectoryPath,
                out string? normalizedLauncherPath
            )
            || !TryNormalizeAbsolutePath(
                clientPath,
                workingDirectoryPath,
                out string? normalizedClientPath
            )
            || !TryNormalizeAbsolutePath(
                outputPath,
                workingDirectoryPath,
                out string? normalizedOutputPath
            )
        )
        {
            return Fail(
                "All paths must be valid absolute or working-directory-relative paths.",
                out options,
                out errorMessage
            );
        }

        options = new ProductStageOptions(
            normalizedLauncherPath,
            normalizedClientPath,
            normalizedOutputPath
        );

        errorMessage = null;

        return true;
    }

    private static bool TryReadValue(
        IReadOnlyList<string> args,
        ref int index,
        string option,
        [NotNullWhen(true)] out string? value,
        [NotNullWhen(false)] out string? errorMessage
    )
    {
        if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            value = null;
            errorMessage = $"Option '{option}' requires a path value.";

            return false;
        }

        value = args[++index];

        errorMessage = null;

        return true;
    }

    private static bool TryNormalizeAbsolutePath(
        string path,
        string workingDirectoryPath,
        [NotNullWhen(true)] out string? normalizedPath
    )
    {
        normalizedPath = null;

        try
        {
            string normalizedWorkingDirectoryPath = Path.GetFullPath(workingDirectoryPath);

            string candidate = Path.IsPathFullyQualified(path)
                ? path
                : Path.Combine(normalizedWorkingDirectoryPath, path);

            string resolvedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));

            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                return false;
            }

            normalizedPath = resolvedPath;

            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
    }

    private static bool Fail(
        string? message,
        out ProductStageOptions? options,
        [NotNull] out string? errorMessage
    )
    {
        options = null;

        errorMessage = message ?? "Invalid product staging arguments.";

        return false;
    }
}
