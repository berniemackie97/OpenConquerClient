using OpenConquer.Rendering.World;

namespace OpenConquer.Rendering.Tests.World;

public sealed class WorldMapTextureCacheTests
{
    [Fact]
    public void StreamingCacheRetainsTheDisplayedAndPendingViewsUntilHandoff()
    {
        using WorldMapTextureCache<TrackedTexture> cache = new(2, 8, streaming: true);
        TrackedTexture old = new();
        TrackedTexture shared = new();
        TrackedTexture next = new();

        cache.BeginPreparation();
        cache.Add("old", old, 4);
        cache.Add("shared", shared, 4);
        cache.EndPreparation(complete: true);

        cache.BeginPreparation();
        Assert.True(cache.Keep("SHARED"));
        cache.Add("next", next, 4);
        cache.EndPreparation(complete: false);

        Assert.Equal(3, cache.Count);
        Assert.Equal(12, cache.PixelBytes);
        Assert.Same(old, cache.Find("old"));
        Assert.Same(next, cache.Find("next"));

        cache.BeginPreparation();
        Assert.True(cache.Keep("shared"));
        Assert.True(cache.Keep("next"));
        cache.EndPreparation(complete: true);

        Assert.Equal(2, cache.Count);
        Assert.Equal(8, cache.PixelBytes);
        Assert.Equal(1, old.Disposals);
        Assert.Equal(0, shared.Disposals);
        Assert.Equal(0, next.Disposals);
        Assert.Null(cache.Find("old"));
    }

    [Theory]
    [InlineData(2, 16)]
    [InlineData(4, 8)]
    public void StreamingCacheEvictsTheLeastRecentIdleTexture(int maximumTextures, long maximumBytes)
    {
        using WorldMapTextureCache<TrackedTexture> cache = new(maximumTextures, maximumBytes, streaming: true);
        TrackedTexture first = new();
        TrackedTexture second = new();

        cache.BeginPreparation();
        cache.Add("first", first, 4);
        cache.Add("second", second, 4);
        cache.EndPreparation(complete: true);

        cache.BeginPreparation();
        Assert.True(cache.Keep("first"));
        cache.EndPreparation(complete: true);

        cache.BeginPreparation();
        cache.Add("third", new TrackedTexture(), 4);
        cache.EndPreparation(complete: true);

        Assert.Equal(2, cache.Count);
        Assert.Equal(8, cache.PixelBytes);
        Assert.Equal(1, second.Disposals);
        Assert.Same(first, cache.Find("first"));
        Assert.Null(cache.Find("second"));
    }

    [Fact]
    public void ReplacedCandidateCanBeEvictedBeforeEitherCandidateCompletes()
    {
        using WorldMapTextureCache<TrackedTexture> cache = new(2, 8, streaming: true);
        TrackedTexture abandoned = new();
        TrackedTexture displayed = new();
        TrackedTexture next = new();

        cache.BeginPreparation();
        cache.Add("displayed", displayed, 4);
        cache.EndPreparation(complete: true);
        cache.BeginPreparation();
        cache.Add("abandoned", abandoned, 4);
        cache.EndPreparation(complete: false);

        cache.BeginPreparation();
        cache.Add("next", next, 4);
        cache.EndPreparation(complete: false);

        Assert.Equal(2, cache.Count);
        Assert.Equal(1, abandoned.Disposals);
        Assert.Same(displayed, cache.Find("displayed"));
        Assert.Same(next, cache.Find("next"));
    }

    [Fact]
    public void ReleasingAnEvictedAndReloadedPathDisposesEachTextureOnce()
    {
        using WorldMapTextureCache<TrackedTexture> cache = new(1, 4, streaming: true);
        TrackedTexture first = new();
        TrackedTexture second = new();
        TrackedTexture reloaded = new();

        foreach ((string path, TrackedTexture texture) in new[] { ("a", first), ("b", second), ("a", reloaded) })
        {
            cache.BeginPreparation();
            cache.Add(path, texture, 4);
            cache.EndPreparation(complete: true);
        }

        Assert.Same(reloaded, cache.Find("a"));
        cache.Dispose();

        Assert.Equal(1, first.Disposals);
        Assert.Equal(1, second.Disposals);
        Assert.Equal(1, reloaded.Disposals);
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.PixelBytes);
    }

    private sealed class TrackedTexture : IDisposable
    {
        internal int Disposals
        {
            get;
            private set;
        }

        public void Dispose() => Disposals++;
    }
}
