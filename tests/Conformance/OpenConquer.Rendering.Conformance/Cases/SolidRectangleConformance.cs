using OpenConquer.Platform.Geometry;
using OpenConquer.Rendering.Conformance.Support;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class SolidRectangleConformance
{
    public static void Run(OpenGLGraphicsDevice graphicsDevice, PixelSize framebufferSize)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        const int width = 5;
        const int height = 4;

        LogicalRenderSize logicalRenderSize = new(width, height);
        byte[] expected = FramebufferVerifier.CreateOpaqueBlack(width, height);

        FillRectangle(expected, width, x: 1, y: 1, rectangleWidth: 3, rectangleHeight: 2, new SpriteColor(byte.MaxValue, 0, 0, byte.MaxValue));
        FillRectangle(expected, width, x: 2, y: 2, rectangleWidth: 2, rectangleHeight: 1, new SpriteColor(0, byte.MaxValue, 0, byte.MaxValue));

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);

        renderer.BeginFrame();
        renderer.DrawSolidRectangle(x: 0, y: 0, width: 0, height: 4, new SpriteColor(0, 0, byte.MaxValue, byte.MaxValue));
        renderer.DrawSolidRectangle(x: 1, y: 1, width: 3, height: 2, new SpriteColor(byte.MaxValue, 0, 0, byte.MaxValue));
        renderer.DrawSolidRectangle(x: 2, y: 2, width: 2, height: 1, new SpriteColor(0, byte.MaxValue, 0, byte.MaxValue));
        byte[] actual = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        FramebufferVerifier.VerifyExact("Solid rectangle geometry, zero-width submission, and ordering", expected, actual);

        Console.WriteLine($"Solid rectangle geometry, zero-width submission, and ordering SHA256: {ConformanceHash.Sha256(actual)}");
    }

    private static void FillRectangle(byte[] framebuffer, int framebufferWidth, int x, int y, int rectangleWidth, int rectangleHeight, SpriteColor color)
    {
        for (int row = y; row < y + rectangleHeight; row++)
        {
            for (int column = x; column < x + rectangleWidth; column++)
            {
                int offset = checked((row * framebufferWidth + column) * 4);

                framebuffer[offset] = color.Red;
                framebuffer[offset + 1] = color.Green;
                framebuffer[offset + 2] = color.Blue;
                framebuffer[offset + 3] = byte.MaxValue;
            }
        }
    }
}
