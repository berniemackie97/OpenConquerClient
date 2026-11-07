using System.Runtime.ExceptionServices;
using OpenConquer.Rendering.Text.Fonts.Discovery;
using OpenConquer.Rendering.Text.Fonts.FreeType;
using OpenConquer.Rendering.Text.Glyphs;

namespace OpenConquer.Rendering.Text.Fonts;

/// <summary>
/// Independently sized text faces prepared from one host font catalog. Each slot transfers once to a text context.
/// Disposing the batch releases any faces it still owns.
/// </summary>
public sealed class PreparedTextFonts : IDisposable
{
    private readonly int[] _heights;
    private readonly IGlyphRasterizer?[] _rasterizers;
    private int _disposed;

    private PreparedTextFonts(ReadOnlySpan<int> nominalPixelHeights)
    {
        _heights = nominalPixelHeights.ToArray();
        _rasterizers = new IGlyphRasterizer?[_heights.Length];
    }

    /// <summary>Discovers the host catalog and opens the requested faces without creating graphics resources.</summary>
    public static PreparedTextFonts Prepare(string? fontToken, bool antialiasEnabled,
        ReadOnlySpan<int> nominalPixelHeights, CancellationToken cancellationToken = default)
    {
        ValidateHeights(nominalPixelHeights);
        cancellationToken.ThrowIfCancellationRequested();

        using FreeTypeLibrary library = new();
        HostFontDiscovery discovery = new SystemHostFontSource().Discover();
        HostFontCatalog catalog = new(new FreeTypeFontInspector(library), discovery, cancellationToken);
        FreeTypeGlyphRasterizerFactory factory = new(library, new SystemFontResolver(catalog));

        // Each memory face retains its own mapping and lease on this library after preparation returns.
        return Build(nominalPixelHeights, height => factory.Create(fontToken, height, antialiasEnabled), cancellationToken);
    }

    internal static PreparedTextFonts Build(ReadOnlySpan<int> nominalPixelHeights,
        Func<int, IGlyphRasterizer> create, CancellationToken cancellationToken)
    {
        ValidateHeights(nominalPixelHeights);
        ArgumentNullException.ThrowIfNull(create);
        PreparedTextFonts batch = new(nominalPixelHeights);
        try
        {
            for (int index = 0; index < batch._heights.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                batch._rasterizers[index] = create(batch._heights[index]);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return batch;
        }
        catch
        {
            try
            {
                batch.Dispose();
            }
            catch
            {
                // Preserve the preparation failure after trying every acquired face.
            }

            throw;
        }
    }

    internal IGlyphRasterizer Take(int index, out int nominalPixelHeight)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _heights.Length);
        nominalPixelHeight = _heights[index];
        return Interlocked.Exchange(ref _rasterizers[index], null) ??
            throw new InvalidOperationException("The prepared font has already been taken.");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        ExceptionDispatchInfo? firstFailure = null;
        for (int index = 0; index < _rasterizers.Length; index++)
        {
            try
            {
                Interlocked.Exchange(ref _rasterizers[index], null)?.Dispose();
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        firstFailure?.Throw();
    }

    private static void ValidateHeights(ReadOnlySpan<int> nominalPixelHeights)
    {
        if (nominalPixelHeights.IsEmpty)
        {
            throw new ArgumentException("At least one font height is required.", nameof(nominalPixelHeights));
        }

        foreach (int height in nominalPixelHeights)
        {
            if (height == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nominalPixelHeights), "Nominal pixel heights cannot be zero.");
            }
        }
    }
}
