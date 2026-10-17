using System.Runtime.ExceptionServices;
using OpenConquer.Content.Images;
using OpenConquer.Content.Startup;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Login;

/// <summary>Owns the retail login stage textures and text for one active OpenGL context.</summary>
internal sealed class LoginPresentation : IDisposable
{
    // defaultR sprite 61 frames 1-5 fade sprite 60 in (alpha 26, 72, 118, 164, 210 of 256) and stop at frame 6.
    private static readonly byte[] s_flameFadeAlpha = [26, 72, 118, 164, 210];
    private readonly List<OpenGLTexture2D> _owned = [];
    private readonly OpenGLTexture2D _backdrop;
    private readonly OpenGLTexture2D _stageBorder;
    private readonly OpenGLTexture2D _regionPanel;
    private readonly OpenGLTexture2D _regionTile;
    private readonly OpenGLTexture2D _regionUnopened;
    private readonly OpenGLTexture2D _regionHover;
    private readonly OpenGLTexture2D _regionFlame;
    private readonly OpenGLTexture2D _regionPagePrevious;
    private readonly OpenGLTexture2D _regionPageNext;
    private readonly OpenGLTexture2D _serverUnopened;
    private readonly OpenGLTexture2D _serverDefault;
    private readonly OpenGLTexture2D _accountPanel;
    private readonly LoginVisualLayout _layout;
    private readonly LoginLabelPresentation _labels;
    private readonly LoginCredentialPresentation _credentials;
    private bool _disposed;

    public LoginPresentation(OpenGLGraphicsDevice graphicsDevice, LogicalRenderSize size, LoginArtworkSet artwork)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(artwork);

        if (artwork.Backdrop is not { Width: 1024, Height: 637 } || artwork.AccountPanel is not { Width: 508, Height: 98 })
        {
            throw new InvalidDataException("Login visual dimensions do not match the verified source artwork.");
        }

        _layout = LoginVisualLayout.Create(size);

