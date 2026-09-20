using OpenConquer.Platform;
using OpenConquer.Rendering.Conformance.Fixtures;
using OpenConquer.Rendering.Conformance.Support;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Atlas;
using OpenConquer.Rendering.Text.Layout;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class NativeTextConformance
{
    public static void Run(OpenGLGraphicsDevice graphicsDevice, PixelSize framebufferSize, string colorFormat)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentException.ThrowIfNullOrWhiteSpace(colorFormat);

        RunCoveragePlacementAndAlphaCase(graphicsDevice, framebufferSize, colorFormat);
        RunPerCornerDiagonalCase(graphicsDevice, framebufferSize, colorFormat);
        RunMultiPageBatchCase(graphicsDevice, framebufferSize, colorFormat);
        RunAtlasRevisionSynchronizationCase(graphicsDevice, framebufferSize, colorFormat);
    }

    private static void RunCoveragePlacementAndAlphaCase(OpenGLGraphicsDevice graphicsDevice, PixelSize framebufferSize, string colorFormat)
    {
        const int targetWidth = 7;
        const int targetHeight = 4;
        const int originX = 2;
        const int originY = 1;

        using NativeTextSyntheticFixture fixture = new();
        GlyphAtlasRegion region = fixture.AddGlyph(widthPixels: 3, heightPixels: 1, [byte.MaxValue, 128, 0]);

        NativeTextLayout layout = CreateSingleGlyphLayout(fixture.Source, region);
        NativeTextRenderOptions options = CreateNormalOptions(new SpriteColor(byte.MaxValue, byte.MaxValue, byte.MaxValue, 128));

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(new LogicalRenderSize(targetWidth, targetHeight), framebufferSize.Width, framebufferSize.Height);
        using OpenGLTextResource resource = graphicsDevice.CreateTextResource(fixture.Source);

        renderer.BeginFrame();
        renderer.DrawText(resource, layout, options, originX, originY);
        byte[] actual = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        byte[] expected = FramebufferVerifier.CreateOpaqueBlack(targetWidth, targetHeight);

        byte fullCoverageIntensity = MultiplyNormalized(128, byte.MaxValue);
        byte halfCoverageIntensity = MultiplyNormalized(128, 128);

        SetPixel(expected, targetWidth, originX, originY, fullCoverageIntensity, fullCoverageIntensity, fullCoverageIntensity, colorFormat);
        SetPixel(expected, targetWidth, originX + 1, originY, halfCoverageIntensity, halfCoverageIntensity, halfCoverageIntensity, colorFormat);

        FramebufferVerifier.VerifyExact("Native text coverage, placement, and alpha", expected, actual);

        Console.WriteLine($"Native text coverage/placement: 3x1 glyph at ({originX}, {originY}), coverage [255, 128, 0], text alpha 128, {colorFormat}");
        Console.WriteLine($"Native text coverage/placement SHA256: {ConformanceHash.Sha256(actual)}");
    }

    private static void RunPerCornerDiagonalCase(OpenGLGraphicsDevice graphicsDevice, PixelSize framebufferSize, string colorFormat)
    {
        const int targetWidth = 3;
        const int targetHeight = 3;
        const int originX = 1;
        const int originY = 1;

        using NativeTextSyntheticFixture fixture = new();
        GlyphAtlasRegion region = fixture.AddGlyph(widthPixels: 1, heightPixels: 1, [byte.MaxValue]);

        NativeTextLayout layout = CreateSingleGlyphLayout(fixture.Source, region);

        SpriteColor topLeft = new(0, 0, byte.MaxValue, byte.MaxValue);
        SpriteColor bottomLeft = new(40, 240, 0, byte.MaxValue);
        SpriteColor topRight = new(200, 20, 0, byte.MaxValue);
        SpriteColor bottomRight = SpriteColor.White;

        NativeTextVertexColors cornerColors = new(topLeft, bottomLeft, topRight, bottomRight);
        NativeTextRenderOptions options = new(
            NativeTextRenderStyle.PerCornerColor,
            SpriteColor.White,
            SpriteColor.White,
            cornerOffsetXPixels: 0,
            cornerOffsetYPixels: 0,
            cornerColors
        );

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(new LogicalRenderSize(targetWidth, targetHeight), framebufferSize.Width, framebufferSize.Height);
        using OpenGLTextResource resource = graphicsDevice.CreateTextResource(fixture.Source);

        renderer.BeginFrame();
        renderer.DrawText(resource, layout, options, originX, originY);
        byte[] actual = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        byte[] expected = FramebufferVerifier.CreateOpaqueBlack(targetWidth, targetHeight);

        // The sole fragment lies at the center of the native TL-BL-TR / BR-TR-BL
        // diagonal. Its color therefore interpolates exactly between TR and BL.
        byte expectedRed = (byte)((topRight.Red + bottomLeft.Red) / 2);
        byte expectedGreen = (byte)((topRight.Green + bottomLeft.Green) / 2);
        byte expectedBlue = (byte)((topRight.Blue + bottomLeft.Blue) / 2);

        SetPixel(expected, targetWidth, originX, originY, expectedRed, expectedGreen, expectedBlue, colorFormat);

        FramebufferVerifier.VerifyExact("Native text per-corner diagonal interpolation", expected, actual);

        Console.WriteLine($"Native text per-corner diagonal: center RGB ({expectedRed}, {expectedGreen}, {expectedBlue}), {colorFormat}");
        Console.WriteLine($"Native text per-corner diagonal SHA256: {ConformanceHash.Sha256(actual)}");
    }

    private static void RunMultiPageBatchCase(OpenGLGraphicsDevice graphicsDevice, PixelSize framebufferSize, string colorFormat)
    {
        const int targetWidth = 4;
        const int targetHeight = 2;
        const int pageZeroX = 0;
        const int pageOneX = 2;

        using NativeTextSyntheticFixture fixture = new();

        GlyphAtlasRegion pageZeroRegion = fixture.AddGlyph(widthPixels: 1, heightPixels: 1, [byte.MaxValue]);

        int fillerWidth = GlyphAtlasPage.SizePixels - GlyphAtlas.SeparationPixels - pageZeroRegion.WidthPixels;
        int fillerCoverageLength = checked(fillerWidth * GlyphAtlasPage.SizePixels);
        fixture.AddGlyph(fillerWidth, GlyphAtlasPage.SizePixels, new byte[fillerCoverageLength]);

        GlyphAtlasRegion pageOneRegion = fixture.AddGlyph(widthPixels: 1, heightPixels: 1, [byte.MaxValue]);

        if (pageZeroRegion.PageIndex != 0 || pageOneRegion.PageIndex != 1 || fixture.Atlas.Pages.Count != 2)
        {
            throw new InvalidDataException($"Synthetic multi-page fixture expected atlas pages 0 and 1, but received page indices {pageZeroRegion.PageIndex} and {pageOneRegion.PageIndex} across {fixture.Atlas.Pages.Count} page(s).");
        }

        NativeTextLayoutItem pageOneItem = NativeTextLayoutItem.CreateGlyph(glyphKey: 0x0042, xPixels: pageOneX, yPixels: 0, pageOneRegion);
        NativeTextLayoutItem pageZeroItem = NativeTextLayoutItem.CreateGlyph(glyphKey: 0x0041, xPixels: pageZeroX, yPixels: 0, pageZeroRegion);

        // Deliberately place page 1 first in layout order. The renderer's prepared
        // page batches still traverse stable ascending atlas-page identity.
        NativeTextLayout layout = new(fixture.Source, widthPixels: 3, heightPixels: 1, [pageOneItem, pageZeroItem]);
        NativeTextRenderOptions options = CreateNormalOptions(SpriteColor.White);

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(new LogicalRenderSize(targetWidth, targetHeight), framebufferSize.Width, framebufferSize.Height);
        using OpenGLTextResource resource = graphicsDevice.CreateTextResource(fixture.Source);

        renderer.BeginFrame();
        renderer.DrawText(resource, layout, options, x: 0, y: 0);
        byte[] actual = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        byte[] expected = FramebufferVerifier.CreateOpaqueBlack(targetWidth, targetHeight);
        SetPixel(expected, targetWidth, pageZeroX, y: 0, byte.MaxValue, byte.MaxValue, byte.MaxValue, colorFormat);
        SetPixel(expected, targetWidth, pageOneX, y: 0, byte.MaxValue, byte.MaxValue, byte.MaxValue, colorFormat);

        FramebufferVerifier.VerifyExact("Native text multi-page batching", expected, actual);

        Console.WriteLine($"Native text multi-page batching: pages {pageZeroRegion.PageIndex} and {pageOneRegion.PageIndex}, layout order [1, 0], {colorFormat}");
        Console.WriteLine($"Native text multi-page batching SHA256: {ConformanceHash.Sha256(actual)}");
    }

    private static void RunAtlasRevisionSynchronizationCase(OpenGLGraphicsDevice graphicsDevice, PixelSize framebufferSize, string colorFormat)
    {
        const int targetWidth = 4;
        const int targetHeight = 2;
        const int visibleX = 1;
        const int visibleY = 0;

        using NativeTextSyntheticFixture fixture = new();

        GlyphAtlasRegion initialRegion = fixture.AddGlyph(widthPixels: 1, heightPixels: 1, [0]);
        NativeTextLayout initialLayout = CreateSingleGlyphLayout(fixture.Source, initialRegion);
        NativeTextRenderOptions options = CreateNormalOptions(SpriteColor.White);

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(new LogicalRenderSize(targetWidth, targetHeight), framebufferSize.Width, framebufferSize.Height);
        using OpenGLTextResource resource = graphicsDevice.CreateTextResource(fixture.Source);

        renderer.BeginFrame();
        renderer.DrawText(resource, initialLayout, options, x: 0, y: 0);
        byte[] initialActual = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        byte[] initialExpected = FramebufferVerifier.CreateOpaqueBlack(targetWidth, targetHeight);
        FramebufferVerifier.VerifyExact("Native text initial atlas upload", initialExpected, initialActual);

        GlyphAtlasRegion revisedRegion = fixture.AddGlyph(widthPixels: 1, heightPixels: 1, [byte.MaxValue]);

        if (revisedRegion.PageIndex != initialRegion.PageIndex)
        {
            throw new InvalidDataException("Synthetic atlas revision fixture unexpectedly allocated the revised glyph on a different page.");
        }

        NativeTextLayout revisedLayout = CreateSingleGlyphLayout(fixture.Source, revisedRegion);

        renderer.BeginFrame();
        renderer.DrawText(resource, revisedLayout, options, visibleX, visibleY);
        byte[] revisedActual = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        byte[] revisedExpected = FramebufferVerifier.CreateOpaqueBlack(targetWidth, targetHeight);
        SetPixel(revisedExpected, targetWidth, visibleX, visibleY, byte.MaxValue, byte.MaxValue, byte.MaxValue, colorFormat);

        FramebufferVerifier.VerifyExact("Native text atlas revision synchronization", revisedExpected, revisedActual);

        Console.WriteLine($"Native text atlas revision synchronization: page {revisedRegion.PageIndex}, revision {fixture.Atlas.Pages[revisedRegion.PageIndex].Revision}, {colorFormat}");
        Console.WriteLine($"Native text atlas revision synchronization SHA256: {ConformanceHash.Sha256(revisedActual)}");
    }

    private static NativeTextLayout CreateSingleGlyphLayout(NativeTextLayoutSource source, GlyphAtlasRegion region)
    {
        NativeTextLayoutItem item = NativeTextLayoutItem.CreateGlyph(glyphKey: 0x0041, xPixels: 0, yPixels: 0, region);
        return new NativeTextLayout(source, region.WidthPixels, region.HeightPixels, [item]);
    }

    private static NativeTextRenderOptions CreateNormalOptions(SpriteColor color)
    {
        return new NativeTextRenderOptions(
            NativeTextRenderStyle.Normal,
            color,
            SpriteColor.White,
            cornerOffsetXPixels: 0,
            cornerOffsetYPixels: 0,
            NativeTextVertexColors.Solid(color)
        );
    }

    private static byte MultiplyNormalized(byte left, byte right)
    {
        return (byte)(((left * right) + 127) / byte.MaxValue);
    }

    private static void SetPixel(byte[] framebuffer, int width, int x, int y, byte red, byte green, byte blue, string colorFormat)
    {
        ArgumentNullException.ThrowIfNull(framebuffer);

        (int redBits, int greenBits, int blueBits) = colorFormat switch
        {
            "RGB565" => (5, 6, 5),
            "RGB555" => (5, 5, 5),
            _ => throw new ArgumentOutOfRangeException(nameof(colorFormat), colorFormat, "Unknown logical color format."),
        };

        int offset = checked(((y * width) + x) * 4);

        framebuffer[offset] = QuantizeNormalizedChannel(red, redBits);
        framebuffer[offset + 1] = QuantizeNormalizedChannel(green, greenBits);
        framebuffer[offset + 2] = QuantizeNormalizedChannel(blue, blueBits);
        framebuffer[offset + 3] = byte.MaxValue;
    }

    private static byte QuantizeNormalizedChannel(byte value, int bits)
    {
        int maximumEncodedValue = (1 << bits) - 1;
        int encoded = ((value * maximumEncodedValue) + 127) / byte.MaxValue;
        return (byte)(((encoded * byte.MaxValue) + (maximumEncodedValue / 2)) / maximumEncodedValue);
    }
}
