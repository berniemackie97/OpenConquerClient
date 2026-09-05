using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using OpenConquer.Platform;
using OpenConquer.Rendering;

namespace OpenConquer.Client;

internal sealed class ClientStartupOptions
{
    private const string ContentRootOptionName = "--content-root";
    private const string PresentationOptionName = "--presentation";
    private const string WindowModeOptionName = "--window-mode";
    private const string WindowSizeOptionName = "--window-size";

    private const int MaximumWindowDimension = 16_384;
    private const long MaximumWindowArea = 7_680L * 4_320L;

    private static readonly (string Name, PresentationPolicy Policy)[] s_presentationPolicies =
    [
        ("fit", PresentationPolicy.Fit),
        ("integer", PresentationPolicy.IntegerScale),
        ("stretch", PresentationPolicy.Stretch),
    ];

    private static readonly (string Name, DesktopWindowMode Mode)[] s_windowModes =
    [
        ("resizable", DesktopWindowMode.Resizable),
        ("fixed", DesktopWindowMode.Fixed),
        ("fullscreen", DesktopWindowMode.Fullscreen),
    ];

    private ClientStartupOptions(
        string contentRootPath,
        PresentationPolicy presentationPolicy,
        DesktopWindowMode windowMode,
        PixelSize windowSize)
    {
        ContentRootPath = contentRootPath;
        PresentationPolicy = presentationPolicy;
        WindowMode = windowMode;
        WindowSize = windowSize;
    }

    public string ContentRootPath
    {
        get;
    }

    /// <summary>
    /// How the fixed logical frame is presented by the native game window.
    /// </summary>
    public PresentationPolicy PresentationPolicy
    {
        get;
    }

    /// <summary>
    /// How the native game window is presented independently of the logical render size.
    /// </summary>
    public DesktopWindowMode WindowMode
    {
        get;
    }

    /// <summary>
    /// The requested physical desktop size. The active display may determine the final fullscreen framebuffer.
    /// </summary>
    public PixelSize WindowSize
    {
        get;
    }

    public static string PresentationPolicyNames => string.Join('|', s_presentationPolicies.Select(entry => entry.Name));

    public static string WindowModeNames => string.Join('|', s_windowModes.Select(entry => entry.Name));

    public static bool TryParse(string[] args, [NotNullWhen(true)] out ClientStartupOptions? options, [NotNullWhen(false)] out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(args);

        string packagedContentRoot = Path.Combine(AppContext.BaseDirectory, "content", "retail-5517", "payload");

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

        DesktopWindowMode windowMode = DesktopWindowMode.Resizable;
        bool windowModeSpecified = false;

        PixelSize windowSize = DesktopWindow.DefaultWindowSize;
        bool windowSizeSpecified = false;

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

                case WindowModeOptionName:
                    {
                        if (windowModeSpecified)
                        {
                            return Fail($"Startup option '{WindowModeOptionName}' may only be specified once.", out options, out errorMessage);
                        }

                        if (!TryReadOptionValue(args, ref index, WindowModeOptionName, $"one of {WindowModeNames}", out string? configuredMode, out errorMessage))
                        {
                            return Fail(errorMessage, out options, out errorMessage);
                        }

                        if (!TryParseWindowMode(configuredMode, out windowMode))
                        {
                            return Fail($"Startup option '{WindowModeOptionName}' value '{configuredMode}' is not recognized. Expected one of {WindowModeNames}.", out options, out errorMessage);
                        }

                        windowModeSpecified = true;

                        break;
                    }

                case WindowSizeOptionName:
                    {
                        if (windowSizeSpecified)
                        {
                            return Fail($"Startup option '{WindowSizeOptionName}' may only be specified once.", out options, out errorMessage);
                        }

                        if (!TryReadOptionValue(args, ref index, WindowSizeOptionName, "a WIDTHxHEIGHT value", out string? configuredSize, out errorMessage))
                        {
                            return Fail(errorMessage, out options, out errorMessage);
                        }

                        if (!TryParseWindowSize(configuredSize, out windowSize))
                        {
                            return Fail($"Startup option '{WindowSizeOptionName}' value '{configuredSize}' is not a supported WIDTHxHEIGHT value.", out options, out errorMessage);
                        }

                        windowSizeSpecified = true;

                        break;
                    }

                default:
                    return Fail($"Unknown startup argument '{argument}'.", out options, out errorMessage);
            }
        }

        options = new ClientStartupOptions(contentRootPath, presentationPolicy, windowMode, windowSize);
        errorMessage = null;

        return true;
    }

    /// <summary>
    /// Consumes the value following an option, advancing <paramref name="index"/> past it.
    /// </summary>
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

    private static bool TryParseWindowMode(string value, out DesktopWindowMode mode)
    {
        foreach ((string name, DesktopWindowMode candidate) in s_windowModes)
        {
            if (string.Equals(value, name, StringComparison.OrdinalIgnoreCase))
            {
                mode = candidate;
                return true;
            }
        }

        mode = DesktopWindowMode.Resizable;
        return false;
    }

    private static bool TryParseWindowSize(string value, out PixelSize size)
    {
        int separatorIndex = value.IndexOf('x');

        if (separatorIndex < 0)
        {
            separatorIndex = value.IndexOf('X');
        }

        if (separatorIndex <= 0 || separatorIndex == value.Length - 1 || value.IndexOf('x', separatorIndex + 1) >= 0 || value.IndexOf('X', separatorIndex + 1) >= 0)
        {
            size = default;
            return false;
        }

        ReadOnlySpan<char> widthText = value.AsSpan(0, separatorIndex);
        ReadOnlySpan<char> heightText = value.AsSpan(separatorIndex + 1);

        if (!int.TryParse(widthText, NumberStyles.None, CultureInfo.InvariantCulture, out int width) ||
            !int.TryParse(heightText, NumberStyles.None, CultureInfo.InvariantCulture, out int height) ||
            width <= 0 ||
            height <= 0 ||
            width > MaximumWindowDimension ||
            height > MaximumWindowDimension ||
            (long)width * height > MaximumWindowArea)
        {
            size = default;
            return false;
        }

        size = new PixelSize(width, height);
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

    private static bool Fail(string? errorMessage, out ClientStartupOptions? options, out string? parsedErrorMessage)
    {
        options = null;
        parsedErrorMessage = errorMessage;

        return false;
    }
}
