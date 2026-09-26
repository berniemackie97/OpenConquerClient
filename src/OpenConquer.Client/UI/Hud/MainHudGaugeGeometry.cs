using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Hud;

internal readonly record struct MainHudGaugeDraw
{
    public MainHudGaugeDraw(int frameIndex, SpriteSourceBounds sourceBounds, int x, int y, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        FrameIndex = frameIndex;
        SourceBounds = sourceBounds;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int FrameIndex
    {
        get;
    }

    public SpriteSourceBounds SourceBounds
    {
        get;
    }

    public int X
    {
        get;
    }

    public int Y
    {
        get;
    }

    public int Width
    {
        get;
    }

    public int Height
    {
        get;
    }

    public int ResolveDestinationHeight(int imageHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageHeight);
        return Height == 0 ? imageHeight : Height;
    }
}

internal readonly record struct MainHudGaugeDrawSequence
{
    private MainHudGaugeDrawSequence(int count, MainHudGaugeDraw first, MainHudGaugeDraw second)
    {
        Count = count;
        First = first;
        Second = second;
    }

    public int Count
    {
        get;
    }

    public MainHudGaugeDraw First
    {
        get;
    }

    public MainHudGaugeDraw Second
    {
        get;
    }

    public static MainHudGaugeDrawSequence Single(MainHudGaugeDraw draw) => new(1, draw, default);
    public static MainHudGaugeDrawSequence Pair(MainHudGaugeDraw first, MainHudGaugeDraw second) => new(2, first, second);
}

internal static class MainHudGaugeGeometry
{
    public static MainHudGaugeDrawSequence CreateStyle0(int x, int y, int width, int height, int alternateWidth, int maximum, int value, int follower, int subVariant)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(alternateWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfNegative(follower);

        if (subVariant is not 0 and not 1)
        {
            throw new ArgumentOutOfRangeException(nameof(subVariant), subVariant, "Main HUD style-0 gauges only use subvariants 0 and 1.");
        }

        if (maximum <= 0)
        {
            return default;
        }

        int current = Math.Min(value, maximum);
        int secondary = Math.Min(follower, maximum);
        float pixelsPerUnit = (float)height / maximum;
        float cappedPixelsPerUnit = maximum >= 100 ? height * 0.01f : pixelsPerUnit;

        return subVariant == 0
            ? CreateNormal(x, y, width, height, current, secondary, pixelsPerUnit, cappedPixelsPerUnit)
            : CreateAlternate(x, y, alternateWidth, height, current, secondary, pixelsPerUnit);
    }

    private static MainHudGaugeDrawSequence CreateNormal(int x, int y, int width, int height, int current, int follower, float pixelsPerUnit, float cappedPixelsPerUnit)
    {
        if (current <= follower)
        {
            if (current == 0)
            {
                return default;
            }

            int pixels = (int)(current * pixelsPerUnit);
            return MainHudGaugeDrawSequence.Single(CreateDraw(frameIndex: 0, x, y, width, height, pixels, pixels));
        }

        int currentPixels = (int)(current * pixelsPerUnit);
        MainHudGaugeDraw currentDraw = CreateDraw(frameIndex: 1, x, y, width, height, currentPixels, currentPixels);

        int bias = (int)(cappedPixelsPerUnit / pixelsPerUnit + 0.5f);
        float followerExact = (follower + bias) * pixelsPerUnit;
        int followerSourcePixels = (int)followerExact;
        int followerDestinationPixels = (int)(followerExact + 0.5f);
        MainHudGaugeDraw followerDraw = CreateDraw(frameIndex: 0, x, y, width, height, followerSourcePixels, followerDestinationPixels);

        return MainHudGaugeDrawSequence.Pair(currentDraw, followerDraw);
    }

    private static MainHudGaugeDrawSequence CreateAlternate(int x, int y, int width, int height, int current, int follower, float pixelsPerUnit)
    {
        if (current <= follower)
        {
            if (current == 0)
            {
                return default;
            }

            int pixels = (int)(current * pixelsPerUnit);
            return MainHudGaugeDrawSequence.Single(CreateDraw(frameIndex: 2, x, y, width, height, pixels, pixels));
        }

        float followerExact = follower * pixelsPerUnit;
        int followerSourcePixels = (int)followerExact;
        int followerDestinationPixels = (int)(followerExact + 0.5f);

        return MainHudGaugeDrawSequence.Single(CreateDraw(frameIndex: 2, x, y, width, height, followerSourcePixels, followerDestinationPixels));
    }

    private static MainHudGaugeDraw CreateDraw(int frameIndex, int x, int y, int width, int height, int sourcePixels, int destinationPixels)
    {
        SpriteSourceBounds sourceBounds = new(left: 0, top: height - sourcePixels, right: width, bottom: height);
        return new MainHudGaugeDraw(frameIndex, sourceBounds, x, y + height - sourcePixels, width, destinationPixels);
    }
}
