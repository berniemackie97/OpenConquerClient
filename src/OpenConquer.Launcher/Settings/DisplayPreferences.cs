namespace OpenConquer.Launcher.Settings;

internal enum GameWindowMode
{
    Resizable, Fixed, Fullscreen
}
internal enum GamePresentation
{
    Fit, Integer, Stretch
}

/// <summary>Non-secret preferences; never installation identity or launch authorization.</summary>
internal sealed record DisplayPreferences
{
    public const int MaximumDimension = 16_384;
    public const long MaximumArea = 7_680L * 4_320L;
    public static DisplayPreferences Default { get; } = new(1280, 720, GameWindowMode.Resizable, GamePresentation.Fit);

    public DisplayPreferences(int width, int height, GameWindowMode windowMode, GamePresentation presentation)
    {
        if (!IsValidSize(width, height))
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Unsupported display dimensions.");
        }
        if (!Enum.IsDefined(windowMode))
        {
            throw new ArgumentOutOfRangeException(nameof(windowMode));
        }

        if (!Enum.IsDefined(presentation))
        {
            throw new ArgumentOutOfRangeException(nameof(presentation));
        }

        Width = width;
        Height = height;
        WindowMode = windowMode;
        Presentation = presentation;
    }

    public int Width
    {
        get;
    }
    public int Height
    {
        get;
    }
    public GameWindowMode WindowMode
    {
        get;
    }
    public GamePresentation Presentation
    {
        get;
    }

    public static bool IsValidSize(int width, int height)
    {
        return width > 0 && height > 0 && width <= MaximumDimension && height <= MaximumDimension && (long)width * height <= MaximumArea;
    }
}
