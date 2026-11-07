using OpenConquer.Client.UI.Hud.Quickbar;
using OpenConquer.Content.Images;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudQuickbarImageLoaderTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task BatchDeduplicatesRequestsAndLimitsDistinctReads()
    {
        List<QuickbarImageRequest> reads = [];
        await using MainHudQuickbarImageLoader loader = new(request =>
        {
            reads.Add(request);
            return default;
        });

        QuickbarImageRequest[] requests = Enumerable.Range(0, 9)
            .Select(index => new QuickbarImageRequest(QuickbarImageKind.Item, IconKey: (uint)index + 1)).ToArray();
        loader.BeginFrame();
        foreach (QuickbarImageRequest request in requests)
        {
            Assert.False(loader.TryGet(request, canUpload: true, out _));
            Assert.False(loader.TryGet(request, canUpload: true, out _));
        }

        loader.EndFrame();

        QuickbarArtwork result = await WaitForArtworkAsync(loader, requests[0]);
        Assert.False(result.HasPixels);
        Assert.Equal(requests[..8], reads);
        loader.EndFrame();
    }

    [Fact]
    public async Task ArtworkBlockedByTheUploadBudgetSurvivesToTheNextDraw()
    {
        int reads = 0;
        RgbaImage image = TargaImageReader.DecodeRle32Bit(
            Convert.FromHexString("00000a00000000000000000001000100200880112233ff"));
        await using MainHudQuickbarImageLoader loader = new(_ =>
        {
            reads++;
            return new QuickbarArtwork(image);
        });

        QuickbarImageRequest request = new(QuickbarImageKind.Cover);
        Assert.Same(image, (await WaitForArtworkAsync(loader, request)).Image);

        // A later slot requests the same artwork after the draw exhausts its upload budget.
        Assert.False(loader.TryGet(request, canUpload: false, out _));
        loader.EndFrame();

        loader.BeginFrame();
        Assert.True(loader.TryGet(request, canUpload: true, out QuickbarArtwork retained));
        Assert.Same(image, retained.Image);
        loader.EndFrame();
        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task AReplacedSlotsFailureDoesNotReachItsCurrentArtwork()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        QuickbarImageRequest old = new(QuickbarImageKind.Control, "old");
        QuickbarImageRequest current = new(QuickbarImageKind.Control, "current");
        List<QuickbarImageRequest> reads = [];
        await using MainHudQuickbarImageLoader loader = new(request =>
        {
            reads.Add(request);
            if (request == old)
            {
                started.SetResult();
                release.Task.WaitAsync(s_timeout, token).GetAwaiter().GetResult();
                throw new InvalidDataException("Obsolete quickbar artwork.");
            }

            return default;
        });

        loader.BeginFrame();
        Assert.False(loader.TryGet(old, canUpload: true, out _));
        loader.EndFrame();

        try
        {
            await started.Task.WaitAsync(s_timeout, token);
            loader.BeginFrame();
            Assert.False(loader.TryGet(current, canUpload: true, out _));
            loader.EndFrame();
            release.SetResult();

            Assert.False((await WaitForArtworkAsync(loader, current)).HasPixels);
            loader.EndFrame();
            Assert.Equal([old, current], reads);
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [Fact]
    public async Task ClosingWaitsForTheActiveReadAndSkipsTheRestOfTheBatch()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        QuickbarImageRequest first = new(QuickbarImageKind.Control, "first");
        List<QuickbarImageRequest> reads = [];
        await using MainHudQuickbarImageLoader loader = new(request =>
        {
            reads.Add(request);
            if (request == first)
            {
                started.SetResult();
                release.Task.WaitAsync(s_timeout, token).GetAwaiter().GetResult();
            }

            return default;
        });

        loader.BeginFrame();
        Assert.False(loader.TryGet(first, canUpload: true, out _));
        Assert.False(loader.TryGet(new QuickbarImageRequest(QuickbarImageKind.Cover), canUpload: true, out _));
        loader.EndFrame();

        try
        {
            await started.Task.WaitAsync(s_timeout, token);
            Task closing = loader.DisposeAsync().AsTask();
            Assert.False(closing.IsCompleted);
            release.SetResult();
            await closing.WaitAsync(s_timeout, token);
            Assert.Equal([first], reads);
        }
        finally
        {
            release.TrySetResult();
        }
    }

    private static async Task<QuickbarArtwork> WaitForArtworkAsync(
        MainHudQuickbarImageLoader loader, QuickbarImageRequest request)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        deadline.CancelAfter(s_timeout);
        while (true)
        {
            loader.BeginFrame();
            if (loader.TryGet(request, canUpload: true, out QuickbarArtwork artwork))
            {
                return artwork;
            }

            loader.EndFrame();
            await Task.Delay(1, deadline.Token);
        }
    }
}
