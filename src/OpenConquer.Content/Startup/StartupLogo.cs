using OpenConquer.Content.Configuration;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Startup;

public sealed class StartupLogo
{
    private const int MaximumEncodedLength = 16 * 1024 * 1024;

    private StartupLogo(int variantIndex, string contentPath, RgbaImage image)
    {
        VariantIndex = variantIndex;
        ContentPath = contentPath;
        Image = image;
    }

    public int VariantIndex
    {
        get;
    }

    public string ContentPath
    {
        get;
    }

    public RgbaImage Image
    {
        get;
    }

    public static StartupLogo Load(IClientContentSource contentSource, long monotonicTickMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        StartupLogoConfiguration configuration = StartupLogoConfiguration.LoadOrDefault(contentSource);
        int variantIndex = (int)((monotonicTickMilliseconds & 1L) + 1L);
        string contentPath = configuration.GetLogoPath(variantIndex);
        byte[] encodedImage = ContentRead.ReadRequiredBytes(contentSource, contentPath, MaximumEncodedLength);
        RgbaImage image = WindowsBitmapReader.Decode24Bit(encodedImage);

        return new StartupLogo(variantIndex, contentPath, image);
    }
}
