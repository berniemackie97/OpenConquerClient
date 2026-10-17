using OpenConquer.Content.Images;
using OpenConquer.Content.Login;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Login.Stage;

/// <summary>Root depth 2: start.dat sprite 22, the looping bird flight, from one atlas.</summary>
internal sealed class LoginBirdPresentation : IDisposable
{
    private readonly LoginStageLayout _layout;
    private readonly OpenGLTexture2D _atlas;

    public LoginBirdPresentation(OpenGLGraphicsDevice graphicsDevice, LoginStageLayout layout)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));

        RgbaImage atlas = LoginBirdArtwork.LoadAtlas();
        _atlas = graphicsDevice.CreateTexture2D(atlas.Width, atlas.Height, atlas.Pixels.Span);
    }

    /// <param name="renderer">The active renderer.</param>
    /// <param name="stageFrame">The stage clock's frame; sprite 22 has no stop action and loops.</param>
    public void Draw(OpenGLRenderer renderer, long stageFrame)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        LoginBirdArtwork.BirdFrame frame = LoginBirdArtwork.FrameAt((int)(stageFrame % LoginBirdArtwork.FrameCount));
        if (!frame.IsVisible)
        {
            return;
        }

        SpriteSourceRectangle source = new(frame.AtlasX, frame.AtlasY, frame.Width, frame.Height);
        renderer.DrawSprite(_atlas, source, _layout.Scale(frame.StageX), _layout.Scale(frame.StageY),
            Math.Max(1, _layout.Scale(frame.Width)), Math.Max(1, _layout.Scale(frame.Height)));
    }

    public void Dispose() => _atlas.Dispose();
}
