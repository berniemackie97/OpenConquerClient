namespace OpenConquer.Rendering.Text;

/// <summary>
/// Creates a glyph rasterizer using the native font-creation fallback chain.
/// </summary>
internal sealed class FreeTypeGlyphRasterizerFactory
{
    private const string SimSunCollectionToken = "$simsun.ttc";
    private const string SimSunFontToken = "$simsun.ttf";
    private const string CourierNewFamilyName = "Courier New";

    private readonly RasterizerCreator _creator;
    private readonly IFontResolver _fontResolver;

    public FreeTypeGlyphRasterizerFactory(FreeTypeLibrary library, IFontResolver fontResolver) : this(fontResolver, CreateRasterizerCreator(library))
    {
    }

    internal FreeTypeGlyphRasterizerFactory(IFontResolver fontResolver, RasterizerCreator creator)
    {
        ArgumentNullException.ThrowIfNull(fontResolver);
        ArgumentNullException.ThrowIfNull(creator);

        _fontResolver = fontResolver;
        _creator = creator;
    }

    public IGlyphRasterizer Create(string? fontToken, int nominalPixelHeight, bool antialiasEnabled)
    {
        if (nominalPixelHeight == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nominalPixelHeight), nominalPixelHeight, "Nominal pixel height cannot be zero.");
        }

        Exception? lastFailure = null;

        if (!string.IsNullOrEmpty(fontToken) && TryCreateFromToken(fontToken, nominalPixelHeight, antialiasEnabled, ref lastFailure) is { } requestedRasterizer)
        {
            return requestedRasterizer;
        }

        if (_fontResolver.TryResolveDefaultGuiFont(out ResolvedFont? defaultGuiFont) && defaultGuiFont is not null && TryCreate(defaultGuiFont, nominalPixelHeight, antialiasEnabled, ref lastFailure) is { } defaultGuiRasterizer)
        {
            return defaultGuiRasterizer;
        }

        if (TryCreateFromToken(SimSunCollectionToken, nominalPixelHeight, antialiasEnabled, ref lastFailure) is { } simSunCollectionRasterizer)
        {
            return simSunCollectionRasterizer;
        }

        if (TryCreateFromToken(SimSunFontToken, nominalPixelHeight, antialiasEnabled, ref lastFailure) is { } simSunRasterizer)
        {
            return simSunRasterizer;
        }

        if (TryCreateFromToken(CourierNewFamilyName, nominalPixelHeight, antialiasEnabled, ref lastFailure) is { } courierNewRasterizer)
        {
            return courierNewRasterizer;
        }

        throw new InvalidOperationException("No usable font could be created from the requested font or the native fallback chain.", lastFailure);
    }

    internal delegate IGlyphRasterizer RasterizerCreator(ResolvedFont font, int nominalPixelHeight, bool antialiasEnabled);

    private IGlyphRasterizer? TryCreateFromToken(string fontToken, int nominalPixelHeight, bool antialiasEnabled, ref Exception? lastFailure)
    {
        if (!_fontResolver.TryResolve(fontToken, out ResolvedFont? font) || font is null)
        {
            return null;
        }

        return TryCreate(font, nominalPixelHeight, antialiasEnabled, ref lastFailure);
    }

    private IGlyphRasterizer? TryCreate(ResolvedFont font, int nominalPixelHeight, bool antialiasEnabled, ref Exception? lastFailure)
    {
        try
        {
            return _creator(font, nominalPixelHeight, antialiasEnabled) ?? throw new InvalidOperationException("Glyph rasterizer creator returned null.");
        }
        catch (FontFaceCreationException exception)
        {
            lastFailure = exception;
        }
        catch (InvalidDataException exception)
        {
            lastFailure = exception;
        }
        catch (IOException exception)
        {
            lastFailure = exception;
        }
        catch (UnauthorizedAccessException exception)
        {
            lastFailure = exception;
        }

        return null;
    }

    private static RasterizerCreator CreateRasterizerCreator(FreeTypeLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);

        return (font, nominalPixelHeight, antialiasEnabled) => new FreeTypeGlyphRasterizer(library, font, nominalPixelHeight, antialiasEnabled);
    }
}
