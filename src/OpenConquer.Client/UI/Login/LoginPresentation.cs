using System.Runtime.ExceptionServices;
using OpenConquer.Content.Startup;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Login;

/// <summary>Owns pre-world login textures for one active OpenGL context.</summary>
internal sealed class LoginPresentation : IDisposable
{
    private readonly OpenGLTexture2D _backdrop;
    private readonly OpenGLTexture2D _header;
    private readonly OpenGLTexture2D _footer;
    private readonly OpenGLTexture2D _accountPanel;
    private readonly OpenGLTexture2D _topDragon;
    private readonly OpenGLTexture2D _regionCell;
    private readonly OpenGLTexture2D _serverCell;
    private readonly LoginVisualLayout _layout;
    private static readonly SpriteColor s_selectedBorder = new(218, 161, 52, 255);
    private static readonly SpriteColor s_focusBorder = new(255, 231, 135, 255);
    private bool _disposed;

    public LoginPresentation(OpenGLGraphicsDevice graphicsDevice, LogicalRenderSize size, LoginArtworkSet artwork)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(artwork);

        LoginVisualLayout layout = LoginVisualLayout.Create(size);

        if (artwork.Backdrop is not { Width: 1024, Height: 637 } ||
            artwork.Header is not { Width: 1024, Height: 116 } ||
            artwork.Footer is not { Width: 1024, Height: 140 } ||
            artwork.AccountPanel is not { Width: 508, Height: 98 } ||
            artwork.TopDragon is not { Width: 494, Height: 140 } ||
            artwork.RegionCell is not { Width: 154, Height: 54 } ||
            artwork.ServerCell is not { Width: 158, Height: 58 })
        {
            throw new InvalidDataException("Login visual dimensions do not match the verified source artwork.");
        }

        OpenGLTexture2D? backdrop = null;
        OpenGLTexture2D? header = null;
        OpenGLTexture2D? footer = null;
        OpenGLTexture2D? accountPanel = null;
        OpenGLTexture2D? topDragon = null;
        OpenGLTexture2D? regionCell = null;
        OpenGLTexture2D? serverCell = null;

        try
        {
            backdrop = graphicsDevice.CreateTexture2D(artwork.Backdrop.Width, artwork.Backdrop.Height, artwork.Backdrop.Pixels.Span);
            header = graphicsDevice.CreateTexture2D(artwork.Header.Width, artwork.Header.Height, artwork.Header.Pixels.Span);
            footer = graphicsDevice.CreateTexture2D(artwork.Footer.Width, artwork.Footer.Height, artwork.Footer.Pixels.Span);
            accountPanel = graphicsDevice.CreateTexture2D(artwork.AccountPanel.Width, artwork.AccountPanel.Height, artwork.AccountPanel.Pixels.Span);
            topDragon = graphicsDevice.CreateTexture2D(artwork.TopDragon.Width, artwork.TopDragon.Height, artwork.TopDragon.Pixels.Span);
            regionCell = graphicsDevice.CreateTexture2D(artwork.RegionCell.Width, artwork.RegionCell.Height, artwork.RegionCell.Pixels.Span);
            serverCell = graphicsDevice.CreateTexture2D(artwork.ServerCell.Width, artwork.ServerCell.Height, artwork.ServerCell.Pixels.Span);
        }
        catch
        {
            try
            {
                serverCell?.Dispose();
            }
            catch { }
            try
            {
                regionCell?.Dispose();
            }
            catch { }
            try
            {
                topDragon?.Dispose();
            }
            catch { }
            try
            {
                accountPanel?.Dispose();
            }
            catch { }
            try
            {
                footer?.Dispose();
            }
            catch { }
            try
            {
                header?.Dispose();
            }
            catch { }
            try
            {
                backdrop?.Dispose();
            }
            catch { }
            throw;
        }

        _backdrop = backdrop;
        _header = header;
        _footer = footer;
        _accountPanel = accountPanel;
        _topDragon = topDragon;
        _regionCell = regionCell;
        _serverCell = serverCell;
        _layout = layout;
    }

    public void Draw(OpenGLRenderer renderer, LoginServerSelection selection, LoginSelectionInput input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(input);

        DrawLayer(renderer, _backdrop, _layout.Backdrop);
        DrawLayer(renderer, _header, _layout.Header);
        DrawLayer(renderer, _footer, _layout.Footer);

        foreach (LoginSpritePlacement placement in _layout.RegionCells)
        {
            DrawLayer(renderer, _regionCell, placement);
        }

        foreach (LoginSpritePlacement placement in _layout.ServerCells)
        {
            DrawLayer(renderer, _serverCell, placement);
        }

        DrawLayer(renderer, _accountPanel, _layout.AccountPanel);
        DrawLayer(renderer, _topDragon, _layout.TopDragon);

        DrawSelectionBorder(renderer, _layout.RegionCells, selection.SelectedRegionSlot, s_selectedBorder);
        DrawSelectionBorder(renderer, _layout.ServerCells, selection.SelectedServerSlot, s_selectedBorder);

        if (input.Focus == LoginSelectionFocus.Region)
        {
            DrawSelectionBorder(renderer, _layout.RegionCells, input.FocusedRegionSlot, s_focusBorder);
        }
        else if (input.Focus == LoginSelectionFocus.Server)
        {
            DrawSelectionBorder(renderer, _layout.ServerCells, input.FocusedServerSlot, s_focusBorder);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ExceptionDispatchInfo? firstFailure = null;

        DisposeTexture(_serverCell, ref firstFailure);
        DisposeTexture(_regionCell, ref firstFailure);
        DisposeTexture(_topDragon, ref firstFailure);
        DisposeTexture(_accountPanel, ref firstFailure);
        DisposeTexture(_footer, ref firstFailure);
        DisposeTexture(_header, ref firstFailure);
        DisposeTexture(_backdrop, ref firstFailure);

        firstFailure?.Throw();
    }

    private static void DisposeTexture(OpenGLTexture2D texture, ref ExceptionDispatchInfo? firstFailure)
    {
        try
        {
            texture.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }
    }

    private static void DrawSelectionBorder(OpenGLRenderer renderer, ReadOnlySpan<LoginSpritePlacement> placements, int slot, SpriteColor color)
    {
        if ((uint)slot >= (uint)placements.Length)
        {
            return;
        }

        LoginSpritePlacement cell = placements[slot];
        renderer.DrawLineRectangle(cell.X, cell.Y, cell.X + cell.Width - 1, cell.Y + cell.Height - 1, color);
    }

    private static void DrawLayer(OpenGLRenderer renderer, OpenGLTexture2D texture, LoginSpritePlacement placement) =>
        renderer.DrawSprite(texture, placement.X, placement.Y, placement.Width, placement.Height);
}
