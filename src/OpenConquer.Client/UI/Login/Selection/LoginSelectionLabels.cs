using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Client.UI.Login.Selection;

/// <summary>Glyph resources and the visible catalog labels for one OpenGL context.</summary>
internal sealed class LoginSelectionLabels : IDisposable
{
    // Edit text 35 (sprite 179 nameTxt) and 184 (sprite 259 pageTxt) use device Arial 14 / Arial Bold 20.
    private const string FieldFontFamily = "Arial";
    private const int ServerFontHeight = 14;
    private const int PageFontHeight = 20;
    // Region captions are a modern substitute for the text baked into each region movie.
    private const int RegionFontHeight = 13;
    private const int RegionHorizontalPadding = 8;
    private static readonly SpriteColor s_regionColor = new(73, 42, 26, 255);
    private static readonly SpriteColor s_selectedRegionColor = new(139, 45, 22, 255);
    private static readonly SpriteColor s_serverColor = new(0, 0, 0, 255);
    private static readonly SpriteColor s_pageColor = new(0x29, 0x39, 0x40, 255);
    private static readonly NativeTextRenderOptions s_regionStyle = Style(s_regionColor);
    private static readonly NativeTextRenderOptions s_selectedRegionStyle = Style(s_selectedRegionColor);
    private static readonly NativeTextRenderOptions s_serverStyle = Style(s_serverColor);
    private static readonly NativeTextRenderOptions s_pageStyle = Style(s_pageColor);
    private readonly OpenGLGraphicsDevice _graphicsDevice;
    private readonly LoginStageLayout _layout;
    private readonly Label?[] _regionLabels = new Label?[LoginServerSelection.RegionsPerPage];
    private readonly Label?[] _serverLabels = new Label?[LoginServerSelection.ServersPerPage];
    private OpenGLTextContext? _regionText;
    private OpenGLTextContext? _serverText;
    private OpenGLTextContext? _pageText;
    private LoginServerCatalog? _catalog;
    private LoginRegion? _region;
    private int _regionPage = -1;
    private Label? _pageLabel;
    private int _pageLabelPage = -1;
    private int _pageLabelCount = -1;
    private bool _disposed;

    public LoginSelectionLabels(OpenGLGraphicsDevice graphicsDevice, LoginStageLayout layout)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(layout);

