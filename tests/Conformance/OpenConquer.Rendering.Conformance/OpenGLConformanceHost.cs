using OpenConquer.Platform;
using OpenConquer.Rendering.OpenGL;

namespace OpenConquer.Rendering.Conformance;

internal static class OpenGLConformanceHost
{
    public static void Run(Action<OpenGLGraphicsDevice, PixelSize> renderFrame)
    {
        ArgumentNullException.ThrowIfNull(renderFrame);

        OpenGLGraphicsDevice? graphicsDevice = null;
        bool contextReady = false;
        bool frameRendered = false;
        bool contextReleased = false;

        {
            using StartupWindow window = new(new PixelSize(1280, 720));

            window.OpenGLContextReady += context =>
            {
                if (contextReady)
                {
                    throw new InvalidOperationException("The OpenGL context-ready callback was raised more than once.");
                }

                graphicsDevice = new OpenGLGraphicsDevice(context.GetProcAddress);
                contextReady = true;

                Console.WriteLine($"OpenGL version: {graphicsDevice.Version}");
                Console.WriteLine($"GLSL version: {graphicsDevice.ShadingLanguageVersion}");
                Console.WriteLine($"OpenGL vendor: {graphicsDevice.Vendor}");
                Console.WriteLine($"OpenGL renderer: {graphicsDevice.Renderer}");
            };

            window.Rendering += metrics =>
            {
                if (!contextReady || graphicsDevice is null)
                {
                    throw new InvalidOperationException("Rendering began before the OpenGL graphics device was ready.");
                }

                if (frameRendered)
                {
                    throw new InvalidOperationException("The conformance window rendered more than one frame.");
                }

                PixelSize framebufferSize = metrics.FramebufferSize;

                if (framebufferSize.Width <= 0 || framebufferSize.Height <= 0)
                {
                    throw new InvalidOperationException($"The native window reported an invalid framebuffer size of {framebufferSize.Width}x{framebufferSize.Height}.");
                }

                renderFrame(graphicsDevice, framebufferSize);
                frameRendered = true;
            };

            window.OpenGLContextReleasing += () =>
            {
                if (!contextReady)
                {
                    throw new InvalidOperationException("The OpenGL context began releasing before it became ready.");
                }

                if (contextReleased)
                {
                    throw new InvalidOperationException("The OpenGL context-release callback was raised more than once.");
                }

                graphicsDevice?.Dispose();
                graphicsDevice = null;
                contextReleased = true;
            };

            window.ShowAndRender();

            if (!frameRendered)
            {
                throw new InvalidOperationException("The production OpenGL renderer did not render the conformance cases.");
            }
        }

        if (!contextReleased)
        {
            throw new InvalidOperationException("The OpenGL context was not released through the production lifetime boundary.");
        }
    }
}
