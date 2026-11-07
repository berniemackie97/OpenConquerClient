using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using OpenConquer.Platform.Geometry;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Startup;

internal enum ClientStartupArgumentCode
{
    None = 0,
    UnknownArgument = 1,
    DuplicateContentRoot = 2,
    MissingContentRoot = 3,
    InvalidContentRoot = 4,
    DuplicatePresentation = 5,
    MissingPresentation = 6,
    InvalidPresentation = 7,
    DuplicateWindowMode = 8,
    MissingWindowMode = 9,
    InvalidWindowMode = 10,
    DuplicateWindowSize = 11,
    MissingWindowSize = 12,
    InvalidWindowSize = 13,
}

internal sealed class ClientStartupOptions
{
    private const string ContentRootOptionName = "--content-root";
    private const string PresentationOptionName = "--presentation";
    private const string WindowModeOptionName = "--window-mode";
    private const string WindowSizeOptionName = "--window-size";

    private const int MaximumWindowDimension = 16_384;
    private const long MaximumWindowArea = 7_680L * 4_320L;

    private static readonly (string Name, PresentationPolicy Policy)[] s_presentationOptions =
    [
        ("fit", PresentationPolicy.Fit),
        ("integer", PresentationPolicy.IntegerScale),
        ("stretch", PresentationPolicy.Stretch),
    ];

    private static readonly (string Name, DesktopWindowMode Mode)[] s_windowModeOptions =
    [
        ("resizable", DesktopWindowMode.Resizable),
        ("fixed", DesktopWindowMode.Fixed),
        ("fullscreen", DesktopWindowMode.Fullscreen),
    ];

    private ClientStartupOptions(string contentRootPath, PresentationPolicy presentationPolicy, DesktopWindowMode windowMode, PixelSize windowSize)
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

    public PresentationPolicy PresentationPolicy
    {
        get;
    }

    public DesktopWindowMode WindowMode
    {
        get;
    }

    /// <summary>
    /// The requested window size. Fullscreen presentation may use the active display's framebuffer size instead.
    /// </summary>
    public PixelSize WindowSize
    {
        get;
    }

    public static bool TryParse(string[] args, [NotNullWhen(true)] out ClientStartupOptions? options, out ClientStartupArgumentCode errorCode)
    {
        ArgumentNullException.ThrowIfNull(args);

        string packagedContentRoot = Path.Combine(AppContext.BaseDirectory, "content", "retail-5517", "payload");

        return TryParse(args, packagedContentRoot, Environment.CurrentDirectory, out options, out errorCode);
    }

    internal static bool TryParse(IReadOnlyList<string> args, string defaultContentRootPath, string workingDirectoryPath, [NotNullWhen(true)] out ClientStartupOptions? options, out ClientStartupArgumentCode errorCode)
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
                            return Fail(ClientStartupArgumentCode.DuplicateContentRoot, out options, out errorCode);
                        }

                        if (!TryReadOptionValue(args, ref index, out string? configuredPath))
                        {
                            return Fail(ClientStartupArgumentCode.MissingContentRoot, out options, out errorCode);
                        }

                        if (!TryResolveConfiguredPath(configuredPath, normalizedWorkingDirectoryPath, out string? resolvedContentRootPath))
                        {
                            return Fail(ClientStartupArgumentCode.InvalidContentRoot, out options, out errorCode);
                        }

                        contentRootPath = resolvedContentRootPath;
                        contentRootSpecified = true;

                        break;
                    }

                case PresentationOptionName:
                    {
                        if (presentationSpecified)
                        {
                            return Fail(ClientStartupArgumentCode.DuplicatePresentation, out options, out errorCode);
                        }

                        if (!TryReadOptionValue(args, ref index, out string? configuredPolicy))
                        {
                            return Fail(ClientStartupArgumentCode.MissingPresentation, out options, out errorCode);
                        }

                        if (!TryParsePresentationPolicy(configuredPolicy, out presentationPolicy))
                        {
                            return Fail(ClientStartupArgumentCode.InvalidPresentation, out options, out errorCode);
                        }

                        presentationSpecified = true;

                        break;
                    }

                case WindowModeOptionName:
                    {
                        if (windowModeSpecified)
                        {
                            return Fail(ClientStartupArgumentCode.DuplicateWindowMode, out options, out errorCode);
                        }

                        if (!TryReadOptionValue(args, ref index, out string? configuredMode))
                        {
                            return Fail(ClientStartupArgumentCode.MissingWindowMode, out options, out errorCode);
                        }

                        if (!TryParseWindowMode(configuredMode, out windowMode))
                        {
                            return Fail(ClientStartupArgumentCode.InvalidWindowMode, out options, out errorCode);
                        }

                        windowModeSpecified = true;

                        break;
                    }

                case WindowSizeOptionName:
                    {
                        if (windowSizeSpecified)
                        {
                            return Fail(ClientStartupArgumentCode.DuplicateWindowSize, out options, out errorCode);
                        }

                        if (!TryReadOptionValue(args, ref index, out string? configuredSize))
                        {
                            return Fail(ClientStartupArgumentCode.MissingWindowSize, out options, out errorCode);
                        }

                        if (!TryParseWindowSize(configuredSize, out windowSize))
                        {
                            return Fail(ClientStartupArgumentCode.InvalidWindowSize, out options, out errorCode);
                        }

                        windowSizeSpecified = true;

                        break;
                    }

                default:
                    return Fail(ClientStartupArgumentCode.UnknownArgument, out options, out errorCode);
            }
        }

        options = new ClientStartupOptions(contentRootPath, presentationPolicy, windowMode, windowSize);
        errorCode = ClientStartupArgumentCode.None;

        return true;
    }

    private static bool TryReadOptionValue(IReadOnlyList<string> args, ref int index, [NotNullWhen(true)] out string? value)
    {
        if (index + 1 >= args.Count)
        {
            value = null;
            return false;
        }

        string candidate = args[index + 1];
        if (string.IsNullOrWhiteSpace(candidate) || candidate.StartsWith("--", StringComparison.Ordinal))
        {
            value = null;
            return false;
        }

        index++;
        value = candidate;
        return true;
    }

    private static bool TryParsePresentationPolicy(string value, out PresentationPolicy policy)
    {
        foreach ((string name, PresentationPolicy candidate) in s_presentationOptions)
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
        foreach ((string name, DesktopWindowMode candidate) in s_windowModeOptions)
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
            width <= 0 || height <= 0 || width > MaximumWindowDimension || height > MaximumWindowDimension ||
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

    private static bool Fail(ClientStartupArgumentCode error, out ClientStartupOptions? options, out ClientStartupArgumentCode errorCode)
    {
        options = null;
        errorCode = error;

        return false;
    }
}
