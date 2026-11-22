using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Login.Stage;

internal readonly record struct LoginRect(int X, int Y, int Width, int Height)
{
    public bool Contains(int x, int y)
    {
        return x >= X && y >= Y && (long)x < (long)X + Width && (long)y < (long)Y + Height;
    }
}

internal readonly record struct LoginPoint(int X, int Y);

internal sealed class LoginStageLayout
{
    public const int RegionColumnCount = 2;
    public const int RegionRowCount = 8;
    public const int ServerColumnCount = 3;
    public const int ServerRowCount = 4;

    private const int ReferenceWidth = 1024;
    private const int ReferenceHeight = 768;

    private const double RegionPanelScaleX = 0.98028564453125;
    private const double RegionPanelScaleY = 0.9801177978515625;
    private const double RegionPanelX = 150;
    private const double RegionPanelY = 381;

    private const double ServerPanelX = 685;
    private const double ServerPanelY = 373;

    private static readonly double[] s_regionColumnX = [-93, 64.85];
    private static readonly double[] s_regionRowY = [-209, -159, -108, -57, -6, 45, 96, 147];

    private static readonly (double X, double Y)[] s_serverOrigins =
    [
        (-259.3, -179.25), (-91, -179.25), (77.35, -178.25),
        (-259.3, -87.75), (-91, -87.75), (77.35, -87.75),
        (-259.3, 3.5), (-91, 3.5), (77.35, 3.5),
        (-259.3, 94.25), (-91, 94.25), (77.35, 94.25),
    ];

    private readonly LoginPoint[] _regionOrigins = new LoginPoint[RegionColumnCount * RegionRowCount];
    private readonly LoginRect[] _regionTiles = new LoginRect[RegionColumnCount * RegionRowCount];
    private readonly LoginRect[] _regionHitAreas = new LoginRect[RegionColumnCount * RegionRowCount];
    private readonly LoginPoint[] _serverOrigins = new LoginPoint[ServerColumnCount * ServerRowCount];
    private readonly LoginRect[] _serverHitAreas = new LoginRect[ServerColumnCount * ServerRowCount];
    private readonly LoginRect[] _serverLights = new LoginRect[ServerColumnCount * ServerRowCount];
    private readonly LoginRect[] _serverLabels = new LoginRect[ServerColumnCount * ServerRowCount];

    private LoginStageLayout(LogicalRenderSize size)
    {
        Width = size.Width;
        Height = size.Height;

        Backdrop = Place(0, 41, 1024, 637);
        StageBorder = Place(0, 0, 1024, 768);
        RegionPanel = Place(43, 162, 340, 434);
        AccountPanel = Place(256, 606, 508, 98);
        Logo = Place(776, 2, 248, 78);
        VersionLabel = PlaceExact(18, 15, 190, 17.9);
        RegionPagePrevious = Place(137, 603, 38, 34);
        RegionPageNext = Place(249, 603, 40, 34);
        RegionPageText = PlaceExact(RegionPanelX + RegionPanelScaleX * 28.5, RegionPanelY + RegionPanelScaleY * 232.85, RegionPanelScaleX * 71, RegionPanelScaleY * 22.35 * 1.0020599365234375);

        for (int column = 0; column < RegionColumnCount; column++)
        {
            for (int row = 0; row < RegionRowCount; row++)
            {
                int slot = column * RegionRowCount + row;
                double originX = RegionPanelX + RegionPanelScaleX * s_regionColumnX[column];
                double originY = RegionPanelY + RegionPanelScaleY * s_regionRowY[row];
                LoginPoint origin = Point(originX, originY);
                _regionOrigins[slot] = origin;
                _regionTiles[slot] = Offset(origin, 0, -1, 151, 53);
                _regionHitAreas[slot] = PlaceExact(originX + RegionPanelScaleX * 1.0866, originY + RegionPanelScaleY * 1.7232, RegionPanelScaleX * 152.5934, RegionPanelScaleY * 50.0044);
            }
        }

        for (int slot = 0; slot < _serverOrigins.Length; slot++)
        {
            double originX = ServerPanelX + s_serverOrigins[slot].X;
            double originY = ServerPanelY + s_serverOrigins[slot].Y;
            _serverOrigins[slot] = Point(originX, originY);
            _serverHitAreas[slot] = PlaceExact(originX + 6.3705, originY + 1.5149, 149.9903, 82.9992);
            _serverLights[slot] = PlaceExact(originX + 9.3, originY + 62.25, 18, 18);
            _serverLabels[slot] = PlaceExact(originX + 30.5, originY + 64.8, 119.05 * 1.00433349609375, 15.6);
        }
    }

    public int Width
    {
        get;
    }

    public int Height
    {
        get;
    }

    public LoginRect Backdrop
    {
        get;
    }

    public LoginRect StageBorder
    {
        get;
    }

    public LoginRect RegionPanel
    {
        get;
    }

    public LoginRect AccountPanel
    {
        get;
    }

    public LoginRect Logo
    {
        get;
    }

    public LoginRect VersionLabel
    {
        get;
    }

    public LoginRect RegionPagePrevious
    {
        get;
    }

    public LoginRect RegionPageNext
    {
        get;
    }

    public LoginRect RegionPageText
    {
        get;
    }

    public ReadOnlySpan<LoginPoint> RegionOrigins => _regionOrigins;
    public ReadOnlySpan<LoginRect> RegionTiles => _regionTiles;
    public ReadOnlySpan<LoginRect> RegionHitAreas => _regionHitAreas;
    public ReadOnlySpan<LoginPoint> ServerOrigins => _serverOrigins;
    public ReadOnlySpan<LoginRect> ServerHitAreas => _serverHitAreas;
    public ReadOnlySpan<LoginRect> ServerLights => _serverLights;
    public ReadOnlySpan<LoginRect> ServerLabels => _serverLabels;

    public static LoginStageLayout Create(LogicalRenderSize size)
    {
        if (size.Width <= 0 || size.Height <= 0 || (long)size.Width * ReferenceHeight != (long)size.Height * ReferenceWidth)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Login visuals require a positive 4:3 logical size.");
        }

        return new LoginStageLayout(size);
    }

    public int Scale(int referenceValue)
    {
        return checked((int)Math.Floor(((long)referenceValue * Width + ReferenceWidth / 2) / (double)ReferenceWidth));
    }

    public LoginRect Offset(LoginPoint origin, int offsetX, int offsetY, int width, int height)
    {
        return new LoginRect(checked(origin.X + Scale(offsetX)), checked(origin.Y + Scale(offsetY)), Scale(width), Scale(height));
    }

    public LoginRect Place(int x, int y, int width, int height)
    {
        return new LoginRect(Scale(x), Scale(y), Scale(width), Scale(height));
    }

    public static int HitSlot(ReadOnlySpan<LoginRect> areas, int x, int y)
    {
        for (int slot = 0; slot < areas.Length; slot++)
        {
            if (areas[slot].Contains(x, y))
            {
                return slot;
            }
        }

        return -1;
    }

    private LoginPoint Point(double x, double y)
    {
        return new LoginPoint(Round(x), Round(y));
    }

    public LoginRect PlaceExact(double x, double y, double width, double height)
    {
        int left = Round(x);
        int top = Round(y);
        return new LoginRect(left, top, Round(x + width) - left, Round(y + height) - top);
    }

    private int Round(double referenceValue)
    {
        return checked((int)Math.Floor(referenceValue * Width / ReferenceWidth + 0.5));
    }
}