        _graphicsDevice = graphicsDevice;
        _layout = layout;
    }

    public void DrawRegions(OpenGLRenderer renderer, LoginServerSelection selection)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(selection);

        if (!Prepare(selection))
        {
            return;
        }

        OpenGLTextContext text = _regionText!;
        for (int slot = 0; slot < _regionLabels.Length; slot++)
        {
            if (_regionLabels[slot] is { } label)
            {
                text.Draw(renderer, label.Layout, selection.SelectedRegionSlot == slot ? s_selectedRegionStyle : s_regionStyle, label.X, label.Y);
            }
        }
    }

    public void DrawServers(OpenGLRenderer renderer, LoginServerSelection selection)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(selection);

        if (!Prepare(selection))
        {
            return;
        }

        OpenGLTextContext text = _serverText!;
        ReadOnlySpan<LoginRect> fields = _layout.ServerLabels;
        for (int slot = 0; slot < _serverLabels.Length; slot++)
        {
            if (_serverLabels[slot] is { } visible)
            {
                // Edit text 35 neither autosizes nor wraps: a longer name is cut at the field box, gutter included.
                LoginRect field = fields[slot];
                int gutter = _layout.Scale(2);
                using OpenGLRenderer.ClipScope clip = renderer.Clip(field.X - gutter, field.Y - gutter, field.Width + 2 * gutter,
                    field.Height + 2 * gutter);
                text.Draw(renderer, visible.Layout, s_serverStyle, visible.X, visible.Y);
            }
        }
    }

    /// <summary>Sprite 259 <c>showPage()</c>: "page/count", centered in pageTxt.</summary>
    public void DrawRegionPage(OpenGLRenderer renderer, LoginServerSelection selection)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(selection);

        OpenGLTextContext text = _pageText ??= new OpenGLTextContext(_graphicsDevice, FieldFontFamily, Math.Max(1, _layout.Scale(PageFontHeight)),
            effectiveCodePage: 0, antialiasEnabled: true, bold: true);

        if (_pageLabel is null || _pageLabelPage != selection.RegionPage || _pageLabelCount != selection.RegionPageCount)
        {
            string value = $"{selection.RegionPage + 1}/{selection.RegionPageCount}";
            _pageLabel = PlaceInField(text.LayoutUnicode(value), _layout.RegionPageText);
            _pageLabelPage = selection.RegionPage;
            _pageLabelCount = selection.RegionPageCount;
        }

        if (_pageLabel is { } label)
        {
            text.Draw(renderer, label.Layout, s_pageStyle, label.X, label.Y);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ResetContexts();
        _pageText?.Dispose();
        _pageText = null;
    }

    private bool Prepare(LoginServerSelection selection)
    {
        if (selection.Catalog.Regions.Count == 0)
        {
            if (_catalog is not null)
            {
                ResetContexts();
            }

            return false;
        }

        if (_catalog is not null && !ReferenceEquals(_catalog, selection.Catalog))
        {
            ResetContexts();
        }

        _regionText ??= new OpenGLTextContext(_graphicsDevice, fontToken: null, nominalPixelHeight: Math.Max(1, _layout.Scale(RegionFontHeight)),
            effectiveCodePage: 0, antialiasEnabled: true);
        _serverText ??= new OpenGLTextContext(_graphicsDevice, FieldFontFamily, Math.Max(1, _layout.Scale(ServerFontHeight)),
            effectiveCodePage: 0, antialiasEnabled: true);

        if (!ReferenceEquals(_catalog, selection.Catalog) || _regionPage != selection.RegionPage || !ReferenceEquals(_region, selection.SelectedRegion))
        {
            PrepareVisibleLabels(selection);
        }

        return true;
    }

    private void PrepareVisibleLabels(LoginServerSelection selection)
    {
        Label?[] regions = new Label?[_regionLabels.Length];
        Label?[] servers = new Label?[_serverLabels.Length];
        int padding = Math.Max(1, _layout.Scale(RegionHorizontalPadding));

        for (int slot = 0; slot < regions.Length; slot++)
        {
            if (selection.RegionAtSlot(slot) is { } region)
            {
                LoginRect tile = _layout.RegionTiles[slot];
                regions[slot] = Fit(_regionText!, region.DisplayName, layout => CenterInk(layout, tile, padding));
            }
        }

        for (int slot = 0; slot < servers.Length; slot++)
        {
            if (selection.ServerAtSlot(slot) is { } server)
            {
                servers[slot] = PlaceInField(Layout(_serverText!, server.DisplayName), _layout.ServerLabels[slot]);
            }
        }

        regions.CopyTo(_regionLabels, 0);
        servers.CopyTo(_serverLabels, 0);
        _catalog = selection.Catalog;
        _region = selection.SelectedRegion;
        _regionPage = selection.RegionPage;
    }

    private static Label? Fit(OpenGLTextContext text, string name, Func<OpenGLTextLayout, Label?> place)
    {
        if (LoginLabelFitter.HasMultiScalarTextElements(name))
        {
            return place(text.LayoutUnicode("?"));
        }

        foreach (string candidate in LoginLabelFitter.Candidates(name))
        {
            OpenGLTextLayout layout = text.LayoutUnicode(candidate);

            if (layout.HasMissingGlyphs)
            {
                return place(text.LayoutUnicode("?"));
            }

            if (place(layout) is { } placed)
            {
                return placed;
            }
        }

        return null;
    }

    private static OpenGLTextLayout Layout(OpenGLTextContext text, string name)
    {
        if (LoginLabelFitter.HasMultiScalarTextElements(name))
        {
            return text.LayoutUnicode("?");
        }

        OpenGLTextLayout layout = text.LayoutUnicode(name);
        return layout.HasMissingGlyphs ? text.LayoutUnicode("?") : layout;
    }

    /// <summary>
    /// Flash center alignment: the line's advance width centered in the field's text area with its first line
    /// at the top; a line wider than the field starts at its left edge.
    /// </summary>
    private static Label? PlaceInField(OpenGLTextLayout layout, LoginRect field)
    {
        if (layout.HasMissingGlyphs)
        {
            return null;
        }

        return new Label(layout, checked(field.X + Math.Max(0, (field.Width - layout.WidthPixels) / 2)), field.Y);
    }

    private static Label? CenterInk(OpenGLTextLayout layout, LoginRect cell, int padding)
    {
        if (layout.HasMissingGlyphs || !layout.TryGetInkBounds(out int left, out int top, out int right, out int bottom) ||
            !LoginLabelFitter.TryCenter(cell, padding, left, top, right, bottom, out int x, out int y))
        {
            return null;
        }

        return new Label(layout, x, y);
    }

    private static NativeTextRenderOptions Style(SpriteColor color) => new(NativeTextRenderStyle.Normal, color, color, 0, 0, NativeTextVertexColors.Solid(color));

    private void ResetContexts()
    {
        OpenGLTextContext? regionText = _regionText;
        OpenGLTextContext? serverText = _serverText;
        _regionText = null;
        _serverText = null;
        Array.Clear(_regionLabels);
        Array.Clear(_serverLabels);
        _catalog = null;
        _region = null;
        _regionPage = -1;

        try
        {
            regionText?.Dispose();
        }
        finally
        {
            serverText?.Dispose();
        }
    }

    private readonly record struct Label(OpenGLTextLayout Layout, int X, int Y);
}
