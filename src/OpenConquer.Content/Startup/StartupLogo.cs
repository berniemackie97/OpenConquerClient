using OpenConquer.Content.Configuration;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Startup;

/// <summary>
/// The one-shot retail startup logo selected for this launch.
/// </summary>
/// <remarks>
/// Mirrors <c>CStartupLogoDialog_OnInitDialogLoadLogoBrush</c> (<c>0x4B08B9</c>). Failure to
/// obtain a usable bitmap is non-fatal. The modern client preserves that behavioral contract while
/// safely rejecting malformed configuration instead of reproducing native printf undefined
/// behavior.
/// </remarks>
public sealed class StartupLogo
{
    private const int MaximumEncodedLength = 16 * 1024 * 1024;

    private StartupLogo(
        int variantIndex,
        string? contentPath,
        RgbaImage? image,
        string? unavailableReason
    )
    {
        VariantIndex = variantIndex;
        ContentPath = contentPath;
        Image = image;
        UnavailableReason = unavailableReason;
    }

    /// <summary>The selected retail variant, <c>1</c> or <c>2</c>.</summary>
    public int VariantIndex
    {
        get;
    }

    /// <summary>
    /// The resolved bitmap path, or <see langword="null"/> when configuration could not safely
    /// produce one.
    /// </summary>
    public string? ContentPath
    {
        get;
    }

    /// <summary>
    /// The decoded bitmap, or <see langword="null"/> when no usable startup image is available.
    /// </summary>
    public RgbaImage? Image
    {
        get;
    }

    /// <summary>
    /// Why <see cref="Image"/> is unavailable, or <see langword="null"/> when the bitmap loaded.
    /// </summary>
    public string? UnavailableReason
    {
        get;
    }

    /// <summary>
    /// Resolves and loads the optional startup logo for the supplied monotonic tick.
    /// </summary>
    public static StartupLogo Load(
        IClientContentSource contentSource,
        long monotonicTickMilliseconds
    )
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        // (timeGetTime() & 1) + 1 at 0x4B0A4F..0x4B0A68.
        int variantIndex = (int)((monotonicTickMilliseconds & 1L) + 1L);

        string contentPath;

        try
        {
            StartupLogoConfiguration configuration = StartupLogoConfiguration.LoadOrDefault(
                contentSource
            );

            contentPath = configuration.GetLogoPath(variantIndex);
        }
        catch (Exception exception) when (IsNonFatalLogoFailure(exception))
        {
            return new StartupLogo(
                variantIndex,
                contentPath: null,
                image: null,
                unavailableReason: $"Startup logo configuration could not be used: {exception.Message}"
            );
        }

        try
        {
            if (
                !contentSource.TryOpenRead(
                    contentPath,
                    ContentLookupMode.LooseOnly,
                    out Stream? stream
                )
            )
            {
                return new StartupLogo(
                    variantIndex,
                    contentPath,
                    image: null,
                    unavailableReason: $"'{contentPath}' was not found as a loose file."
                );
            }

            byte[] encodedImage;

            using (stream)
            {
                encodedImage = ContentRead.ReadBytes(stream, contentPath, MaximumEncodedLength);
            }

            return new StartupLogo(
                variantIndex,
                contentPath,
                WindowsBitmapReader.Decode24Bit(encodedImage),
                unavailableReason: null
            );
        }
        catch (Exception exception) when (IsNonFatalLogoFailure(exception))
        {
            return new StartupLogo(
                variantIndex,
                contentPath,
                image: null,
                unavailableReason: $"'{contentPath}' could not be loaded: {exception.Message}"
            );
        }
    }

    private static bool IsNonFatalLogoFailure(Exception exception)
    {
        return exception
            is ArgumentException
                or InvalidDataException
                or IOException
                or UnauthorizedAccessException;
    }
}
