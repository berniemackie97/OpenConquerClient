using OpenConquer.Platform;
using OpenConquer.Rendering.OpenGL;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class PresentationConformance
{
    private static readonly LogicalRenderSize[] s_logicalRenderSizes =
    [
        new(800, 600),
        new(1024, 768),
    ];

    public static void Run(OpenGLGraphicsDevice graphicsDevice, PixelSize framebufferSize)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        foreach (LogicalRenderSize logicalRenderSize in s_logicalRenderSizes)
        {
            RunCase(graphicsDevice, logicalRenderSize, framebufferSize);
        }
    }

    private static void RunCase(OpenGLGraphicsDevice graphicsDevice, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);

        renderer.RenderFrame();

        PresentationViewport viewport = renderer.Viewport;

        Console.WriteLine($"Logical target: {logicalRenderSize.Width}x{logicalRenderSize.Height}");
        Console.WriteLine($"Host framebuffer: {framebufferSize.Width}x{framebufferSize.Height}");
        Console.WriteLine($"Presentation viewport: {viewport.Width}x{viewport.Height} at ({viewport.OffsetX}, {viewport.OffsetY}), {viewport.Filter}");
    }
}
