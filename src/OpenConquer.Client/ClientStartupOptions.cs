using System.Diagnostics.CodeAnalysis;
using OpenConquer.Rendering;

namespace OpenConquer.Client;

internal sealed class ClientStartupOptions
{
    private const string ContentRootOptionName = "--content-root";
    private const string PresentationOptionName = "--presentation";

    /// <summary>
    /// Accepted <see cref="PresentationOptionName"/> values, in the order they are listed to the
    /// user when the supplied value is not one of them.
    /// </summary>
    private static readonly (string Name, PresentationPolicy Policy)[] s_presentationPolicies =
    [
        ("fit", PresentationPolicy.Fit),
        ("integer", PresentationPolicy.IntegerScale),
        ("stretch", PresentationPolicy.Stretch),
    ];

    private ClientStartupOptions(string contentRootPath, PresentationPolicy presentationPolicy)
    {
        ContentRootPath = contentRootPath;
        PresentationPolicy = presentationPolicy;
    }

    public string ContentRootPath
    {
        get;
    }

    /// <summary>
    /// How the fixed logical frame is fitted into the resizable host window.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="PresentationPolicy.Fit"/>, which fills as much of the window as a
    /// distortion-free scale allows.
    /// </remarks>
    public PresentationPolicy PresentationPolicy
    {
        get;
    }

    /// <summary>The <c>--presentation</c> values accepted on the command line.</summary>
    public static string PresentationPolicyNames => string.Join('|', s_presentationPolicies.Select(entry => entry.Name));

    public static bool TryParse(string[] args, [NotNullWhen(true)] out ClientStartupOptions? options, [NotNullWhen(false)] out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(args);

        string packagedContentRoot = Path.Combine(
            AppContext.BaseDirectory,
            "content",
            "retail-5517",
            "payload"
        );

        return TryParse(args, packagedContentRoot, Environment.CurrentDirectory, out options, out errorMessage);
    }

    internal static bool TryParse(IReadOnlyList<string> args, string defaultContentRootPath, string workingDirectoryPath, [NotNullWhen(true)] out ClientStartupOptions? options, [NotNullWhen(false)] out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(args);

        string normalizedDefaultContentRootPath = NormalizeRequiredAbsolutePath(defaultContentRootPath, nameof(defaultContentRootPath));

        string normalizedWorkingDirectoryPath = NormalizeRequiredAbsolutePath(workingDirectoryPath, nameof(workingDirectoryPath));

        string contentRootPath = normalizedDefaultContentRootPath;
        bool contentRootSpecified = false;

        PresentationPolicy presentationPolicy = PresentationPolicy.Fit;
        bool presentationSpecified = false;

        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];

            switch (argument)
            {
                case ContentRootOptionName:
                    {
                        if (contentRootSpecified)
                        {
                            return Fail($"Startup option '{ContentRootOptionName}' may only be specified once.", out options, out errorMessage);
                        }

                        if (!TryReadOptionValue(args, ref index, ContentRootOptionName, "a path value", out string? configuredPath, out errorMessage))
                        {
                            return Fail(errorMessage, out options, out errorMessage);
                        }

                        if (!TryResolveConfiguredPath(configuredPath, normalizedWorkingDirectoryPath, out string? resolvedContentRootPath))
                        {
                            return Fail($"Startup option '{ContentRootOptionName}' contains an invalid path.", out options, out errorMessage);
                        }

                        contentRootPath = resolvedContentRootPath;
                        contentRootSpecified = true;

                        break;
                    }

                case PresentationOptionName:
                    {
                        if (presentationSpecified)
                        {
                            return Fail($"Startup option '{PresentationOptionName}' may only be specified once.", out options, out errorMessage);
                        }

                        if (!TryReadOptionValue(args, ref index, PresentationOptionName, $"one of {PresentationPolicyNames}", out string? configuredPolicy, out errorMessage))
                        {
                            return Fail(errorMessage, out options, out errorMessage);
                        }

                        if (!TryParsePresentationPolicy(configuredPolicy, out presentationPolicy))
                        {
                            return Fail($"Startup option '{PresentationOptionName}' value '{configuredPolicy}' is not recognized. Expected one of {PresentationPolicyNames}.", out options, out errorMessage);
                        }

                        presentationSpecified = true;

                        break;
                    }

                default:
                    return Fail($"Unknown startup argument '{argument}'.", out options, out errorMessage);
            }
        }

        options = new ClientStartupOptions(contentRootPath, presentationPolicy);
        errorMessage = null;

        return true;
    }

    /// <summary>
    /// Consumes the value following an option, advancing <paramref name="index"/> past it.
    /// </summary>
    /// <remarks>
    /// A value beginning with <c>--</c> is refused so a forgotten value cannot silently swallow the
    /// next option and leave it unapplied.
    /// </remarks>
    private static bool TryReadOptionValue(IReadOnlyList<string> args, ref int index, string optionName, string expectation, [NotNullWhen(true)] out string? value, out string? errorMessage)
    {
        if (index + 1 >= args.Count)
        {
            value = null;
            errorMessage = $"Startup option '{optionName}' requires {expectation}.";

            return false;
        }

        string candidate = args[index + 1];

        if (string.IsNullOrWhiteSpace(candidate) || candidate.StartsWith("--", StringComparison.Ordinal))
        {
            value = null;
            errorMessage = $"Startup option '{optionName}' requires {expectation}.";

            return false;
        }

        index++;

        value = candidate;
        errorMessage = null;

        return true;
    }

    private static bool TryParsePresentationPolicy(string value, out PresentationPolicy policy)
    {
        foreach ((string name, PresentationPolicy candidate) in s_presentationPolicies)
        {
            if (string.Equals(value, name, StringComparison.OrdinalIgnoreCase))
            {
                policy = candidate;
                return true;
            }
        }

        policy = PresentationPolicy.Fit;
        return false;
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

    private static bool Fail(string? errorMessage, out ClientStartupOptions? options, out string? parsedErrorMessage)
    {
        options = null;
        parsedErrorMessage = errorMessage;

        return false;
    }
}
