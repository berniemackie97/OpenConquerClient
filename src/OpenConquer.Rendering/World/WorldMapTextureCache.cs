using System.Runtime.ExceptionServices;

namespace OpenConquer.Rendering.World;

/// <summary>
/// Owns map textures and accounts for their uploaded RGBA bytes. A streaming cache protects both live views
/// while reclaiming idle textures in least-recently-used order.
/// </summary>
internal sealed class WorldMapTextureCache<TTexture>(int maximumTextures, long maximumPixelBytes, bool streaming)
    : IDisposable where TTexture : class, IDisposable
{
    private readonly Dictionary<string, LinkedListNode<Entry>> _textures = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<Entry> _recency = new();
    private HashSet<string> _presented = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _candidate = new(StringComparer.OrdinalIgnoreCase);

    internal int Count => _textures.Count;
    internal long PixelBytes
    {
        get;
        private set;
    }

    internal bool Fits(int additionalBytes) => Count < maximumTextures && additionalBytes <= maximumPixelBytes - PixelBytes;

    internal void BeginPreparation() => _candidate.Clear();

    internal bool Keep(string path)
    {
        if (!_textures.TryGetValue(path, out LinkedListNode<Entry>? node))
        {
            return false;
        }

        if (streaming && _candidate.Add(path))
        {
            _recency.Remove(node);
            _recency.AddLast(node);
        }

        return true;
    }

    internal void Add(string path, TTexture texture, int pixelBytes)
    {
        LinkedListNode<Entry> node = new(new Entry(path, texture, pixelBytes));
        if (streaming)
        {
            _candidate.Add(path);
        }

        _textures.Add(path, node);
        _recency.AddLast(node);
        PixelBytes += pixelBytes;
    }

    internal TTexture? Find(string path) =>
        _textures.TryGetValue(path, out LinkedListNode<Entry>? node) ? node.Value.Texture : null;

    internal void EndPreparation(bool complete)
    {
        if (complete)
        {
            // Only the new complete view can draw after this point.
            (_presented, _candidate) = (_candidate, _presented);
            _candidate.Clear();
        }

        LinkedListNode<Entry>? node = _recency.First;
        while (node is not null && (Count > maximumTextures || PixelBytes > maximumPixelBytes))
        {
            LinkedListNode<Entry>? next = node.Next;
            Entry entry = node.Value;
            if (!_presented.Contains(entry.Path) && !_candidate.Contains(entry.Path))
            {
                _textures.Remove(entry.Path);
                _recency.Remove(node);
                PixelBytes -= entry.PixelBytes;
                entry.Texture.Dispose();
            }

            node = next;
        }
    }

    public void Dispose()
    {
        ExceptionDispatchInfo? firstFailure = null;
        foreach (Entry entry in _recency)
        {
            try
            {
                entry.Texture.Dispose();
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        _textures.Clear();
        _recency.Clear();
        _presented.Clear();
        _candidate.Clear();
        PixelBytes = 0;
        firstFailure?.Throw();
    }

    private readonly record struct Entry(string Path, TTexture Texture, int PixelBytes);
}
