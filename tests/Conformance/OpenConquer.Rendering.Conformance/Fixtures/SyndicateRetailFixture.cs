using OpenConquer.Content;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;
using OpenConquer.Rendering.Conformance.Support;

namespace OpenConquer.Rendering.Conformance.Fixtures;

internal static class SyndicateRetailFixture
{
    private const string AniContentPath = "ani/Common.Ani";
    private const string FrameContentPath = "data/pic/Syndicate.tga";
    private const string SectionName = "Syndicate";

    private const string ExpectedEncodedFrameSha256 = "a813875f120d20908e13c5cdb4410008d5ff1b6f2d6f9186051185f7aa331b3a";
    private const string ExpectedDecodedRgbaSha256 = "1e112db318ecd33cba4b2980d0ed92e502e74bcd0e6a539747733cad718f8c37";

    private const int ExpectedWidth = 14;
    private const int ExpectedHeight = 14;

    public static RgbaImage Load(PackagedClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        AniIndexSection section = AniIndexFile.Load(contentSource, AniContentPath, ContentLookupMode.LooseThenPackage).GetRequiredSection(SectionName);

        if (section.FrameCount != 1)
        {
            throw new InvalidDataException($"ANI section [{SectionName}] contains {section.FrameCount} frame(s); verified retail 5517 requires exactly one.");
        }

        if (!string.Equals(section.FramePaths[0], FrameContentPath, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"ANI section [{SectionName}] references '{section.FramePaths[0]}'; verified retail 5517 requires '{FrameContentPath}'.");
        }

        using (Stream frameStream = contentSource.OpenRequiredRead(FrameContentPath, ContentLookupMode.LooseThenPackage))
        {
            string encodedHash = ConformanceHash.Sha256(frameStream);

            if (!string.Equals(encodedHash, ExpectedEncodedFrameSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Retail frame '{FrameContentPath}' has SHA256 {encodedHash}; expected {ExpectedEncodedFrameSha256}.");
            }
        }

        RgbaImage image = AniFrameLoader.Load(contentSource, AniContentPath, SectionName, frameIndex: 0, ContentLookupMode.LooseThenPackage);

        if (image.Width != ExpectedWidth || image.Height != ExpectedHeight)
        {
            throw new InvalidDataException($"Retail Syndicate frame decoded as {image.Width}x{image.Height}; expected {ExpectedWidth}x{ExpectedHeight}.");
        }

        string decodedHash = ConformanceHash.Sha256(image.Pixels.Span);

        if (!string.Equals(decodedHash, ExpectedDecodedRgbaSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Retail Syndicate frame decoded to RGBA SHA256 {decodedHash}; expected {ExpectedDecodedRgbaSha256}.");
        }

        return image;
    }
}
