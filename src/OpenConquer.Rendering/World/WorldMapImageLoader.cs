using System.Runtime.ExceptionServices;
using OpenConquer.Content;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;

namespace OpenConquer.Rendering.World;

/// <summary>
/// Prepares a bounded batch of map images off the context thread. The scene requests images for the current view,
/// and the renderer consumes completed images when its upload budget allows.
/// </summary>
public sealed class WorldMapImageLoader : IAsyncDisposable
{
    private const int MaximumBatchImages = 8;
    private const int BatchPixelThreshold = 8 * 1024 * 1024;
    private readonly Func<AniIndexSection, int, RgbaImage> _decode;
    private readonly CancellationTokenSource _stop = new();
    private readonly Dictionary<string, Request> _requested = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _stillWanted = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, Result> _ready = new(StringComparer.OrdinalIgnoreCase);
    private Task<Dictionary<string, Result>>? _pending;
    private bool _disposed;

    public WorldMapImageLoader(IClientContentSource source)
        : this((animation, index) => AniFrameLoader.LoadWrappedFrame(source, animation,
            (uint)index, ContentLookupMode.LooseThenPackage))
    {
        ArgumentNullException.ThrowIfNull(source);
    }

    internal WorldMapImageLoader(Func<AniIndexSection, int, RgbaImage> decode) => _decode = decode;

    internal void BeginPreparation()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _requested.Clear();
        _stillWanted.Clear();
        if (_pending is { IsCompleted: true } complete)
        {
            _pending = null;
            _ready = complete.GetAwaiter().GetResult();
        }
    }

    internal RgbaImage? RequestImage(string path, AniIndexSection animation, int index, bool canUpload)
    {
        if (_ready.TryGetValue(path, out Result result))
        {
            result.Failure?.Throw();
            if (canUpload)
            {
                _ready.Remove(path);
                return result.Image;
            }

            _stillWanted.Add(path);
        }
        else if (_requested.Count < MaximumBatchImages)
        {
            _requested.TryAdd(path, new Request(path, animation, index));
        }

        return null;
    }

    internal void EndPreparation()
    {
        foreach (string path in _ready.Keys.Where(path => !_stillWanted.Contains(path)).ToArray())
        {
            _ready.Remove(path);
        }

        if (_pending is null && _ready.Count == 0 && _requested.Count != 0)
        {
            Request[] batch = _requested.Values.ToArray();
            CancellationToken token = _stop.Token;
            _pending = Task.Run(() => DecodeBatch(batch, token), token);
        }
    }

    private Dictionary<string, Result> DecodeBatch(Request[] requests, CancellationToken token)
    {
        Dictionary<string, Result> results = new(requests.Length, StringComparer.OrdinalIgnoreCase);
        int bytes = 0;
        foreach (Request request in requests)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                RgbaImage image = _decode(request.Animation, request.Index);
                results.Add(request.Path, new Result(image, null));
                bytes += image.Pixels.Length;
                if (bytes >= BatchPixelThreshold)
                {
                    // Allow a single legal large image and stop the batch once it crosses the threshold.
                    break;
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                // A stale request's failure must not invalidate a later viewport.
                results.Add(request.Path, new Result(null, ExceptionDispatchInfo.Capture(exception)));
            }
        }

        return results;
    }

    /// <summary>Stops preparation after the current content read returns.</summary>
    public void Cancel()
    {
        if (!_disposed)
        {
            _stop.Cancel();
        }
    }

    /// <summary>Cancels and observes the outstanding batch. This owner never holds OpenGL resources.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stop.Cancel();
        try
        {
            if (_pending is { } pending)
            {
                await pending.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            // The world requested cancellation before the batch finished.
        }
        finally
        {
            _pending = null;
            _ready.Clear();
            _requested.Clear();
            _stillWanted.Clear();
            _stop.Dispose();
        }
    }

    private readonly record struct Request(string Path, AniIndexSection Animation, int Index);
    private readonly record struct Result(RgbaImage? Image, ExceptionDispatchInfo? Failure);
}
