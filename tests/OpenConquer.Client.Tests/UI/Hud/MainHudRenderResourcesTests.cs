using OpenConquer.Client.UI.Hud.Rendering;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudRenderResourcesTests
{
    [Fact]
    public void Ownership_TracksAndReturnsExactResourceInstance()
    {
        List<string> events = [];
        using MainHudRendererOwnership ownership = new();
        RecordingResource resource = new("chrome", events);

        RecordingResource tracked = ownership.Track(resource);

        Assert.Same(resource, tracked);
    }

    [Fact]
    public void Dispose_ReleasesResourcesInReverseConstructionOrder()
    {
        List<string> events = [];
        MainHudRendererOwnership ownership = new();

        string[] constructionOrder =
        [
            "Chrome",
            "Vitals",
            "SkillExperience",
            "Quickbar",
            "ActionButtons",
            "CheckControls",
            "SelectedSkill",
            "SelectedSkillCooldown",
            "StatusHints",
            "MagicHint",
        ];

        foreach (string name in constructionOrder)
        {
            ownership.Track(new RecordingResource(name, events));
        }

        ownership.Dispose();

        Assert.Equal(constructionOrder.Reverse(), events);
    }

    [Fact]
    public void Dispose_AttemptsEveryResourceWhenEarlierDisposalsThrow()
    {
        List<string> events = [];
        MainHudRendererOwnership ownership = new();

        InvalidOperationException expected = new("magic hint cleanup");
        InvalidOperationException secondary = new("selected skill cleanup");

        ownership.Track(new RecordingResource("Chrome", events));
        ownership.Track(new RecordingResource("Vitals", events));
        ownership.Track(new RecordingResource("SelectedSkill", events, secondary));
        ownership.Track(new RecordingResource("StatusHints", events));
        ownership.Track(new RecordingResource("MagicHint", events, expected));

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(ownership.Dispose);

        Assert.Same(expected, thrown);
        Assert.Contains(nameof(RecordingResource.Dispose), thrown.StackTrace);
        Assert.Equal(
        [
            "MagicHint",
            "StatusHints",
            "SelectedSkill",
            "Vitals",
            "Chrome",
        ], events);
    }

    [Fact]
    public void DisposeAfterInitializationFailure_PreservesOriginalConstructionFailure()
    {
        List<string> events = [];
        MainHudRendererOwnership ownership = new();

        InvalidOperationException primary = new("renderer initialization failed");

        ownership.Track(new RecordingResource("Chrome", events));
        ownership.Track(new RecordingResource("Vitals", events, new InvalidOperationException("vitals disposal failed")));

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() =>
            SimulateConstructionFailure(ownership, primary));

        Assert.Same(primary, thrown);
        Assert.Contains(nameof(SimulateConstructionFailure), thrown.StackTrace);
        Assert.Equal(["Vitals", "Chrome"], events);
    }

    [Fact]
    public void DisposeAfterInitializationFailure_OnlyReleasesConstructedResources()
    {
        List<string> events = [];
        MainHudRendererOwnership ownership = new();

        ownership.Track(new RecordingResource("Chrome", events));
        ownership.Track(new RecordingResource("Vitals", events));

        InvalidOperationException expected = new("skill renderer initialization failed");

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() =>
            SimulateConstructionFailure(ownership, expected));

        Assert.Same(expected, thrown);
        Assert.Equal(["Vitals", "Chrome"], events);
    }

    [Fact]
    public void Dispose_IsIdempotentAfterSuccessfulCleanup()
    {
        List<string> events = [];
        MainHudRendererOwnership ownership = new();
        RecordingResource resource = new("Chrome", events);

        ownership.Track(resource);

        ownership.Dispose();
        ownership.Dispose();

        Assert.Equal(1, resource.DisposeCount);
        Assert.Equal(["Chrome"], events);
    }

    [Fact]
    public void Dispose_IsIdempotentAfterCleanupFailure()
    {
        List<string> events = [];
        MainHudRendererOwnership ownership = new();
        RecordingResource resource = new("Chrome", events, new InvalidOperationException("cleanup failed"));

        ownership.Track(resource);

        Assert.Throws<InvalidOperationException>(ownership.Dispose);

        ownership.Dispose();

        Assert.Equal(1, resource.DisposeCount);
        Assert.Equal(["Chrome"], events);
    }

    [Fact]
    public void Track_AfterDisposalIsRejected()
    {
        List<string> events = [];
        MainHudRendererOwnership ownership = new();

        ownership.Dispose();

        RecordingResource resource = new("Chrome", events);

        Assert.Throws<ObjectDisposedException>(() => ownership.Track(resource));
        Assert.Equal(0, resource.DisposeCount);
    }

    [Fact]
    public void IndependentOwners_ReleaseOnlyTheirOwnResources()
    {
        List<string> events = [];
        MainHudRendererOwnership first = new();
        MainHudRendererOwnership second = new();

        RecordingResource firstResource = first.Track(new RecordingResource("First", events));
        RecordingResource secondResource = second.Track(new RecordingResource("Second", events));

        first.Dispose();

        Assert.Equal(1, firstResource.DisposeCount);
        Assert.Equal(0, secondResource.DisposeCount);

        second.Dispose();

        Assert.Equal(1, secondResource.DisposeCount);
        Assert.Equal(["First", "Second"], events);
    }

    private static void SimulateConstructionFailure(MainHudRendererOwnership ownership, InvalidOperationException exception)
    {
        try
        {
            throw exception;
        }
        catch
        {
            ownership.DisposeAfterInitializationFailure();
            throw;
        }
    }

    private sealed class RecordingResource(string name, List<string> events, Exception? failure = null) : IDisposable
    {
        public int DisposeCount
        {
            get; private set;
        }

        public void Dispose()
        {
            DisposeCount++;
            events.Add(name);

            if (failure is not null)
            {
                throw failure;
            }
        }
    }
}