        try
        {
            _backdrop = Create(graphicsDevice, artwork.Backdrop);
            _stageBorder = Create(graphicsDevice, LoginRetailStageArtwork.Load(LoginRetailStageArtwork.Layer.StageBorder));
            _regionPanel = Create(graphicsDevice, LoginRetailStageArtwork.Load(LoginRetailStageArtwork.Layer.RegionPanel));
            _regionTile = Create(graphicsDevice, LoginRetailStageArtwork.Load(LoginRetailStageArtwork.Layer.RegionTile));
            _regionUnopened = Create(graphicsDevice, LoginRetailStageArtwork.Load(LoginRetailStageArtwork.Layer.RegionUnopened));
            _regionHover = Create(graphicsDevice, LoginRetailStageArtwork.Load(LoginRetailStageArtwork.Layer.RegionHover));
            _regionFlame = Create(graphicsDevice, LoginRetailStageArtwork.Load(LoginRetailStageArtwork.Layer.RegionFlame));
            _regionPagePrevious = Create(graphicsDevice, LoginRetailStageArtwork.Load(LoginRetailStageArtwork.Layer.RegionPagePrevious));
            _regionPageNext = Create(graphicsDevice, LoginRetailStageArtwork.Load(LoginRetailStageArtwork.Layer.RegionPageNext));
            _serverUnopened = Create(graphicsDevice, LoginRetailRealmFallbackArtwork.LoadUnopened());
            _serverDefault = Create(graphicsDevice, LoginRetailRealmFallbackArtwork.LoadDefault());
            _accountPanel = Create(graphicsDevice, artwork.AccountPanel);
            _labels = new LoginLabelPresentation(graphicsDevice, _layout);
            _credentials = new LoginCredentialPresentation(graphicsDevice, _layout);
        }
        catch
        {
            // Preserve the initialization failure; a release failure here would only obscure it.
            _ = DisposeTextures();
            throw;
        }
    }

    /// <summary>Root depth 1: shape 11.</summary>
    public void DrawBackdrop(OpenGLRenderer renderer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        Draw(renderer, _backdrop, _layout.Backdrop);
    }

    /// <summary>Root depth 4, sprite 180: each <c>ss</c> holder's <c>son</c> movie for empty and unpictured realms.</summary>
    public void DrawServerMovies(OpenGLRenderer renderer, LoginServerSelection selection)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(selection);

        ReadOnlySpan<LoginStagePoint> holders = _layout.ServerHolders;
        for (int slot = 0; slot < holders.Length; slot++)
        {
            LoginServer? server = selection.ServerAtSlot(slot);
            // `ss.flush`: absent rows load unopenedS.swf, rows without `pic` load defaultS.swf.
            // Pictured rows are composited by LoginServerIconPresentation.
            OpenGLTexture2D? movie = server is null ? _serverUnopened : server.IconId is null ? _serverDefault : null;
            if (movie is not null)
            {
                Draw(renderer, movie, _layout.Offset(holders[slot], 0, 0, movie.Width, movie.Height));
            }
        }
    }

    /// <summary>Sprite 179 depth 10: each holder's <c>nameTxt</c>, above its selection effects.</summary>
    public void DrawServerLabels(OpenGLRenderer renderer, LoginServerSelection selection)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _labels.DrawServers(renderer, selection);
    }

    /// <summary>Root depth 138, sprite 259: panel, region movies with their rollover and selection frames, and paging.</summary>
    public void DrawRegionPanel(OpenGLRenderer renderer, LoginServerSelection selection, LoginSelectionInput input, LoginRetailSelectionMotion motion)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(motion);

        Draw(renderer, _regionPanel, _layout.RegionPanel);

        // A rolled-over holder is swapped to depth 999, above every other region movie.
        int raised = motion.RegionBorderSlot;
        ReadOnlySpan<LoginStagePoint> holders = _layout.RegionHolders;
        for (int slot = 0; slot < holders.Length; slot++)
        {
            if (slot != raised)
            {
                DrawRegionMovie(renderer, selection, motion, slot);
            }
        }

        if ((uint)raised < (uint)holders.Length)
        {
            DrawRegionMovie(renderer, selection, motion, raised);
        }

        _labels.DrawRegions(renderer, selection);

        if (selection.RegionPageCount > 1)
        {
            DrawPageButton(renderer, _regionPagePrevious, _layout.RegionPagePrevious, input.PageButtonState(LoginRegionPageButton.Previous));
            DrawPageButton(renderer, _regionPageNext, _layout.RegionPageNext, input.PageButtonState(LoginRegionPageButton.Next));
            _labels.DrawRegionPage(renderer, selection);
        }
    }

    /// <summary>Root depth 216: shape 400, above both selection panels and below <c>enterMC</c>.</summary>
    public void DrawStageBorder(OpenGLRenderer renderer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        Draw(renderer, _stageBorder, _layout.StageBorder);
    }

    /// <summary>Root depth 221 <c>enterMC</c>: shape 328 and the edit-text fields 363/364.</summary>
    public void DrawAccountPanel(OpenGLRenderer renderer, LoginCredentialInput credentials)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(credentials);
        Draw(renderer, _accountPanel, _layout.AccountPanel);
        _credentials.Draw(renderer, credentials);
    }

    public bool TryHitTestCredentialCaret(LoginCredentialInput input, int x, int y, out int index) =>
        _credentials.TryHitTest(input, x, y, out index);

    public int HitTestDraggedCredentialCaret(LoginCredentialInput input, int x) => _credentials.HitTestFocused(input, x);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _credentials.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _labels.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstFailure ??= DisposeTextures();
        firstFailure?.Throw();
    }

    private void DrawRegionMovie(OpenGLRenderer renderer, LoginServerSelection selection, LoginRetailSelectionMotion motion, int slot)
    {
        LoginStagePoint holder = _layout.RegionHolders[slot];

        // `sr.flush`: slots past the catalog load unopenedR.swf; regions without a picture load defaultR.swf.
        if (selection.RegionAtSlot(slot) is null)
        {
            Draw(renderer, _regionUnopened, _layout.Offset(holder, 0, 0, _regionUnopened.Width, _regionUnopened.Height));
            return;
        }

        Draw(renderer, _regionTile, _layout.RegionTiles[slot]);

        if (slot == selection.SelectedRegionSlot)
        {
            // Root frame 13 (gotoAndStop(16) clamps to the last frame): sprite 61 at depth 3.
            int step = motion.RegionFlameFadeStep;
            SpriteColor tint = step < s_flameFadeAlpha.Length ? new SpriteColor(255, 255, 255, s_flameFadeAlpha[step]) : SpriteColor.White;
            DrawFrame(renderer, _regionFlame, LoginRetailStageArtwork.Describe(LoginRetailStageArtwork.Layer.RegionFlame),
                motion.RegionFlameFrame, holder, 2, 0, tint);
        }
        else if (slot == motion.RegionBorderSlot)
        {
            DrawFrame(renderer, _regionHover, LoginRetailStageArtwork.Describe(LoginRetailStageArtwork.Layer.RegionHover),
                motion.RegionBorderFrame, holder, -10, -6, SpriteColor.White);
        }
    }

    private void DrawFrame(OpenGLRenderer renderer, OpenGLTexture2D atlas, LoginRetailStageArtwork.Sheet sheet, int frame,
        LoginStagePoint holder, int offsetX, int offsetY, SpriteColor tint)
    {
        if ((uint)frame >= (uint)sheet.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(frame));
        }

        SpriteSourceRectangle source = new((frame % sheet.Columns) * sheet.FrameWidth, (frame / sheet.Columns) * sheet.FrameHeight,
            sheet.FrameWidth, sheet.FrameHeight);
        LoginSpritePlacement target = _layout.Offset(holder, offsetX, offsetY, sheet.FrameWidth, sheet.FrameHeight);
        renderer.DrawSprite(atlas, source, target.X, target.Y, target.Width, target.Height, tint);
    }

    private static void DrawPageButton(OpenGLRenderer renderer, OpenGLTexture2D atlas, LoginSpritePlacement target, int state)
    {
        int frameWidth = atlas.Width / LoginRetailStageArtwork.ButtonStates;
        SpriteSourceRectangle source = new(state * frameWidth, 0, frameWidth, atlas.Height);
        renderer.DrawSprite(atlas, source, target.X, target.Y, target.Width, target.Height);
    }

    private static void Draw(OpenGLRenderer renderer, OpenGLTexture2D texture, LoginSpritePlacement placement) =>
        renderer.DrawSprite(texture, placement.X, placement.Y, placement.Width, placement.Height);

    private OpenGLTexture2D Create(OpenGLGraphicsDevice graphicsDevice, RgbaImage image)
    {
        OpenGLTexture2D texture = graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
        _owned.Add(texture);
        return texture;
    }

    private ExceptionDispatchInfo? DisposeTextures()
    {
        ExceptionDispatchInfo? firstFailure = null;
        for (int index = _owned.Count - 1; index >= 0; index--)
        {
            try
            {
                _owned[index].Dispose();
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        _owned.Clear();
        return firstFailure;
    }
}
