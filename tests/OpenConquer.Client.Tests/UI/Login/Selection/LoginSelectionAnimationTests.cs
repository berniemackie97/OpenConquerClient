using OpenConquer.Client.UI.Login.Selection;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Login;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login.Selection;

public sealed class LoginSelectionAnimationTests
{
    private static readonly LoginStageLayout s_layout = LoginStageLayout.Create(new LogicalRenderSize(1024, 768));

    [Fact]
    public void SelectionTimelinesRestartWhenTheSelectionChanges()
    {
        LoginSelectionAnimation animation = new(s_layout);
        LoginServerSelection selection = SelectionWithServers(regionCount: 2);
        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(0));

        animation.Advance(0, selection, interactive: true);
        animation.Advance(3, selection, interactive: true);
        Assert.Equal(3, animation.RegionFlameFrame);
        Assert.Equal(3, animation.RegionFlameFadeFrame);
        Assert.Equal(3, animation.ServerSelectedFrame);

        Assert.True(selection.SelectServerSlot(1));
        animation.Advance(0, selection, interactive: true);
        Assert.Equal(3, animation.RegionFlameFrame);
        Assert.Equal(0, animation.ServerSelectedFrame);

        Assert.True(selection.SelectRegionSlot(1));
        animation.Advance(0, selection, interactive: true);
        Assert.Equal(0, animation.RegionFlameFrame);
        Assert.Equal(0, animation.RegionFlameFadeFrame);
    }

    [Fact]
    public void FlameLoopsAndItsFadeStopsAfterFiveFrames()
    {
        LoginSelectionAnimation animation = new(s_layout);
        LoginServerSelection selection = SelectionWithServers(regionCount: 1);
        Assert.True(selection.SelectRegionSlot(0));

        animation.Advance(0, selection, interactive: true);
        animation.Advance(LoginRegionArtwork.FlameFrameCount + 2, selection, interactive: true);

        Assert.Equal(2, animation.RegionFlameFrame);
        Assert.Equal(5, animation.RegionFlameFadeFrame);
    }

    [Fact]
    public void SelectedServerPlaysOnceThenLoopsFromFrame23()
    {
        LoginSelectionAnimation animation = new(s_layout);
        LoginServerSelection selection = SelectionWithServers(regionCount: 1);
        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(0));
        animation.Advance(0, selection, interactive: true);

        animation.Advance(LoginServerArtwork.SelectedFrameCount - 1, selection, interactive: true);
        Assert.Equal(LoginServerArtwork.SelectedFrameCount - 1, animation.ServerSelectedFrame);
        animation.Advance(1, selection, interactive: true);
        Assert.Equal(LoginServerArtwork.SelectedLoopStart, animation.ServerSelectedFrame);
        animation.Advance(LoginServerArtwork.SelectedFrameCount - LoginServerArtwork.SelectedLoopStart, selection, interactive: true);
        Assert.Equal(LoginServerArtwork.SelectedLoopStart, animation.ServerSelectedFrame);
    }

    [Fact]
    public void RegionRolloverHoldsThenRollsOutOverThreeFrames()
    {
        LoginSelectionAnimation animation = new(s_layout);
        LoginServerSelection selection = SelectionWithServers(regionCount: 1);
        LoginRect tile = s_layout.RegionHitAreas[0];

        animation.PointerMoved(true, tile.X + 4, tile.Y + 4);
        animation.Advance(0, selection, interactive: true);
        Assert.Equal(0, animation.RegionRolloverSlot);
        Assert.Equal(0, animation.RegionRolloverFrame);
        animation.Advance(20, selection, interactive: true);
        Assert.Equal(6, animation.RegionRolloverFrame);

        animation.PointerMoved(true, 0, 0);
        animation.Advance(0, selection, interactive: true);
        Assert.Equal(-1, animation.HoveredRegionSlot);
        Assert.Equal(0, animation.RegionRolloverSlot);
        Assert.Equal(7, animation.RegionRolloverFrame);
        animation.Advance(2, selection, interactive: true);
        Assert.Equal(9, animation.RegionRolloverFrame);
        animation.Advance(1, selection, interactive: true);
        Assert.Equal(-1, animation.RegionRolloverSlot);
    }

    [Fact]
    public void SelectedRegionAndEmptyTilesDoNotRollOver()
    {
        LoginSelectionAnimation animation = new(s_layout);
        LoginServerSelection selection = SelectionWithServers(regionCount: 1);
        Assert.True(selection.SelectRegionSlot(0));

        LoginRect selected = s_layout.RegionHitAreas[0];
        animation.PointerMoved(true, selected.X + 4, selected.Y + 4);
        animation.Advance(0, selection, interactive: true);
        Assert.Equal(-1, animation.RegionRolloverSlot);

        LoginRect empty = s_layout.RegionHitAreas[1];
        animation.PointerMoved(true, empty.X + 4, empty.Y + 4);
        animation.Advance(0, selection, interactive: true);
        Assert.Equal(-1, animation.RegionRolloverSlot);
    }

    [Fact]
    public void ServerHoverNeedsASelectedRegionAndAnInteractiveStage()
    {
        LoginSelectionAnimation animation = new(s_layout);
        LoginServerSelection selection = SelectionWithServers(regionCount: 1);
        LoginRect server = s_layout.ServerHitAreas[0];

        animation.PointerMoved(true, server.X + 4, server.Y + 4);
        animation.Advance(0, selection, interactive: true);
        Assert.Equal(-1, animation.HoveredServerSlot);

        Assert.True(selection.SelectRegionSlot(0));
        animation.Advance(0, selection, interactive: true);
        Assert.Equal(0, animation.HoveredServerSlot);
        animation.Advance(LoginServerArtwork.HoverFrameCount - 1, selection, interactive: true);
        Assert.Equal(LoginServerArtwork.HoverFrameCount - 1, animation.ServerHoverFrame);
        animation.Advance(1, selection, interactive: true);
        Assert.Equal(0, animation.ServerHoverFrame);

        animation.Advance(0, selection, interactive: false);
        Assert.Equal(-1, animation.HoveredServerSlot);
    }

    private static LoginServerSelection SelectionWithServers(int regionCount)
    {
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(new LoginServerCatalog(Enumerable.Range(0, regionCount).Select(region =>
            new LoginRegion($"region-{region}", $"Region {region}",
                [new LoginServer($"realm-{region}-0", "Realm 0", $"Protocol{region}_0"), new LoginServer($"realm-{region}-1", "Realm 1", $"Protocol{region}_1")]))));
        return selection;
    }
}
