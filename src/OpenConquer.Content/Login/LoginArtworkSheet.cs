namespace OpenConquer.Content.Login;

/// <summary>An image holding <see cref="Count"/> equally sized frames, left to right and then top to bottom.</summary>
public readonly record struct LoginArtworkSheet(int Width, int Height, int FrameWidth, int FrameHeight, int Columns, int Count)
{
    public static LoginArtworkSheet OneFrame(int width, int height)
    {
        return new LoginArtworkSheet(width, height, width, height, Columns: 1, Count: 1);
    }

    public int FrameX(int frame)
    {
        return Validate(frame) % Columns * FrameWidth;
    }

    public int FrameY(int frame)
    {
        return Validate(frame) / Columns * FrameHeight;
    }

    private int Validate(int frame)
    {
        return (uint)frame < (uint)Count ? frame : throw new ArgumentOutOfRangeException(nameof(frame));
    }
}
