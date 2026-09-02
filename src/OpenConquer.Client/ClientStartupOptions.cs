using System.Diagnostics.CodeAnalysis;

namespace OpenConquer.Client;

internal sealed class ClientStartupOptions
{
    private const string ContentRootOptionName = "--content-root";

    private ClientStartupOptions(string contentRootPath)
    {
        ContentRootPath = contentRootPath;
    }

    public string ContentRootPath
    {
        get;
    }

    public static bool TryParse(string[] args, [NotNullWhen(true)] out ClientStartupOptions? options, [NotNullWhen(false)] out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(args);

        return TryParse(args, AppContext.BaseDirectory, Environment.CurrentDirectory, out options, out errorMessage);
    }

    internal static bool TryParse(IReadOnlyList<string> args, string defaultContentRootPath, string workingDirectoryPath, [NotNullWhen(true)] out ClientStartupOptions? options, [NotNullWhen(false)] out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(args);

        string normalizedDefaultContentRootPath = NormalizeRequiredAbsolutePath(defaultContentRootPath, nameof(defaultContentRootPath));

        string normalizedWorkingDirectoryPath = NormalizeRequiredAbsolutePath(workingDirectoryPath, nameof(workingDirectoryPath));

        string contentRootPath = normalizedDefaultContentRootPath;
        bool contentRootSpecified = false;

        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];

            if (!string.Equals(argument, ContentRootOptionName, StringComparison.Ordinal))
            {
                return Fail($"Unknown startup argument '{argument}'.", out options, out errorMessage);
            }

            if (contentRootSpecified)
            {
                return Fail($"Startup option '{ContentRootOptionName}' may only be specified once.", out options, out errorMessage);
            }

            if (index + 1 >= args.Count)
            {
                return Fail($"Startup option '{ContentRootOptionName}' requires a path value.", out options, out errorMessage);
            }

            string configuredPath = args[++index];

            if (string.IsNullOrWhiteSpace(configuredPath) || configuredPath.StartsWith("--", StringComparison.Ordinal))
            {
                return Fail($"Startup option '{ContentRootOptionName}' requires a path value.", out options, out errorMessage);
            }

            if (!TryResolveConfiguredPath(configuredPath, normalizedWorkingDirectoryPath, out string? resolvedContentRootPath))
            {
                return Fail($"Startup option '{ContentRootOptionName}' contains an invalid path.", out options, out errorMessage);
            }

            contentRootPath = resolvedContentRootPath;
            contentRootSpecified = true;
        }

        options = new ClientStartupOptions(contentRootPath);
        errorMessage = null;

        return true;
    }

    private static string NormalizeRequiredAbsolutePath(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must not be null, empty, or whitespace.", parameterName);
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("Path must be fully qualified.", parameterName);
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static bool TryResolveConfiguredPath(string configuredPath, string workingDirectoryPath, [NotNullWhen(true)] out string? resolvedPath)
    {
        try
        {
            resolvedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredPath, workingDirectoryPath));

            return true;
        }
        catch (ArgumentException)
        {
            resolvedPath = null;
            return false;
        }
        catch (NotSupportedException)
        {
            resolvedPath = null;
            return false;
        }
        catch (PathTooLongException)
        {
            resolvedPath = null;
            return false;
        }
    }

    private static bool Fail(string errorMessage, out ClientStartupOptions? options, out string? parsedErrorMessage)
    {
        options = null;
        parsedErrorMessage = errorMessage;

        return false;
    }
}
