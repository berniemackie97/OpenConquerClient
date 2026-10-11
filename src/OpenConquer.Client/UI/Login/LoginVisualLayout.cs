using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Login;

internal readonly record struct LoginSpritePlacement(int X, int Y, int Width, int Height);

/// <summary>Places the verified login ornament on the 1024x768 reference stage.</summary>
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

    private LoginVisualLayout(LogicalRenderSize size)
    {
        Width = size.Width;
        Height = size.Height;
        TopDragon = new LoginSpritePlacement(Scale(267, Width, ReferenceWidth), Scale(26, Height, ReferenceHeight),
            Scale(494, Width, ReferenceWidth), Scale(140, Height, ReferenceHeight));
    }

    public static LoginVisualLayout Create(LogicalRenderSize size)
    {
        if (size.Width <= 0 || size.Height <= 0 || (long)size.Width * ReferenceHeight != (long)size.Height * ReferenceWidth)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Login visuals require a positive 4:3 logical size.");
        }

        return new LoginVisualLayout(size);
    }

    private static int Scale(int value, int destination, int reference) => checked((int)(((long)value * destination + reference / 2) / reference));
}
