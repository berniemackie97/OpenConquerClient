using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Login;

internal readonly record struct LoginSpritePlacement(int X, int Y, int Width, int Height);

/// <summary>Origin of a retail movie holder (a <c>loadMovie</c> target) on the logical stage.</summary>
internal readonly record struct LoginStagePoint(int X, int Y);

/// <summary>Places the retail <c>start.dat</c> display list on the 1024x768 stage and supported 4:3 logical sizes.</summary>
internal sealed class LoginVisualLayout
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
    // holder's y toward aPosData, which settles while the root is still hidden; x is never moved.
    private static readonly double[] s_regionColumnX = [-93, 64.85];
    private static readonly double[] s_regionRowY = [-209, -159, -108, -57, -6, 45, 96, 147];

    // Sprite 180 initPos(): aPosData for ss1..ss12 (ss3 is authored one pixel above its row).
    private static readonly (double X, double Y)[] s_serverHolders =
    [
        (-259.3, -179.25), (-91, -179.25), (77.35, -178.25),
        (-259.3, -87.75), (-91, -87.75), (77.35, -87.75),
        (-259.3, 3.5), (-91, 3.5), (77.35, 3.5),
        (-259.3, 94.25), (-91, 94.25), (77.35, 94.25),
    ];

    private readonly LoginStagePoint[] _regionHolders = new LoginStagePoint[RegionColumnCount * RegionRowCount];
    private readonly LoginSpritePlacement[] _regionTiles = new LoginSpritePlacement[RegionColumnCount * RegionRowCount];
    private readonly LoginSpritePlacement[] _regionHitAreas = new LoginSpritePlacement[RegionColumnCount * RegionRowCount];
    private readonly LoginStagePoint[] _serverHolders = new LoginStagePoint[ServerColumnCount * ServerRowCount];
    private readonly LoginSpritePlacement[] _serverHitAreas = new LoginSpritePlacement[ServerColumnCount * ServerRowCount];
    private readonly LoginSpritePlacement[] _serverLabels = new LoginSpritePlacement[ServerColumnCount * ServerRowCount];

    private LoginVisualLayout(LogicalRenderSize size)
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
                double holderX = RegionPanelX + RegionPanelScaleX * s_regionColumnX[column];
                double holderY = RegionPanelY + RegionPanelScaleY * s_regionRowY[row];
                LoginStagePoint holder = Point(holderX, holderY);
                _regionHolders[slot] = holder;
                // defaultR shape 3: 154x54 bitmap 2 at (0, -1), shown at the panel scale.
                _regionTiles[slot] = Offset(holder, 0, -1, 151, 53);
                // defaultR hitMC: sprite 5 (-55.2,-199.6)-(60.8,-106.6) under [1.31546, 0, 0, 0.53772, 73.7, 109.05].
                _regionHitAreas[slot] = PlaceExact(holderX + RegionPanelScaleX * 1.0866, holderY + RegionPanelScaleY * 1.7232,
                    RegionPanelScaleX * 152.5934, RegionPanelScaleY * 50.0044);
            }
        }

        for (int slot = 0; slot < _serverHolders.Length; slot++)
        {
            double holderX = ServerPanelX + s_serverHolders[slot].X;
            double holderY = ServerPanelY + s_serverHolders[slot].Y;
            _serverHolders[slot] = Point(holderX, holderY);
            // Sprite 179 hitMC: sprite 37 (-55.2,-199.6)-(60.8,-106.6) under [1.29311, 0, 0, 0.89247, 77.75, 179.65].
            _serverHitAreas[slot] = PlaceExact(holderX + 6.3705, holderY + 1.5149, 149.9903, 82.9992);
            // Sprite 179 nameTxt: edit text 35 at (30.5, 64.8), x-scale 1.0043335, inside its 2-pixel gutter.
            _serverLabels[slot] = PlaceExact(holderX + 30.5, holderY + 64.8, 119.05 * 1.00433349609375, 15.6);
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

    public LoginSpritePlacement Backdrop
    {
        get;
    }

    public LoginSpritePlacement StageBorder
    {
        get;
    }

    public LoginSpritePlacement RegionPanel
    {
        get;
    }

    public LoginSpritePlacement AccountPanel
    {
        get;
    }

    public LoginSpritePlacement VersionLabel
    {
        get;
    }

    public LoginSpritePlacement RegionPagePrevious
    {
        get;
    }

    public LoginSpritePlacement RegionPageNext
    {
        get;
    }

    public LoginSpritePlacement RegionPageText
    {
        get;
    }

    /// <summary>Sprite 259 holders <c>sr1..sr16</c>; slots run down the first column, then the second.</summary>
    public ReadOnlySpan<LoginStagePoint> RegionHolders => _regionHolders;

    public ReadOnlySpan<LoginSpritePlacement> RegionTiles => _regionTiles;

    public ReadOnlySpan<LoginSpritePlacement> RegionHitAreas => _regionHitAreas;

    /// <summary>Sprite 180 holders <c>ss1..ss12</c>, three per row.</summary>
    public ReadOnlySpan<LoginStagePoint> ServerHolders => _serverHolders;

    public ReadOnlySpan<LoginSpritePlacement> ServerHitAreas => _serverHitAreas;

    public ReadOnlySpan<LoginSpritePlacement> ServerLabels => _serverLabels;

    public static LoginVisualLayout Create(LogicalRenderSize size)
    {
        if (size.Width <= 0 || size.Height <= 0 || (long)size.Width * ReferenceHeight != (long)size.Height * ReferenceWidth)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Login visuals require a positive 4:3 logical size.");
        }

        return new LoginVisualLayout(size);
    }

    /// <summary>Scales a 1024x768 reference length or offset to the logical stage, rounding half up for either sign.</summary>
    public int Scale(int referenceValue) => checked((int)Math.Floor(((long)referenceValue * Width + ReferenceWidth / 2) / (double)ReferenceWidth));

    /// <summary>Places reference-sized artwork at a reference offset from a movie holder.</summary>
    public LoginSpritePlacement Offset(LoginStagePoint holder, int offsetX, int offsetY, int width, int height) =>
        new(checked(holder.X + Scale(offsetX)), checked(holder.Y + Scale(offsetY)), Scale(width), Scale(height));

    public static bool Contains(LoginSpritePlacement area, int x, int y) =>
        x >= area.X && y >= area.Y && (long)x < (long)area.X + area.Width && (long)y < (long)area.Y + area.Height;

    public static int HitSlot(ReadOnlySpan<LoginSpritePlacement> areas, int x, int y)
    {
        for (int slot = 0; slot < areas.Length; slot++)
        {
            if (Contains(areas[slot], x, y))
            {
                return slot;
            }
        }

        return -1;
    }

    private LoginSpritePlacement Place(int x, int y, int width, int height) => new(Scale(x), Scale(y), Scale(width), Scale(height));

    private LoginStagePoint Point(double x, double y) => new(Round(x), Round(y));

    private LoginSpritePlacement PlaceExact(double x, double y, double width, double height)
    {
        int left = Round(x);
        int top = Round(y);
        return new LoginSpritePlacement(left, top, Round(x + width) - left, Round(y + height) - top);
    }

    private int Round(double referenceValue) => checked((int)Math.Floor(referenceValue * Width / ReferenceWidth + 0.5));
}
