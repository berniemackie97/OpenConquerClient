using OpenConquer.Content.Configuration;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Startup;

/// <summary>
/// The one-shot retail startup logo selected for this launch.
/// </summary>
/// <remarks>
/// Mirrors <c>CStartupLogoDialog_OnInitDialogLoadLogoBrush</c> (<c>0x4B08B9</c>). The bitmap may be
/// absent: retail stores a null <c>HBITMAP</c> at <c>0x4B0A8E</c> without checking it,
/// <c>CreatePatternBrush(NULL)</c> yields a null brush at <c>0x4B0AA2</c>, and the handler still
/// returns <c>TRUE</c> at <c>0x4B0AAB</c>. The dialog is created, shown, centered, and destroyed on
/// exactly the same schedule; only the background paint is missing.
/// </remarks>
public sealed class StartupLogo
{
    private const int MaximumEncodedLength = 16 * 1024 * 1024;

    private StartupLogo(int variantIndex, string contentPath, RgbaImage? image, string? unavailableReason)
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

    /// <summary>The resolved bitmap path, whether or not it could be loaded.</summary>
    public string ContentPath
    {
        get;
    }

    /// <summary>
    /// The decoded bitmap, or <see langword="null"/> when it could not be loaded.
    /// </summary>
    public RgbaImage? Image
    {
        get;
    }

    /// <summary>
    /// Why <see cref="Image"/> is <see langword="null"/>, or <see langword="null"/> when the bitmap
    /// loaded. Non-fatal, but reported so a broken content set is visible rather than silent.
    /// </summary>
    public string? UnavailableReason
    {
        get;
    }

    /// <summary>
    /// Resolves and loads the startup logo for the supplied monotonic tick.
    /// </summary>
    /// <remarks>
    /// The bitmap is read with <see cref="ContentLookupMode.LooseOnly"/> because retail loads it
    /// through <c>LoadImageA(..., LR_LOADFROMFILE)</c> at <c>0x4B0A88</c>, which is raw Win32 file
    /// I/O and never consults the package layer.
    /// </remarks>
    public static StartupLogo Load(IClientContentSource contentSource, long monotonicTickMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        StartupLogoConfiguration configuration = StartupLogoConfiguration.LoadOrDefault(contentSource);

        // (timeGetTime() & 1) + 1 at 0x4B0A4F..0x4B0A68.
        int variantIndex = (int)((monotonicTickMilliseconds & 1L) + 1L);
        string contentPath = configuration.GetLogoPath(variantIndex);

        if (!contentSource.TryOpenRead(contentPath, ContentLookupMode.LooseOnly, out Stream? stream))
        {
            return new StartupLogo(variantIndex, contentPath, image: null, $"'{contentPath}' was not found as a loose file.");
        }

        try
        {
            byte[] encodedImage;

            using (stream)
            {
                encodedImage = ContentRead.ReadBytes(stream, contentPath, MaximumEncodedLength);
            }

            return new StartupLogo(variantIndex, contentPath, WindowsBitmapReader.Decode24Bit(encodedImage), unavailableReason: null);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return new StartupLogo(variantIndex, contentPath, image: null, $"'{contentPath}' could not be loaded: {exception.Message}");
        }
    }
}
