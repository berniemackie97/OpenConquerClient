using OpenConquer.Rendering.Conformance.Support;

namespace OpenConquer.Rendering.Conformance.Reference;

internal static class SyndicateGeometryReference
{
    private const int FrameWidth = 14;
    private const int FrameHeight = 14;
    private const int TargetWidth = 32;
    private const int TargetHeight = 32;
    private const int NaturalX = 7;
    private const int NaturalY = 9;

    public static byte[] ComposeNearest(ReadOnlySpan<byte> naturalFramebuffer, SpriteSourceRectangle sourceRectangle, int destinationX, int destinationY, int destinationWidth, int destinationHeight)
    {
        int expectedFramebufferLength = checked(TargetWidth * TargetHeight * 4);

        if (naturalFramebuffer.Length != expectedFramebufferLength)
        {
            throw new ArgumentException($"Expected a {TargetWidth}x{TargetHeight} RGBA framebuffer.", nameof(naturalFramebuffer));
        }

        ValidateSourceRectangle(sourceRectangle);
        ValidateDestination(destinationX, destinationY, destinationWidth, destinationHeight);

        byte[] expected = FramebufferVerifier.CreateOpaqueBlack(TargetWidth, TargetHeight);

        for (int destinationOffsetY = 0; destinationOffsetY < destinationHeight; destinationOffsetY++)
        {
            int sourceOffsetY = GetNearestSourceOffset(destinationOffsetY, destinationHeight, sourceRectangle.Height);
            int sourceY = NaturalY + sourceRectangle.Y + sourceOffsetY;

            for (int destinationOffsetX = 0; destinationOffsetX < destinationWidth; destinationOffsetX++)
            {
                int sourceOffsetX = GetNearestSourceOffset(destinationOffsetX, destinationWidth, sourceRectangle.Width);
                int sourceX = NaturalX + sourceRectangle.X + sourceOffsetX;

                int sourceOffset = checked(((sourceY * TargetWidth) + sourceX) * 4);
                int outputX = destinationX + destinationOffsetX;
                int outputY = destinationY + destinationOffsetY;
                int destinationOffset = checked(((outputY * TargetWidth) + outputX) * 4);

                naturalFramebuffer.Slice(sourceOffset, 4).CopyTo(expected.AsSpan(destinationOffset, 4));
            }
        }

        return expected;
    }

    private static void ValidateSourceRectangle(SpriteSourceRectangle sourceRectangle)
    {
        long right = (long)sourceRectangle.X + sourceRectangle.Width;
        long bottom = (long)sourceRectangle.Y + sourceRectangle.Height;

        if (sourceRectangle.X < 0 || sourceRectangle.Y < 0 || sourceRectangle.Width <= 0 || sourceRectangle.Height <= 0 || right > FrameWidth || bottom > FrameHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRectangle), sourceRectangle, "The Syndicate source rectangle must fit within the verified retail frame.");
        }
    }

    private static void ValidateDestination(int x, int y, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        long right = (long)x + width;
        long bottom = (long)y + height;

        if (right > TargetWidth || bottom > TargetHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "The Syndicate destination rectangle must fit within the logical target.");
        }
    }

    private static int GetNearestSourceOffset(int destinationOffset, int destinationExtent, int sourceExtent)
    {
        long numerator = (2L * destinationOffset + 1) * sourceExtent;
        long denominator = 2L * destinationExtent;

        return (int)(numerator / denominator);
    }
}
