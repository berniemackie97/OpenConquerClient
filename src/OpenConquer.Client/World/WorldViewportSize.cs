using OpenConquer.Platform.Geometry;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.World;

internal static class WorldViewportSize
{
    internal static LogicalRenderSize FromFramebuffer(LogicalRenderSize stage, PixelSize framebuffer)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(framebuffer.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(framebuffer.Height);

        int width = checked((int)Math.Round((double)stage.Height * framebuffer.Width / framebuffer.Height));
        return new LogicalRenderSize(Math.Max(1, width), stage.Height);
    }
}
