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

        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(options.ContentRoot);
        RgbaImage syndicateImage = SyndicateRetailFixture.Load(contentSource);
        RgbaImage fireworkImage = FireworkRetailFixture.Load(contentSource);

        OpenGLConformanceHost.Run((graphicsDevice, framebufferSize) =>
        {
            PresentationConformance.Run(graphicsDevice, framebufferSize);

            SyndicateSpriteBaseline syndicateBaseline = SyndicateSpriteConformance.Run(graphicsDevice, syndicateImage, framebufferSize);

            SpriteColorConformance.Run(graphicsDevice, syndicateImage, framebufferSize, syndicateBaseline);
            SpriteBlendConformance.Run(graphicsDevice, framebufferSize);
            SolidRectangleConformance.Run(graphicsDevice, framebufferSize);
            FireworkDxt3Conformance.Run(graphicsDevice, fireworkImage, framebufferSize);
            SpriteGeometryConformance.Run(graphicsDevice, syndicateImage, framebufferSize, syndicateBaseline);
            SpriteRepeatedSamplingConformance.Run(graphicsDevice, framebufferSize);
            SpriteRotationConformance.Run(graphicsDevice, framebufferSize, syndicateBaseline.ColorFormat);
            NativeTextConformance.Run(graphicsDevice, framebufferSize, syndicateBaseline.ColorFormat);
            MainHudChromeConformance.Run(graphicsDevice, contentSource, framebufferSize, syndicateBaseline.ColorFormat);
            MainHudVitalsConformance.Run(graphicsDevice, contentSource, framebufferSize, syndicateBaseline.ColorFormat);
            MainHudSkillExperienceConformance.Run(graphicsDevice, contentSource, framebufferSize, syndicateBaseline.ColorFormat);
            MainHudActionButtonStripConformance.Run(graphicsDevice, contentSource, framebufferSize, syndicateBaseline.ColorFormat);
        });

        Console.WriteLine("OpenGL render-target, presentation, ANI asset, DXT3 DDS, sprite geometry, repeated sprite sampling, sprite color, sprite blending, solid rectangle, sprite rotation, native text, main HUD chrome, main HUD vitals, main HUD skill/experience, and main HUD action-strip conformance passed.");
    }
}
