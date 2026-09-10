using OpenConquer.Content;
using OpenConquer.Content.Images;
using OpenConquer.Rendering.Conformance.Cases;
using OpenConquer.Rendering.Conformance.Fixtures;
using OpenConquer.Rendering.Conformance.Support;

namespace OpenConquer.Rendering.Conformance;

internal static class ConformanceRunner
{
    public static void Run(ConformanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(
            options.ContentRoot
        );
        RgbaImage syndicateImage = SyndicateRetailFixture.Load(contentSource);

        OpenGLConformanceHost.Run(
            (graphicsDevice, framebufferSize) =>
            {
                PresentationConformance.Run(graphicsDevice, framebufferSize);

                SyndicateSpriteBaseline syndicateBaseline = SyndicateSpriteConformance.Run(
                    graphicsDevice,
                    syndicateImage,
                    framebufferSize
                );

                SpriteColorConformance.Run(
                    graphicsDevice,
                    syndicateImage,
                    framebufferSize,
                    syndicateBaseline
                );
                SpriteBlendConformance.Run(graphicsDevice, framebufferSize);
                SpriteGeometryConformance.Run(
                    graphicsDevice,
                    syndicateImage,
                    framebufferSize,
                    syndicateBaseline
                );
                SpriteRotationConformance.Run(
                    graphicsDevice,
                    framebufferSize,
                    syndicateBaseline.ColorFormat
                );
            }
        );

        Console.WriteLine(
            "OpenGL render-target, presentation, ANI asset, sprite geometry, sprite color, sprite blending, and sprite rotation conformance passed."
        );
    }
}
