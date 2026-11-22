using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Images;
using OpenConquer.Content.Login;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Login.Selection;

internal sealed class LoginServerPresentation : IDisposable
{
    private const int UnlistedStatusFrame = 3;

    private static readonly byte[] s_highlightFadeAlpha = [13, 74, 135, 195];

    private readonly ResourceOwnership _ownership = new();
    private readonly OpenGLGraphicsDevice _graphicsDevice;
    private readonly LoginStageLayout _layout;
    private readonly OpenGLTexture2D _unopened;
    private readonly OpenGLTexture2D _default;
    private readonly OpenGLTexture2D _highlight;
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
            _highlight = Create(LoginServerArtwork.Load(LoginServerArtwork.Layer.Highlight));
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
        int selected = selection.SelectedServerSlot;
        int rolledOver = animation.HoveredServerSlot != selected ? animation.HoveredServerSlot : -1;

        for (int slot = 0; slot < origins.Length; slot++)
        {
            LoginServer? server = selection.ServerAtSlot(slot);

            OpenGLTexture2D picture = server switch
            {
                null => _unopened,
                { IconId: { } icon } => Icon(icon),
                _ => _default,
            };
            LoginRect bounds = _layout.Offset(origins[slot], 0, 0, picture.Width, picture.Height);
            renderer.DrawSprite(picture, bounds.X, bounds.Y, bounds.Width, bounds.Height);

            if (server is null)
            {
                continue;
            }

            if (slot == rolledOver)
            {
                int step = animation.ServerHighlightStep;
                SpriteColor tint = step < s_highlightFadeAlpha.Length ? new SpriteColor(255, 255, 255, s_highlightFadeAlpha[step]) : SpriteColor.White;
                LoginRect highlight = _layout.Offset(origins[slot], -15, -18, _highlight.Width, _highlight.Height);
                renderer.DrawSprite(_highlight, highlight.X, highlight.Y, highlight.Width, highlight.Height, tint);
            }

            DrawFrame(renderer, _lights, LoginServerArtwork.Layer.Status, UnlistedStatusFrame, _layout.ServerLights[slot]);

            if (slot == selected)
            {
                DrawEffect(renderer, _selected, LoginServerArtwork.Layer.Selected, animation.ServerSelectedFrame, origins[slot], -26, -29);
            }
            else if (slot == rolledOver && animation.ServerHoverFrame >= 0)
            {
                DrawEffect(renderer, _hover, LoginServerArtwork.Layer.Hover, animation.ServerHoverFrame, origins[slot], -23, -9);
            }
        }
    }

    public void Dispose()
    {
        _ownership.Dispose();
    }

    private void DrawEffect(OpenGLRenderer renderer, OpenGLTexture2D atlas, LoginServerArtwork.Layer layer, int frame, LoginPoint origin, int offsetX, int offsetY)
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
        if (_icons.TryGetValue(iconId, out OpenGLTexture2D? texture))
        {
            return texture;
        }

        texture = Create(LoginServerIconArtwork.Load(iconId));
        _icons.Add(iconId, texture);

        return texture;
    }

    private OpenGLTexture2D Create(RgbaImage image)
    {
        return _ownership.Track(_graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span));
    }
}
