using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Login.Stage;

/// <summary>A rectangle on the logical stage.</summary>
internal readonly record struct LoginRect(int X, int Y, int Width, int Height)
{
    public bool Contains(int x, int y) => x >= X && y >= Y && (long)x < (long)X + Width && (long)y < (long)Y + Height;
}

/// <summary>A point on the logical stage; region and server tiles are placed from their origin.</summary>
internal readonly record struct LoginPoint(int X, int Y);

/// <summary>Places the retail <c>start.dat</c> display list on the 1024x768 stage and supported 4:3 logical sizes.</summary>
internal sealed class LoginStageLayout
{
    public const int RegionColumnCount = 2;
    public const int RegionRowCount = 8;
    public const int ServerColumnCount = 3;
    public const int ServerRowCount = 4;

    private const int ReferenceWidth = 1024;
    private const int ReferenceHeight = 768;

    // Root depth 138: sprite 259 `sr`, matrix [0.98028564, 0, 0, 0.9801178, 150, 381].
    private const double RegionPanelScaleX = 0.98028564453125;
    private const double RegionPanelScaleY = 0.9801177978515625;
    private const double RegionPanelX = 150;
    private const double RegionPanelY = 381;

    // Root depth 4: sprite 180 `ss` at (685, 373).
    private const double ServerPanelX = 685;
    private const double ServerPanelY = 373;

    // Sprite 259 places sr1..sr8 at x -93 and sr9..sr16 at x 64.85. Its frame script springs every
    // tile's y toward aPosData, which settles while the root is still hidden; x is never moved.
    private static readonly double[] s_regionColumnX = [-93, 64.85];
    private static readonly double[] s_regionRowY = [-209, -159, -108, -57, -6, 45, 96, 147];

    // Sprite 180 initPos(): aPosData for ss1..ss12 (ss3 is authored one pixel above its row).
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

        // Root depth 1: shape 11 fills bitmap 10 (1024x637) from stage y 41.
        Backdrop = Place(0, 41, 1024, 637);
        // Root depth 216: shape 400, the full-stage top/bottom ornament placed at root frame 3.
        StageBorder = Place(0, 0, 1024, 768);
        // Sprite 259 depth 1: sprite 183 at (-99.7, -242.85), baked onto the stage pixel grid.
        RegionPanel = Place(43, 162, 340, 434);
        // Root depth 221 enterMC (512.5, 720.4): shape 328 at (256, 605.9).
        AccountPanel = Place(256, 606, 508, 98);
        // Root depth 274 logoMC: logo.swf loaded at flashLogin.ini [logo] (776, 2).
        Logo = Place(776, 2, 248, 78);
        // Root depth 276 verTxt (16, 13): edit text 8 at (2, 2); its text area starts inside the 2-pixel gutter.
        VersionLabel = PlaceExact(18, 15, 190, 17.9);
        // Sprite 259 leftBtn (199) and rightBtn (192), rotated a quarter turn, baked onto the stage.
        RegionPagePrevious = Place(137, 603, 38, 34);
        RegionPageNext = Place(249, 603, 40, 34);
        // Sprite 259 depth 3: pageTxt (edit text 184) at (28.5, 232.85), y-scale 1.00206, inside its gutter.
        RegionPageText = PlaceExact(RegionPanelX + RegionPanelScaleX * 28.5, RegionPanelY + RegionPanelScaleY * 232.85,
            RegionPanelScaleX * 71, RegionPanelScaleY * 22.35 * 1.0020599365234375);

        for (int column = 0; column < RegionColumnCount; column++)
        {
            for (int row = 0; row < RegionRowCount; row++)
            {
                int slot = column * RegionRowCount + row;
                double originX = RegionPanelX + RegionPanelScaleX * s_regionColumnX[column];
                double originY = RegionPanelY + RegionPanelScaleY * s_regionRowY[row];
                LoginPoint origin = Point(originX, originY);
                _regionOrigins[slot] = origin;
                // defaultR shape 3: 154x54 bitmap 2 at (0, -1), shown at the panel scale.
                _regionTiles[slot] = Offset(origin, 0, -1, 151, 53);
                // defaultR hitMC: sprite 5 (-55.2,-199.6)-(60.8,-106.6) under [1.31546, 0, 0, 0.53772, 73.7, 109.05].
                _regionHitAreas[slot] = PlaceExact(originX + RegionPanelScaleX * 1.0866, originY + RegionPanelScaleY * 1.7232,
                    RegionPanelScaleX * 152.5934, RegionPanelScaleY * 50.0044);
            }
        }

        for (int slot = 0; slot < _serverOrigins.Length; slot++)
        {
            double originX = ServerPanelX + s_serverOrigins[slot].X;
            double originY = ServerPanelY + s_serverOrigins[slot].Y;
            _serverOrigins[slot] = Point(originX, originY);
            // Sprite 179 hitMC: sprite 37 (-55.2,-199.6)-(60.8,-106.6) under [1.29311, 0, 0, 0.89247, 77.75, 179.65].
            _serverHitAreas[slot] = PlaceExact(originX + 6.3705, originY + 1.5149, 149.9903, 82.9992);
            // Sprite 179 light: sprite 34, bounds (0,0)-(18,18), at (9.3, 62.25).
            _serverLights[slot] = PlaceExact(originX + 9.3, originY + 62.25, 18, 18);
            // Sprite 179 nameTxt: edit text 35 at (30.5, 64.8), x-scale 1.0043335, inside its 2-pixel gutter.
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

    /// <summary>Sprite 259 <c>sr1..sr16</c>; slots run down the first column, then the second.</summary>
    public ReadOnlySpan<LoginPoint> RegionOrigins => _regionOrigins;

    public ReadOnlySpan<LoginRect> RegionTiles => _regionTiles;

    public ReadOnlySpan<LoginRect> RegionHitAreas => _regionHitAreas;

    /// <summary>Sprite 180 <c>ss1..ss12</c>, three per row.</summary>
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

    /// <summary>Scales a 1024x768 reference length or offset to the logical stage, rounding half up for either sign.</summary>
    public int Scale(int referenceValue) => checked((int)Math.Floor(((long)referenceValue * Width + ReferenceWidth / 2) / (double)ReferenceWidth));

    /// <summary>Places reference-sized artwork at a reference offset from a tile origin.</summary>
    public LoginRect Offset(LoginPoint origin, int offsetX, int offsetY, int width, int height) =>
        new(checked(origin.X + Scale(offsetX)), checked(origin.Y + Scale(offsetY)), Scale(width), Scale(height));

    /// <summary>Places a reference rectangle on the logical stage.</summary>
    public LoginRect Place(int x, int y, int width, int height) => new(Scale(x), Scale(y), Scale(width), Scale(height));

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

    private LoginPoint Point(double x, double y) => new(Round(x), Round(y));

    /// <summary>Places a fractional reference rectangle, rounding each edge to the nearest logical pixel.</summary>
    public LoginRect PlaceExact(double x, double y, double width, double height)
    {
        int left = Round(x);
        int top = Round(y);
        return new LoginRect(left, top, Round(x + width) - left, Round(y + height) - top);
    }

    private int Round(double referenceValue) => checked((int)Math.Floor(referenceValue * Width / ReferenceWidth + 0.5));
}
