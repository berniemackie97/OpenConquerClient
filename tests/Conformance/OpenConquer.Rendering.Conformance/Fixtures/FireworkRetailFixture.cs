using OpenConquer.Content;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;
using OpenConquer.Rendering.Conformance.Reference;
using OpenConquer.Rendering.Conformance.Support;

namespace OpenConquer.Rendering.Conformance.Fixtures;

internal static class FireworkRetailFixture
{
    private const string AniContentPath = "ani/weather.ani";
    private const string FrameContentPath = "data/firework/yinfa1/1.dds";
    private const string SectionName = "YinFa1";

    private const string ExpectedEncodedFrameSha256 = "1a79bb1faf0c94b759d723a18b9ef6908e38a0c7f5e600ceadb3675ecb7ea2df";

    private const int ExpectedFrameCount = 9;
    private const int ExpectedEncodedLength = 384;
    private const int ExpectedWidth = 16;
    private const int ExpectedHeight = 16;

    public static RgbaImage Load(PackagedClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        AniIndexSection section = AniIndexFile.Load(contentSource, AniContentPath, ContentLookupMode.LooseThenPackage).GetRequiredSection(SectionName);

        if (section.FrameCount != ExpectedFrameCount)
        {
            throw new InvalidDataException($"ANI section [{SectionName}] contains {section.FrameCount} frame(s); verified retail 5517 requires {ExpectedFrameCount}.");
        }

        if (!string.Equals(section.FramePaths[0], FrameContentPath, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"ANI section [{SectionName}] frame 0 references '{section.FramePaths[0]}'; verified retail 5517 requires '{FrameContentPath}'.");
        }

        byte[] encodedFrame = ReadVerifiedEncodedFrame(contentSource);
        byte[] referencePixels = Dxt3ReferenceDecoder.Decode(encodedFrame, ExpectedWidth, ExpectedHeight);
        RgbaImage image = AniFrameLoader.Load(contentSource, AniContentPath, SectionName, frameIndex: 0, ContentLookupMode.LooseThenPackage);

        if (image.Width != ExpectedWidth || image.Height != ExpectedHeight)
        {
            throw new InvalidDataException($"Retail firework frame decoded as {image.Width}x{image.Height}; expected {ExpectedWidth}x{ExpectedHeight}.");
        }

        if (!image.Pixels.Span.SequenceEqual(referencePixels))
        {
            string expectedHash = ConformanceHash.Sha256(referencePixels);
            string actualHash = ConformanceHash.Sha256(image.Pixels.Span);

            throw new InvalidDataException($"Retail firework frame decoded to RGBA SHA256 {actualHash}; independent DXT3 reference decoder produced {expectedHash}.");
        }

        return image;
    }

    private static byte[] ReadVerifiedEncodedFrame(PackagedClientContentSource contentSource)
    {
        if (contentSource.TryOpenRead(FrameContentPath, ContentLookupMode.LooseOnly, out Stream? looseStream))
        {
            looseStream.Dispose();
            throw new InvalidDataException($"Retail firework frame '{FrameContentPath}' unexpectedly resolves as a loose file; package-backed conformance requires the verified data.wdf entry.");
        }

        using Stream stream = contentSource.OpenRequiredRead(FrameContentPath, ContentLookupMode.PackageOnly);

        byte[] encodedFrame = new byte[ExpectedEncodedLength];

        try
        {
            stream.ReadExactly(encodedFrame);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException($"Retail firework frame '{FrameContentPath}' is shorter than the verified {ExpectedEncodedLength}-byte payload.", exception);
        }

        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException($"Retail firework frame '{FrameContentPath}' is longer than the verified {ExpectedEncodedLength}-byte payload.");
        }

        string encodedHash = ConformanceHash.Sha256(encodedFrame);

        if (!string.Equals(encodedHash, ExpectedEncodedFrameSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Retail firework frame '{FrameContentPath}' has SHA256 {encodedHash}; expected {ExpectedEncodedFrameSha256}.");
        }

        return encodedFrame;
    }
}
