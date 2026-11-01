using System.Runtime.ExceptionServices;
using OpenConquer.Content;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.World;

/// <summary>Uploads visible retail puzzle frames on the graphics thread and reuses textures across frames.</summary>
internal sealed class WorldPuzzleTextureRenderer : IDisposable
{
    private readonly IClientContentSource _source;
    private readonly OpenGLGraphicsDevice _device;
    private readonly Dictionary<string, OpenGLTexture2D> _textures = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public WorldPuzzleTextureRenderer(IClientContentSource source, OpenGLGraphicsDevice device)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _device = device ?? throw new ArgumentNullException(nameof(device));
    }

    /// <summary>Resolve and upload every required tile before starting an OpenGL frame.</summary>
    public void Prepare(IReadOnlyList<WorldPuzzleTile> tiles)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(tiles);

        foreach (WorldPuzzleTile tile in tiles)
        {
            string path = tile.Animation.FramePaths[tile.FrameIndex];
            if (_textures.ContainsKey(path))
            {
                continue;
            }

            RgbaImage frame = AniFrameLoader.LoadWrappedFrame(_source, tile.Animation,
                (uint)tile.FrameIndex, ContentLookupMode.LooseThenPackage);
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
    }

    /// <summary>Issues only GPU draws; I/O and texture uploads must have completed in Prepare.</summary>
    public void Draw(OpenGLRenderer renderer, IReadOnlyList<WorldPuzzleTile> tiles, SpriteColor worldColor)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(tiles);

        foreach (WorldPuzzleTile tile in tiles)
        {
            string path = tile.Animation.FramePaths[tile.FrameIndex];
            if (!_textures.TryGetValue(path, out OpenGLTexture2D? texture))
            {
                throw new InvalidOperationException($"Puzzle frame '{path}' has not been prepared.");
            }

            renderer.DrawSprite(texture, tile.X, tile.Y, tile.Size, tile.Size, worldColor);
        }
    }

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
