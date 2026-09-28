using System.Runtime.ExceptionServices;
using OpenConquer.Content.Images;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Hud;

internal sealed class MainHudActionButtonStripRenderer : IDisposable
{
    private readonly MainHudActionButtonLayout _layout;
    private readonly Dictionary<MainHudActionButtonId, OpenGLTexture2D[]> _buttonFrames;
    private readonly Dictionary<MainHudPkButtonSkin, OpenGLTexture2D[]> _pkFrames;
    private readonly List<OpenGLTexture2D> _ownedTextures;
    private bool _disposed;

    public MainHudActionButtonStripRenderer(
        OpenGLGraphicsDevice graphicsDevice,
        MainHudActionButtonAssets assets,
        LogicalRenderSize logicalRenderSize)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(assets);

        _layout = MainHudActionButtonLayout.Create(logicalRenderSize);

        Dictionary<MainHudActionButtonId, OpenGLTexture2D[]> buttonFrames = [];
        Dictionary<MainHudPkButtonSkin, OpenGLTexture2D[]> pkFrames = [];
        List<OpenGLTexture2D> ownedTextures = [];

        try
        {
            foreach (MainHudActionButtonDefinition definition in MainHudActionButtonDefinitions.NativeDrawOrder)
            {
                if (definition.Id == MainHudActionButtonId.Button47 || !assets.IsAvailable(definition.Id))
                {
                    continue;
                }

                buttonFrames.Add(definition.Id, CreateFrames(
                    graphicsDevice,
                    definition.ExpectedFrameCount,
                    frameIndex => assets.GetFrame(definition.Id, frameIndex)!,
                    ownedTextures));
            }

            foreach (MainHudPkButtonSkin skin in Enum.GetValues<MainHudPkButtonSkin>())
            {
                if (!assets.IsPkSkinAvailable(skin))
                {
                    continue;
                }

                pkFrames.Add(skin, CreateFrames(
                    graphicsDevice,
                    2,
                    frameIndex => assets.GetPkFrame(skin, frameIndex)!,
                    ownedTextures));
            }

            _buttonFrames = buttonFrames;
            _pkFrames = pkFrames;
            _ownedTextures = ownedTextures;
        }
        catch
        {
            DisposeCreatedTextures(ownedTextures);
            throw;
        }
    }

    public void Draw(OpenGLRenderer renderer, MainHudActionButtonStripState state)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(state);

        foreach (MainHudActionButtonDefinition definition in MainHudActionButtonDefinitions.NativeDrawOrder)
        {
            MainHudActionButtonState button = state.GetButton(definition.Id);
            OpenGLTexture2D[]? frames = definition.Id == MainHudActionButtonId.Button47
                ? _pkFrames.GetValueOrDefault(state.PkSkin)
                : _buttonFrames.GetValueOrDefault(definition.Id);

            if (frames is null)
            {
                continue;
            }

            OpenGLTexture2D texture = frames[button.RenderFrame % frames.Length];
            MainHudActionButtonBounds bounds = _layout.GetBounds(definition.Id);
            renderer.DrawSprite(texture, bounds.X, bounds.Y);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ExceptionDispatchInfo? firstFailure = null;

        for (int index = _ownedTextures.Count - 1; index >= 0; index--)
        {
            try
            {
                _ownedTextures[index].Dispose();
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        _disposed = true;
        firstFailure?.Throw();
    }

    private static OpenGLTexture2D[] CreateFrames(
        OpenGLGraphicsDevice graphicsDevice,
        int frameCount,
        Func<int, RgbaImage> getFrame,
        List<OpenGLTexture2D> ownedTextures)
    {
        OpenGLTexture2D[] frames = new OpenGLTexture2D[frameCount];

        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            RgbaImage image = getFrame(frameIndex);
            OpenGLTexture2D texture = graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
            frames[frameIndex] = texture;
            ownedTextures.Add(texture);
        }

        return frames;
    }

    private static void DisposeCreatedTextures(List<OpenGLTexture2D> textures)
    {
        for (int index = textures.Count - 1; index >= 0; index--)
        {
            try
            {
                textures[index].Dispose();
            }
            catch
            {
                // Preserve the texture-creation failure that initiated cleanup.
            }
        }
    }
}
