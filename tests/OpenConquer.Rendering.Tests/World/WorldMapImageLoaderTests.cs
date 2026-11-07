using System.Globalization;
using System.Text;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;
using OpenConquer.Rendering.World;

namespace OpenConquer.Rendering.Tests.World;

public sealed class WorldMapImageLoaderTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task DuplicateAndObsoleteRequests_DoNotAccumulateOrReplaceTheLatestImage()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        AniIndexSection frames = Frames();
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<int> decoded = [];
        await using WorldMapImageLoader loader = new((_, index) =>
        {
            decoded.Add(index);
            if (index == 0)
            {
                started.SetResult();
                release.Task.WaitAsync(s_timeout, token).GetAwaiter().GetResult();
                throw new InvalidDataException("Stale texture is corrupt.");
            }

            return DecodePixel();
        });

        loader.BeginPreparation();
        Assert.Null(loader.RequestImage(frames.FramePaths[0], frames, 0, canUpload: true));
        Assert.Null(loader.RequestImage(frames.FramePaths[0].ToUpperInvariant(), frames, 0, canUpload: true));
        loader.EndPreparation();
        try
        {
            await started.Task.WaitAsync(s_timeout, token);
            Assert.Null(RequestFrame(loader, frames, 1, canUpload: true));
            release.SetResult();

            RgbaImage image = await WaitForImageAsync(loader, frames, 1, token);
            Assert.Equal([0, 1], decoded);
            Assert.Equal((1, 1), (image.Width, image.Height));
            Assert.Equal(new byte[] { 0x33, 0x22, 0x11, 0xFF }, image.Pixels.ToArray());
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [Fact]
    public async Task AViewportWithMoreThanEightMisses_DecodesOnlyOneBoundedBatch()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        AniIndexSection frames = Frames(12);
        List<int> decoded = [];
        await using WorldMapImageLoader loader = new((_, index) =>
        {
            decoded.Add(index);
            return DecodePixel();
        });

        loader.BeginPreparation();
        for (int index = 0; index < frames.FrameCount; index++)
        {
            Assert.Null(loader.RequestImage(frames.FramePaths[index], frames, index, canUpload: true));
        }

        loader.EndPreparation();
        _ = await WaitForImageAsync(loader, frames, 0, token);
        Assert.Equal(Enumerable.Range(0, 8), decoded);
    }

    [Fact]
    public async Task RequestedFailure_IsReportedToTheFrame()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using WorldMapImageLoader loader = new((_, _) => throw new IOException("Unreadable image."));
        await Assert.ThrowsAsync<IOException>(() => WaitForImageAsync(loader, Frames(), 0, token));
    }

    [Fact]
    public async Task Disposal_WaitsForTheCurrentReadAndCancelsLaterImages()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        AniIndexSection frames = Frames();
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<int> decoded = [];
        WorldMapImageLoader loader = new((_, index) =>
        {
            decoded.Add(index);
            started.SetResult();
            release.Task.WaitAsync(s_timeout, token).GetAwaiter().GetResult();
            return DecodePixel();
        });

        loader.BeginPreparation();
        Assert.Null(loader.RequestImage(frames.FramePaths[0], frames, 0, canUpload: true));
        Assert.Null(loader.RequestImage(frames.FramePaths[1], frames, 1, canUpload: true));
        loader.EndPreparation();

        try
        {
            await started.Task.WaitAsync(s_timeout, token);
            Task closing = loader.DisposeAsync().AsTask();
            Assert.False(closing.IsCompleted);
            release.SetResult();
            await closing.WaitAsync(s_timeout, token);
            Assert.Equal([0], decoded);
        }
        finally
        {
            release.TrySetResult();
            await loader.DisposeAsync();
        }
    }

    private static RgbaImage? RequestFrame(WorldMapImageLoader loader, AniIndexSection frames, int index, bool canUpload)
    {
        loader.BeginPreparation();
        RgbaImage? image = loader.RequestImage(frames.FramePaths[index], frames, index, canUpload);
        loader.EndPreparation();
        return image;
    }

    private static async Task<RgbaImage> WaitForImageAsync(WorldMapImageLoader loader, AniIndexSection frames,
        int index, CancellationToken token)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(s_timeout);
        while (true)
        {
            if (RequestFrame(loader, frames, index, canUpload: true) is { } image)
            {
                return image;
            }

            await Task.Delay(1, deadline.Token);
        }
    }

    private static AniIndexSection Frames(int count = 2)
    {
        StringBuilder text = new();
        text.AppendLine("[Map]");
        text.AppendLine(CultureInfo.InvariantCulture, $"FrameAmount={count}");
        for (int index = 0; index < count; index++)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"Frame{index}=map/{index}.tga");
        }

        using MemoryStream stream = new(Encoding.ASCII.GetBytes(text.ToString()));
        return AniIndexFile.Load(stream, "map.ani").GetRequiredSection("Map");
    }

    private static RgbaImage DecodePixel() =>
        TargaImageReader.DecodeRle32Bit(Convert.FromHexString("00000a00000000000000000001000100200880112233ff"));
}
