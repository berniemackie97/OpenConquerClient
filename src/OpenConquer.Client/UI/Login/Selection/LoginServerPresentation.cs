using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Images;
using OpenConquer.Content.Login;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Login.Selection;

/// <summary>
/// Root depth 4, sprite 180: each server tile's picture, status light, and rollover and selection effects.
/// Pictures are created on first use, at most one texture per bundled icon.
/// </summary>
internal sealed class LoginServerPresentation : IDisposable
{
    // flushLight shows sprite 34 frame `state` for states 1-4 and frame 4 for any other value. The catalog has no
    // status, which ServerListEntry_ResolveFlashStatusCode (0x5EA5A2) resolves to 0 for every server: the frame the
    // retail client shows once its status fetch has failed.
    private const int UnlistedStatusFrame = 3;

    private readonly ResourceOwnership _ownership = new();
    private readonly OpenGLGraphicsDevice _graphicsDevice;
    private readonly LoginStageLayout _layout;
    private readonly OpenGLTexture2D _unopened;
    private readonly OpenGLTexture2D _default;
    private readonly OpenGLTexture2D _hover;
    private readonly OpenGLTexture2D _selected;
    private readonly OpenGLTexture2D _lights;
    private readonly Dictionary<string, OpenGLTexture2D> _icons = new(StringComparer.Ordinal);

    public LoginServerPresentation(OpenGLGraphicsDevice graphicsDevice, LoginStageLayout layout)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));

        try
        {
            _unopened = Create(LoginServerArtwork.Load(LoginServerArtwork.Layer.Unopened));
            _default = Create(LoginServerArtwork.Load(LoginServerArtwork.Layer.Default));
            _hover = Create(LoginServerArtwork.Load(LoginServerArtwork.Layer.Hover));
            _selected = Create(LoginServerArtwork.Load(LoginServerArtwork.Layer.Selected));
            _lights = Create(LoginServerArtwork.Load(LoginServerArtwork.Layer.Status));
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

        ReadOnlySpan<LoginPoint> origins = _layout.ServerOrigins;
        for (int slot = 0; slot < origins.Length; slot++)
        {
            // ss.flush: an empty slot loads unopenedS.swf, a server without a picture defaultS.swf.
            OpenGLTexture2D picture = selection.ServerAtSlot(slot) switch
            {
                null => _unopened,
                { IconId: { } icon } => Icon(icon),
                _ => _default,
            };
            LoginRect bounds = _layout.Offset(origins[slot], 0, 0, picture.Width, picture.Height);
            renderer.DrawSprite(picture, bounds.X, bounds.Y, bounds.Width, bounds.Height);

            // Sprite 179 depth 6, above the picture and below the depth 8 effects; an empty slot hides it.
            if (selection.ServerAtSlot(slot) is not null)
            {
                DrawFrame(renderer, _lights, LoginServerArtwork.Layer.Status, UnlistedStatusFrame, _layout.ServerLights[slot]);
            }
        }

        int hovered = animation.HoveredServerSlot;
        if (hovered >= 0 && hovered != selection.SelectedServerSlot)
        {
            // Sprite 179 frame 6 nests sprite 83 at (-22.65, -9).
            DrawEffect(renderer, _hover, LoginServerArtwork.Layer.Hover, animation.ServerHoverFrame, origins[hovered], -23, -9);
        }

        int selected = selection.SelectedServerSlot;
        if (selected >= 0)
        {
            // Sprite 179 frame 12 nests sprite 178 at (-26, -29).
            DrawEffect(renderer, _selected, LoginServerArtwork.Layer.Selected, animation.ServerSelectedFrame, origins[selected], -26, -29);
        }
    }

    public void Dispose() => _ownership.Dispose();

    private void DrawEffect(OpenGLRenderer renderer, OpenGLTexture2D atlas, LoginServerArtwork.Layer layer, int frame, LoginPoint origin,
        int offsetX, int offsetY)
    {
        LoginArtworkSheet sheet = LoginServerArtwork.Describe(layer);
        DrawFrame(renderer, atlas, layer, frame, _layout.Offset(origin, offsetX, offsetY, sheet.FrameWidth, sheet.FrameHeight));
    }

    private static void DrawFrame(OpenGLRenderer renderer, OpenGLTexture2D atlas, LoginServerArtwork.Layer layer, int frame, LoginRect target)
    {
        LoginArtworkSheet sheet = LoginServerArtwork.Describe(layer);
        SpriteSourceRectangle source = new(sheet.FrameX(frame), sheet.FrameY(frame), sheet.FrameWidth, sheet.FrameHeight);
        renderer.DrawSprite(atlas, source, target.X, target.Y, target.Width, target.Height);
    }

    private OpenGLTexture2D Icon(string iconId)
    {
        if (!_icons.TryGetValue(iconId, out OpenGLTexture2D? texture))
        {
            texture = Create(LoginServerIconArtwork.Load(iconId));
            _icons.Add(iconId, texture);
        }

        return texture;
    }

    private OpenGLTexture2D Create(RgbaImage image) =>
        _ownership.Track(_graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span));
}
