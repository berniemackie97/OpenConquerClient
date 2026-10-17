using OpenConquer.Client.UI.Login.Controls;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Images;
using OpenConquer.Content.Login;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Login.Selection;

/// <summary>Root depth 138, sprite 259: the region panel, its sixteen tiles and its page controls.</summary>
internal sealed class LoginRegionPresentation : IDisposable
{
    // defaultR sprite 61 frames 1-5 carry alpha 26, 72, 118, 164 and 210 of 256; from frame 6 the flame is opaque.
    private static readonly byte[] s_flameFadeAlpha = [26, 72, 118, 164, 210];

    private readonly ResourceOwnership _ownership = new();
    private readonly LoginStageLayout _layout;
    private readonly OpenGLTexture2D _panel;
    private readonly OpenGLTexture2D[] _layers = new OpenGLTexture2D[4];

    public LoginRegionPresentation(OpenGLGraphicsDevice graphicsDevice, LoginStageLayout layout)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));

        try
        {
            _panel = Create(graphicsDevice, LoginStageArtwork.Load(LoginStageArtwork.Layer.RegionPanel));
            foreach (LoginRegionArtwork.Layer layer in Enum.GetValues<LoginRegionArtwork.Layer>())
            {
                _layers[(int)layer] = Create(graphicsDevice, LoginRegionArtwork.Load(layer));
            }
        }
        catch
        {
            _ownership.DisposeAfterInitializationFailure();
            throw;
        }
    }

    public void Draw(OpenGLRenderer renderer, LoginServerSelection selection, LoginSelectionAnimation animation)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(animation);

        LoginRect panel = _layout.RegionPanel;
        renderer.DrawSprite(_panel, panel.X, panel.Y, panel.Width, panel.Height);

        // A rolled-over tile is swapped to depth 999, above every other tile.
        int raised = animation.RegionRolloverSlot;
        for (int slot = 0; slot < _layout.RegionOrigins.Length; slot++)
        {
            if (slot != raised)
            {
                DrawTile(renderer, selection, animation, slot);
            }
        }

        if (raised >= 0)
        {
            DrawTile(renderer, selection, animation, raised);
        }
    }

    public void DrawPageButtons(OpenGLRenderer renderer, LoginSelectionInput input, LoginButtonPresentation buttons)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(buttons);

        buttons.Draw(renderer, LoginButton.RegionPagePrevious, input.PageButtonState(LoginButton.RegionPagePrevious), _layout.RegionPagePrevious);
        buttons.Draw(renderer, LoginButton.RegionPageNext, input.PageButtonState(LoginButton.RegionPageNext), _layout.RegionPageNext);
    }

    public void Dispose() => _ownership.Dispose();

    // sr.flush: slots past the catalog load unopenedR.swf; a region without a picture loads defaultR.swf.
    private void DrawTile(OpenGLRenderer renderer, LoginServerSelection selection, LoginSelectionAnimation animation, int slot)
    {
        if (selection.RegionAtSlot(slot) is null)
        {
            DrawLayer(renderer, LoginRegionArtwork.Layer.Unopened, 0, slot, SpriteColor.White);
            return;
        }

        DrawLayer(renderer, LoginRegionArtwork.Layer.Tile, 0, slot, SpriteColor.White);

        if (slot == selection.SelectedRegionSlot)
        {
            // gotoAndStop(16) clamps to the last timeline frame, 13: the flame.
            int fade = animation.RegionFlameFadeFrame;
            SpriteColor tint = fade < s_flameFadeAlpha.Length ? new SpriteColor(255, 255, 255, s_flameFadeAlpha[fade]) : SpriteColor.White;
            DrawLayer(renderer, LoginRegionArtwork.Layer.Flame, animation.RegionFlameFrame, slot, tint);
        }
        else if (slot == animation.RegionRolloverSlot)
        {
            DrawLayer(renderer, LoginRegionArtwork.Layer.Hover, animation.RegionRolloverFrame, slot, SpriteColor.White);
        }
    }

    private void DrawLayer(OpenGLRenderer renderer, LoginRegionArtwork.Layer layer, int frame, int slot, SpriteColor tint)
    {
        LoginArtworkSheet sheet = LoginRegionArtwork.Describe(layer);
        (int offsetX, int offsetY) = LoginRegionArtwork.Offset(layer);
        LoginRect target = _layout.Offset(_layout.RegionOrigins[slot], offsetX, offsetY, sheet.FrameWidth, sheet.FrameHeight);
        SpriteSourceRectangle source = new(sheet.FrameX(frame), sheet.FrameY(frame), sheet.FrameWidth, sheet.FrameHeight);
        renderer.DrawSprite(_layers[(int)layer], source, target.X, target.Y, target.Width, target.Height, tint);
    }

    private OpenGLTexture2D Create(OpenGLGraphicsDevice graphicsDevice, RgbaImage image) =>
        _ownership.Track(graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span));
}
