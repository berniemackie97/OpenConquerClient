using OpenConquer.Platform.Geometry;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.World;

internal static class WorldViewportSize
{
    internal static LogicalRenderSize FromFramebuffer(LogicalRenderSize stage, PixelSize framebuffer,
        LogicalRenderSize backdropExtent, int maximumRenderTargetDimension)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(framebuffer.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(framebuffer.Height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRenderTargetDimension);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(backdropExtent.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(backdropExtent.Height);

        int height = Math.Min(stage.Height, Math.Min(backdropExtent.Height, maximumRenderTargetDimension));
        int widthLimit = Math.Min(backdropExtent.Width, maximumRenderTargetDimension);
        long width = (long)Math.Round((double)height * framebuffer.Width / framebuffer.Height);

        if (width > widthLimit)
        {
            width = widthLimit;
            height = Math.Max(1, (int)Math.Round((double)width * framebuffer.Height / framebuffer.Width));
        }

        return new LogicalRenderSize(Math.Max(1, (int)width), height);
    }
}
