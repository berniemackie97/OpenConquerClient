using OpenConquer.Content;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Rendering.World;

public sealed class WorldMapTextureRenderer : IDisposable
{
    private readonly IClientContentSource? _source;
    private readonly WorldMapImageLoader? _stream;
    private readonly OpenGLGraphicsDevice _device;
    private const int StreamingTextureTarget = 512;
    private const long StreamingPixelByteTarget = 128 * 1024 * 1024;
    private readonly WorldMapTextureCache<OpenGLTexture2D> _textures;
    private bool _disposed;

    public WorldMapTextureRenderer(IClientContentSource source, OpenGLGraphicsDevice device,
        int maximumTextures = int.MaxValue, long maximumPixelBytes = long.MaxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumTextures);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPixelBytes);
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _textures = new(maximumTextures, maximumPixelBytes, streaming: false);
    }

    /// <summary>Creates a context-owned texture cache fed by a separately owned map image loader.</summary>
    public static WorldMapTextureRenderer CreateStreaming(WorldMapImageLoader images, OpenGLGraphicsDevice device) =>
        new(images, device, StreamingTextureTarget, StreamingPixelByteTarget);

    internal WorldMapTextureRenderer(WorldMapImageLoader stream, OpenGLGraphicsDevice device,
        int maximumTextures, long maximumPixelBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumTextures);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPixelBytes);
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _textures = new(maximumTextures, maximumPixelBytes, streaming: true);
    }

    internal bool IsStreaming => _stream is not null;
    internal int ResidentTextureCount => _textures.Count;
    internal long ResidentPixelBytes => _textures.PixelBytes;

    internal void BeginPreparation()
    {
        _stream?.BeginPreparation();
        _textures.BeginPreparation();
    }

    internal void EndPreparation(bool complete)
    {
        if (_stream is not null)
        {
            _stream.EndPreparation();
            _textures.EndPreparation(complete);
        }
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

    internal bool Prepare(IReadOnlyList<WorldScenerySprite> sprites, ref int remaining)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(sprites);
        bool complete = true;
        foreach (WorldScenerySprite sprite in sprites)
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

    internal void Draw(OpenGLRenderer renderer, IReadOnlyList<WorldScenerySprite> sprites,
        WorldPuzzleProjection projection, SpriteColor worldColor) =>
        Draw(renderer, sprites, projection, worldColor, 0, sprites.Count);

    internal void Draw(OpenGLRenderer renderer, IReadOnlyList<WorldScenerySprite> sprites,
        WorldPuzzleProjection projection, SpriteColor worldColor, int start, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (start > sprites.Count || count > sprites.Count - start)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        for (int i = start; i < start + count; i++)
        {
            WorldScenerySprite sprite = sprites[i];
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
        if (_textures.Keep(path))
        {
            return true;
        }

        RgbaImage? frame;
        if (_stream is { } stream)
        {
            frame = stream.RequestImage(path, animation, frameIndex, remaining > 0);
            if (frame is null)
            {
                return false;
            }
        }
        else
        {
            if (remaining == 0)
            {
                return false;
            }

            IClientContentSource source = _source ?? throw new InvalidOperationException("Map content source is unavailable.");
            frame = AniFrameLoader.LoadWrappedFrame(source, animation, (uint)frameIndex, ContentLookupMode.LooseThenPackage);
        }

        if (_stream is null && !_textures.Fits(frame.Pixels.Length))
        {
            throw new InvalidDataException("The map preview exceeds its resident texture count or pixel-byte budget.");
        }
        OpenGLTexture2D texture = _device.CreateTexture2D(frame.Width, frame.Height, frame.Pixels.Span);
        try
        {
            _textures.Add(path, texture, frame.Pixels.Length);
            remaining--;
            return true;
        }
        catch
        {
            texture.Dispose();
            throw;
        }
    }

    internal bool TryGetFrameExtent(string path, out int width, out int height)
    {
        if (_textures.Find(path) is { } texture)
        {
            width = texture.Width;
            height = texture.Height;
            return true;
        }

        width = 0;
        height = 0;
        return false;
    }

    private OpenGLTexture2D GetPrepared(string path) =>
        _textures.Find(path) ?? throw new InvalidOperationException($"World frame '{path}' has not been prepared.");

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _textures.Dispose();
    }
}
