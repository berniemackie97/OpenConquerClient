using System.Runtime.ExceptionServices;
using OpenConquer.Client.World;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;

namespace OpenConquer.Client.UI.Hud.Quickbar;

internal enum QuickbarImageKind
{
    Item,
    Control,
    Magic,
    Cover,
    Glow,
}

internal readonly record struct QuickbarImageRequest(QuickbarImageKind Kind, string Section = "", uint IconKey = 0);

internal readonly record struct QuickbarArtwork(RgbaImage? Image = null, AniFrameSet? Glow = null)
{
    internal bool HasPixels => Image is not null || Glow is not null;

    internal long DecodedBytes
    {
        get
        {
            long bytes = Image?.Pixels.Length ?? 0;
            if (Glow is { } frames)
            {
                for (uint index = 0; index < frames.FrameCount; index++)
                {
                    bytes += frames.GetFrame(index).Pixels.Length;
                }
            }

            return bytes;
        }
    }
}

/// <summary>Prepares a bounded batch of the current quickbar misses without holding any graphics resources.</summary>
internal sealed class MainHudQuickbarImageLoader(Func<QuickbarImageRequest, QuickbarArtwork> read) : IAsyncDisposable
{
    private const int MaximumBatchRequests = 8;
    private const int BatchPixelThreshold = 8 * 1024 * 1024;
    private readonly List<QuickbarImageRequest> _requested = new(MaximumBatchRequests);
    private readonly HashSet<QuickbarImageRequest> _retained = [];
    private Dictionary<QuickbarImageRequest, ImageResult> _ready = [];
    private WorldContentLoad<Dictionary<QuickbarImageRequest, ImageResult>>? _pending;
    private bool _disposed;

    internal MainHudQuickbarImageLoader(MainHudQuickbarAssets assets) : this(request => ReadAssets(assets, request))
    {
    }

    internal void BeginFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _requested.Clear();
        _retained.Clear();
        if (_pending is { IsCompleted: true } complete)
        {
            _pending = null;
            _ready = complete.Complete();
        }
    }

    internal bool TryGet(QuickbarImageRequest request, bool canUpload, out QuickbarArtwork artwork)
    {
        artwork = default;
        if (_ready.TryGetValue(request, out ImageResult result))
        {
            result.Failure?.Throw();
            if (!canUpload && result.Artwork.HasPixels)
            {
                _retained.Add(request);
                return false;
            }

            artwork = result.Artwork;
            return true;
        }

        if (_requested.Count < MaximumBatchRequests && !_requested.Contains(request))
        {
            _requested.Add(request);
        }

        return false;
    }

    internal void EndFrame()
    {
        if (_retained.Count == 0)
        {
            _ready.Clear();
        }
        else
        {
            foreach (QuickbarImageRequest request in _ready.Keys)
            {
                if (!_retained.Contains(request))
                {
                    _ready.Remove(request);
                }
            }
        }

        if (_pending is null && _ready.Count == 0 && _requested.Count != 0)
        {
            QuickbarImageRequest[] batch = [.. _requested];
            _pending = new WorldContentLoad<Dictionary<QuickbarImageRequest, ImageResult>>(token => ReadBatch(batch, token));
        }
    }

    internal void Cancel() => _pending?.Cancel();

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ready.Clear();
        _requested.Clear();
        _retained.Clear();
        if (_pending is { } pending)
        {
            _pending = null;
            await pending.DisposeAsync().ConfigureAwait(false);
        }
    }

    internal static QuickbarArtwork ReadAssets(MainHudQuickbarAssets assets, QuickbarImageRequest request) =>
        request.Kind switch
        {
            QuickbarImageKind.Item => new QuickbarArtwork(assets.GetItemFrame(request.IconKey)),
            QuickbarImageKind.Control => new QuickbarArtwork(assets.GetControlFrame0(request.Section)),
            QuickbarImageKind.Magic => new QuickbarArtwork(assets.GetMagicFrame0(request.Section)),
            QuickbarImageKind.Cover => new QuickbarArtwork(assets.GetCoverFrame()),
            QuickbarImageKind.Glow => new QuickbarArtwork(Glow: assets.GetEffectFrames(request.Section)),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Kind, "Unknown quickbar image kind."),
        };

    private Dictionary<QuickbarImageRequest, ImageResult> ReadBatch(QuickbarImageRequest[] requests, CancellationToken token)
    {
        Dictionary<QuickbarImageRequest, ImageResult> ready = new(requests.Length);
        long decodedBytes = 0;
        foreach (QuickbarImageRequest request in requests)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                QuickbarArtwork artwork = read(request);
                ready.Add(request, new ImageResult(artwork, null));
                decodedBytes += artwork.DecodedBytes;
                if (decodedBytes >= BatchPixelThreshold)
                {
                    break;
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                // An obsolete request's failure stays with its key and is discarded if it is no longer requested.
                ready.Add(request, new ImageResult(default, ExceptionDispatchInfo.Capture(exception)));
            }
        }

        return ready;
    }

    private readonly record struct ImageResult(QuickbarArtwork Artwork, ExceptionDispatchInfo? Failure);
}
