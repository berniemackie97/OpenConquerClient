using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Login;

internal readonly record struct LoginSpritePlacement(int X, int Y, int Width, int Height);

/// <summary>Places independent login artwork on the fixed 1024x768 reference stage.</summary>
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
    public LoginSpritePlacement Backdrop
    {
        get;
    }
    public LoginSpritePlacement Header
    {
        get;
    }
    public LoginSpritePlacement Footer
    {
        get;
    }
    public LoginSpritePlacement AccountPanel
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
        Backdrop = Place(0, 40, 1024, 637);
        Header = Place(0, 0, 1024, 116);
        Footer = Place(0, 660, 1024, 140);
        AccountPanel = Place(258, 637, 508, 98);
        TopDragon = Place(267, 26, 494, 140);
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
