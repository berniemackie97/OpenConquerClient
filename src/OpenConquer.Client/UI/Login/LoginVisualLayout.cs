using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Login;

internal readonly record struct LoginSpritePlacement(int X, int Y, int Width, int Height);

/// <summary>Places login artwork on the 1024x768 reference stage and supported 4:3 logical sizes.</summary>
internal sealed class LoginVisualLayout
{
    private const int ReferenceWidth = 1024;
    private const int ReferenceHeight = 768;
    private const int RegionColumnCount = 2;
    private const int RegionRowCount = 8;
    private const int ServerColumnCount = 3;
    private const int ServerRowCount = 4;
    private readonly LoginSpritePlacement[] _regionCells;
    private readonly LoginSpritePlacement[] _serverCells;

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
    public ReadOnlySpan<LoginSpritePlacement> RegionCells => _regionCells;
    public ReadOnlySpan<LoginSpritePlacement> ServerCells => _serverCells;

    private LoginVisualLayout(LogicalRenderSize size)
    {
        Width = size.Width;
        Height = size.Height;
        Backdrop = Place(0, 40, 1024, 637);
        Header = Place(0, 0, 1024, 116);
        Footer = Place(0, 660, 1024, 140);
        AccountPanel = Place(258, 637, 508, 98);
        TopDragon = Place(267, 26, 494, 140);

        _regionCells = new LoginSpritePlacement[RegionColumnCount * RegionRowCount];
        _serverCells = new LoginSpritePlacement[ServerColumnCount * ServerRowCount];

        for (int row = 0; row < RegionRowCount; row++)
        {
            for (int column = 0; column < RegionColumnCount; column++)
            {
                _regionCells[row * RegionColumnCount + column] = Place(55 + column * 161, 194 + row * 50, 154, 50);
            }
        }

        for (int row = 0; row < ServerRowCount; row++)
        {
            for (int column = 0; column < ServerColumnCount; column++)
            {
                _serverCells[row * ServerColumnCount + column] = Place(431 + column * 168, 218 + row * 90, 158, 58);
            }
        }
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
