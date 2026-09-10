namespace OpenConquer.Content.Images;

/// <summary>
/// Loads retail DXT3 DDS image content into normalized RGBA pixels.
/// </summary>
public static class DdsImageLoader
{
    private const int MaximumEncodedLength = 16 * 1024 * 1024;

    public static RgbaImage Load(IClientContentSource contentSource, string contentPath, ContentLookupMode mode)
    {
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentPath);

        byte[] encodedImage = ContentRead.ReadRequiredBytes(contentSource, contentPath, mode, MaximumEncodedLength);

        return DdsImageReader.DecodeDxt3(encodedImage);
    }
}
