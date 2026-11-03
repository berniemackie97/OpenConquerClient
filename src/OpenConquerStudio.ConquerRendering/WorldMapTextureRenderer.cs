using System.Runtime.ExceptionServices;
using OpenConquer.Content;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Sprites;

namespace OpenConquerStudio.ConquerRendering;

public sealed class WorldMapTextureRenderer : IDisposable
{
    private readonly IClientContentSource _source;
    private readonly OpenGLGraphicsDevice _device;
    private readonly Dictionary<string, OpenGLTexture2D> _textures = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;
    private readonly int _maximumTextures;
    private readonly long _maximumPixelBytes;
    private long _pixelBytes;

    public WorldMapTextureRenderer(IClientContentSource source, OpenGLGraphicsDevice device,
        int maximumTextures = int.MaxValue, long maximumPixelBytes = long.MaxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumTextures);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPixelBytes);
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _maximumTextures = maximumTextures;
        _maximumPixelBytes = maximumPixelBytes;
    }

    internal bool Prepare(IReadOnlyList<WorldPuzzleTile> tiles, ref int remaining)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(tiles);
        bool complete = true;
        foreach (WorldPuzzleTile tile in tiles)
        {
            if (!EnsureTexture(tile.Animation, tile.FrameIndex, ref remaining))
            {
                complete = false;
            }
        }

        return complete;
    }

    internal bool Prepare(IReadOnlyList<WorldTerrainSprite> sprites, ref int remaining)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(sprites);
        bool complete = true;
        foreach (WorldTerrainSprite sprite in sprites)
        {
            if (!EnsureTexture(sprite.Animation, sprite.FrameIndex, ref remaining))
            {
                complete = false;
            }
        }

        return complete;
    }

    internal void Draw(OpenGLRenderer renderer, IReadOnlyList<WorldPuzzleTile> tiles, SpriteColor worldColor)
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

    internal void Draw(OpenGLRenderer renderer, IReadOnlyList<WorldTerrainSprite> sprites,
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

    private bool EnsureTexture(AniIndexSection animation, int frameIndex, ref int remaining)
    {
        string path = animation.FramePaths[frameIndex];
        if (_textures.ContainsKey(path))
        {
            return true;
        }

        if (remaining == 0)
        {
            return false;
        }

        RgbaImage frame = AniFrameLoader.LoadWrappedFrame(_source, animation,
            (uint)frameIndex, ContentLookupMode.LooseThenPackage);
        if (_textures.Count >= _maximumTextures || frame.Pixels.Length > _maximumPixelBytes - _pixelBytes)
        {
            throw new InvalidDataException("The map preview exceeds its resident texture count or pixel-byte budget.");
        }
        OpenGLTexture2D texture = _device.CreateTexture2D(frame.Width, frame.Height, frame.Pixels.Span);
        try
        {
            _textures.Add(path, texture);
            _pixelBytes += frame.Pixels.Length;
            remaining--;
            return true;
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
