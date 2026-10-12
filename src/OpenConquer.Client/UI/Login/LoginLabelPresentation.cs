using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Client.UI.Login;

/// <summary>Owns glyph resources and the bounded set of visible catalog labels for one OpenGL context.</summary>
internal sealed class LoginLabelPresentation : IDisposable
{
    private const int ReferenceWidth = 1024;
    private const int ReferenceFontHeight = 13;
    private const int ReferenceHorizontalPadding = 8;
    private static readonly SpriteColor s_labelColor = new(245, 230, 194, 255);
    private static readonly NativeTextRenderOptions s_labelStyle = new(NativeTextRenderStyle.Normal, s_labelColor, s_labelColor, 0, 0, NativeTextVertexColors.Solid(s_labelColor));
    private readonly OpenGLGraphicsDevice _graphicsDevice;
    private readonly LoginVisualLayout _layout;
    private readonly Label?[] _regionLabels = new Label?[LoginServerSelection.RegionsPerPage];
    private readonly Label?[] _serverLabels = new Label?[LoginServerSelection.ServersPerPage];
    private readonly int _fontHeight;
    private readonly int _padding;
    private OpenGLTextContext? _textContext;
    private LoginServerCatalog? _catalog;
    private LoginRegion? _region;
    private int _regionPage = -1;
    private int _serverPage = -1;
    private bool _disposed;

    public LoginLabelPresentation(OpenGLGraphicsDevice graphicsDevice, LoginVisualLayout layout)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(layout);

        _graphicsDevice = graphicsDevice;
        _layout = layout;
        _fontHeight = Math.Max(1, checked((int)((ReferenceFontHeight * (long)layout.Width + ReferenceWidth / 2) / ReferenceWidth)));
        _padding = Math.Max(1, checked((int)((ReferenceHorizontalPadding * (long)layout.Width + ReferenceWidth / 2) / ReferenceWidth)));
    }

    public void Draw(OpenGLRenderer renderer, LoginServerSelection selection)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(selection);

        if (selection.Catalog.Regions.Count == 0)
        {
            if (_catalog is not null)
            {
                ResetContext();
            }

            return;
        }

        if (_catalog is not null && !ReferenceEquals(_catalog, selection.Catalog))
        {
            ResetContext();
        }

        OpenGLTextContext text = _textContext ??= new OpenGLTextContext(_graphicsDevice, fontToken: null, nominalPixelHeight: _fontHeight, effectiveCodePage: 0, antialiasEnabled: true);

        if (!ReferenceEquals(_catalog, selection.Catalog) || _regionPage != selection.RegionPage ||
            !ReferenceEquals(_region, selection.SelectedRegion) || _serverPage != selection.ServerPage)
        {
            PrepareVisibleLabels(text, selection);
        }

        DrawLabels(text, renderer, _regionLabels);
        DrawLabels(text, renderer, _serverLabels);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ResetContext();
    }

    private void PrepareVisibleLabels(OpenGLTextContext text, LoginServerSelection selection)
    {
        Label?[] regions = new Label?[_regionLabels.Length];
        Label?[] servers = new Label?[_serverLabels.Length];
        ReadOnlySpan<LoginSpritePlacement> regionCells = _layout.RegionCells;
        ReadOnlySpan<LoginSpritePlacement> serverCells = _layout.ServerCells;

        for (int index = 0; index < regions.Length; index++)
        {
            if (selection.RegionAtSlot(index) is { } region)
            {
                regions[index] = PrepareLabel(text, region.DisplayName, regionCells[index]);
            }
        }

        for (int index = 0; index < servers.Length; index++)
        {
            if (selection.ServerAtSlot(index) is { } server)
            {
                servers[index] = PrepareLabel(text, server.DisplayName, serverCells[index]);
            }
        }

        regions.CopyTo(_regionLabels, 0);
        servers.CopyTo(_serverLabels, 0);
        _catalog = selection.Catalog;
        _region = selection.SelectedRegion;
        _regionPage = selection.RegionPage;
        _serverPage = selection.ServerPage;
    }

    private Label? PrepareLabel(OpenGLTextContext text, string name, LoginSpritePlacement cell)
    {
        if (LoginLabelFitter.HasMultiScalarTextElements(name))
        {
            return PrepareCandidate(text.LayoutUnicode("?"), cell);
        }

        OpenGLTextLayout complete = text.LayoutUnicode(name);

        if (complete.HasMissingGlyphs)
        {
            return PrepareCandidate(text.LayoutUnicode("?"), cell);
        }

        if (PrepareCandidate(complete, cell) is { } fitted)
        {
            return fitted;
        }

        bool original = true;

        foreach (string candidate in LoginLabelFitter.Candidates(name))
        {
            if (original)
            {
                original = false;
                continue;
            }

            OpenGLTextLayout layout = text.LayoutUnicode(candidate);

            if (!layout.HasMissingGlyphs && PrepareCandidate(layout, cell) is { } shortened)
            {
                return shortened;
            }
        }

        return null;
    }

    private Label? PrepareCandidate(OpenGLTextLayout layout, LoginSpritePlacement cell)
    {
        if (layout.HasMissingGlyphs || !layout.TryGetInkBounds(out int left, out int top, out int right, out int bottom) ||
            !LoginLabelFitter.TryCenter(cell, _padding, left, top, right, bottom, out int x, out int y))
        {
            return null;
        }

        return new Label(layout, x, y);
    }

    private static void DrawLabels(OpenGLTextContext text, OpenGLRenderer renderer, Label?[] labels)
    {
        foreach (Label? label in labels)
        {
            if (label is { } visible)
            {
                text.Draw(renderer, visible.Layout, s_labelStyle, visible.X, visible.Y);
            }
        }
    }

    private void ResetContext()
    {
        OpenGLTextContext? text = _textContext;
        _textContext = null;
        ClearLabels();
        text?.Dispose();
    }

    private void ClearLabels()
    {
        Array.Clear(_regionLabels);
        Array.Clear(_serverLabels);
        _catalog = null;
        _region = null;
        _regionPage = -1;
        _serverPage = -1;
    }

    private readonly record struct Label(OpenGLTextLayout Layout, int X, int Y);
}
