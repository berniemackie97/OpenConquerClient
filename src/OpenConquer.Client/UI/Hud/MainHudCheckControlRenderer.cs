using System.Runtime.ExceptionServices;
using OpenConquer.Content.Images;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Hud;

internal sealed class MainHudCheckControlRenderer : IDisposable
{
    private readonly MainHudCheckControlLayout _layout;
    private readonly Dictionary<MainHudCheckControlId, OpenGLTexture2D[]> _frames;
    private readonly List<OpenGLTexture2D> _ownedTextures;
    private bool _disposed;

    public MainHudCheckControlRenderer(OpenGLGraphicsDevice graphicsDevice, MainHudCheckControlAssets assets, LogicalRenderSize logicalRenderSize)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(assets);

        _layout = MainHudCheckControlLayout.Create(logicalRenderSize);

        Dictionary<MainHudCheckControlId, OpenGLTexture2D[]> frames = [];
        List<OpenGLTexture2D> ownedTextures = [];

        try
        {
            foreach (MainHudCheckControlDefinition definition in MainHudCheckControlDefinitions.NativeDrawOrder)
            {
                if (!assets.IsAvailable(definition.Id))
                {
                    continue;
                }

                frames.Add(definition.Id, CreateFrames(graphicsDevice, assets, definition.Id, ownedTextures));
            }

            _frames = frames;
            _ownedTextures = ownedTextures;
        }
        catch
        {
            DisposeCreatedTextures(ownedTextures);
            throw;
        }
    }

    public void Draw(OpenGLRenderer renderer, MainHudCheckControlsState state)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(state);

        foreach (MainHudCheckControlDefinition definition in MainHudCheckControlDefinitions.NativeDrawOrder)
        {
            if (!_frames.TryGetValue(definition.Id, out OpenGLTexture2D[]? frames))
            {
                continue;
            }

            MainHudCheckControlState control = state.GetControl(definition.Id);
            MainHudCheckControlBounds bounds = _layout.GetBounds(definition.Id);
            renderer.DrawSprite(frames[control.RenderFrame], bounds.X, bounds.Y);
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
        MainHudCheckControlAssets assets,
        MainHudCheckControlId id,
        List<OpenGLTexture2D> ownedTextures)
    {
        OpenGLTexture2D[] frames = new OpenGLTexture2D[MainHudCheckControlDefinitions.FrameCount];

        for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
        {
            RgbaImage image = assets.GetFrame(id, frameIndex)!;
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
