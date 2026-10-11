using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Login;

internal readonly record struct LoginSpritePlacement(int X, int Y, int Width, int Height);

/// <summary>
/// Maps independently rendered retail-derived decorations from the 1024x768 reference stage
/// to the configured 4:3 client logical resolution.
/// </summary>
internal sealed class LoginVisualLayout
{
    private const int ReferenceWidth = 1024;
    private const int ReferenceHeight = 768;

    public int Width
    {
        get;
    }
    public int Height
    {
        get;
    }
    public LoginSpritePlacement TopDragon
    {
        get;
    }
    public LoginSpritePlacement RegionSlot
    {
        get;
    }
    public LoginSpritePlacement ServerSlot
    {
        get;
    }

    private LoginVisualLayout(LogicalRenderSize size)
    {
        Width = size.Width;
        Height = size.Height;
        TopDragon = Place(267, 26, 494, 140);
        RegionSlot = Place(150, 381, 158, 58);
        ServerSlot = Place(685, 373, 158, 58);
    }

    public static LoginVisualLayout Create(LogicalRenderSize size)
    {
        if (size.Width <= 0 || size.Height <= 0 || (long)size.Width * ReferenceHeight != (long)size.Height * ReferenceWidth)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Login visuals require a positive 4:3 logical size.");
        }

        return new LoginVisualLayout(size);
    }

    private LoginSpritePlacement Place(int x, int y, int width, int height) => new(
        Scale(x, Width, ReferenceWidth), Scale(y, Height, ReferenceHeight),
        Scale(width, Width, ReferenceWidth), Scale(height, Height, ReferenceHeight));

    private static int Scale(int value, int destination, int reference) => checked((int)(((long)value * destination + reference / 2) / reference));
}
