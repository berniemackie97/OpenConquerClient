using OpenConquer.Client.UI.Hud.SelectedSkill;
using OpenConquer.Content.Images;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudSelectedSkillImageLoaderTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ReplacingSelectionDiscardsItsFailureAndKeepsIndependentCover()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> requested = [];
        RgbaImage coverImage = TargaImageReader.DecodeRle32Bit(
            Convert.FromHexString("00000a00000000000000000001000100200880112233ff"));

        await using MainHudSelectedSkillImageLoader loader = new(section =>
        {
            requested.Add(section);
            if (section == "old")
            {
                started.SetResult();
                release.Task.WaitAsync(s_timeout, token).GetAwaiter().GetResult();
                throw new InvalidDataException("Obsolete selection.");
            }

            return null;
        }, () => coverImage);

        Assert.False(loader.Poll("old", needCover: true).Selected.IsComplete);
        try
        {
            await started.Task.WaitAsync(s_timeout, token);
            Assert.False(loader.Poll("intermediate", needCover: true).Selected.IsComplete);
            Assert.False(loader.Poll("latest", needCover: true).Selected.IsComplete);
            release.SetResult();

            MainHudSelectedSkillImageLoader.ImageResult cover = await WaitForResultAsync(
                () => loader.Poll("latest", needCover: true).Cover, token);
            Assert.Same(coverImage, cover.GetImage());

            MainHudSelectedSkillImageLoader.ImageResult missing = await WaitForResultAsync(
                () => loader.Poll("latest", needCover: false).Selected, token);
            Assert.Null(missing.GetImage());
            Assert.Equal(["old", "latest"], requested);
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [Fact]
    public async Task FailureOfCurrentImageIsReportedWhenTaken()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        IOException expected = new("Unreadable selected image.");
        await using MainHudSelectedSkillImageLoader loader = new(_ => throw expected, () => null);

        MainHudSelectedSkillImageLoader.ImageResult result = await WaitForResultAsync(
            () => loader.Poll("current", needCover: false).Selected, token);

        Assert.Same(expected, Assert.Throws<IOException>(() => result.GetImage()));
    }

    [Fact]
    public async Task ClosingWaitsForCurrentReadAndSkipsQueuedCover()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool coverRead = false;
        MainHudSelectedSkillImageLoader loader = new(_ =>
        {
            started.SetResult();
            release.Task.WaitAsync(s_timeout, token).GetAwaiter().GetResult();
            return null;
        }, () =>
        {
            coverRead = true;
            return null;
        });

        Assert.False(loader.Poll("current", needCover: true).Selected.IsComplete);
        try
        {
            await started.Task.WaitAsync(s_timeout, token);
            Task closing = loader.DisposeAsync().AsTask();
            Assert.False(closing.IsCompleted);
            release.SetResult();
            await closing.WaitAsync(s_timeout, token);
            Assert.False(coverRead);
        }
        finally
        {
            release.TrySetResult();
            await loader.DisposeAsync();
        }
    }

    private static async Task<MainHudSelectedSkillImageLoader.ImageResult> WaitForResultAsync(
        Func<MainHudSelectedSkillImageLoader.ImageResult> poll, CancellationToken token)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(s_timeout);
        while (true)
        {
            MainHudSelectedSkillImageLoader.ImageResult result = poll();
            if (result.IsComplete)
            {
                return result;
            }

            await Task.Delay(1, deadline.Token);
        }
    }
}
