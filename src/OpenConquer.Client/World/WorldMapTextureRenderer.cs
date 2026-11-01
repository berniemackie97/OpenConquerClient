using System.Runtime.ExceptionServices;
using OpenConquer.Content;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.World;

internal sealed class WorldMapTextureRenderer : IDisposable
{
    private readonly IClientContentSource _source;
    private readonly OpenGLGraphicsDevice _device;
    private readonly Dictionary<string, OpenGLTexture2D> _textures = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public WorldMapTextureRenderer(IClientContentSource source, OpenGLGraphicsDevice device)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _device = device ?? throw new ArgumentNullException(nameof(device));
    }

    public void Prepare(IReadOnlyList<WorldPuzzleTile> tiles)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(tiles);
        foreach (WorldPuzzleTile tile in tiles)
        {
            EnsureTexture(tile.Animation, tile.FrameIndex);
        }
    }

    public void Prepare(IReadOnlyList<WorldTerrainSprite> sprites)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(sprites);
        foreach (WorldTerrainSprite sprite in sprites)
        {
            EnsureTexture(sprite.Animation, sprite.FrameIndex);
        }
    }

    public void Draw(OpenGLRenderer renderer, IReadOnlyList<WorldPuzzleTile> tiles, SpriteColor worldColor)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(tiles);
        foreach (WorldPuzzleTile tile in tiles)
        {
            string path = tile.Animation.FramePaths[tile.FrameIndex];
            renderer.DrawSprite(GetPrepared(path), tile.X, tile.Y, tile.Size, tile.Size, worldColor);
        }
    }

    public void Draw(OpenGLRenderer renderer, IReadOnlyList<WorldTerrainSprite> sprites,
        WorldPuzzleProjection projection, SpriteColor worldColor)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(sprites);
        foreach (WorldTerrainSprite sprite in sprites)
        {
            string path = sprite.Animation.FramePaths[sprite.FrameIndex];
            OpenGLTexture2D texture = GetPrepared(path);
            int width = checked((int)((long)texture.Width * sprite.ScaleNumerator / sprite.ScaleDenominator));
            int height = checked((int)((long)texture.Height * sprite.ScaleNumerator / sprite.ScaleDenominator));
            if (sprite.IntersectsViewport(width, height, projection.ScreenWidth, projection.ScreenHeight))
            {
                renderer.DrawSprite(texture, sprite.X, sprite.Y, width, height, worldColor);
            }
        }
    }

    private void EnsureTexture(AniIndexSection animation, int frameIndex)
    {
        string path = animation.FramePaths[frameIndex];
        if (_textures.ContainsKey(path))
        {
            return;
        }

        RgbaImage frame = AniFrameLoader.LoadWrappedFrame(_source, animation,
            (uint)frameIndex, ContentLookupMode.LooseThenPackage);
        OpenGLTexture2D texture = _device.CreateTexture2D(frame.Width, frame.Height, frame.Pixels.Span);
        try
        {
            _textures.Add(path, texture);
        }
        catch
        {
            texture.Dispose();
            throw;
        }
    }

    private OpenGLTexture2D GetPrepared(string path) =>
        _textures.TryGetValue(path, out OpenGLTexture2D? texture)
            ? texture : throw new InvalidOperationException($"World frame '{path}' has not been prepared.");

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ExceptionDispatchInfo? firstFailure = null;
        foreach (OpenGLTexture2D texture in _textures.Values)
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

        _textures.Clear();
        _disposed = true;
        firstFailure?.Throw();
    }
}
