namespace OpenConquer.Platform;

public readonly record struct PixelSize
{
    public PixelSize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        Width = width;
        Height = height;
    }

    public int Width
    {
        get;
    }

    public int Height
    {
        get;
    }
}
