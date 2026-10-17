using OpenConquer.Client.UI.Login;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginRetailSelectionMotionTests
{
    [Theory]
    [InlineData(1024, 768)]
    [InlineData(800, 600)]
    public void OriginalAnimationsRequireAuthenticatedVisibleSelection(int width, int height)
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(width, height));
        LoginRetailSelectionMotion motion = new(layout);
        LoginServerSelection selection = new();
        motion.Advance(0.1, selection, interactive: true);
        Assert.Equal(-1, motion.RegionFlameFrame);
        Assert.Equal(-1, motion.ServerSelectedFrame);
        Assert.Equal(-1, motion.HoveredServerSlot);

        selection.ReplaceCatalog(new LoginServerCatalog([
            new LoginRegion("region", "Region", [new LoginServer("realm", "Realm", "Protocol")])
        ]));
        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(0));
        motion.Advance(0, selection, interactive: true);
        Assert.Equal(0, motion.RegionFlameFrame);
        Assert.Equal(0, motion.ServerSelectedFrame);
        motion.Advance(1.0 / 30, selection, interactive: true);
        Assert.Equal(1, motion.RegionFlameFrame);
        Assert.Equal(1, motion.ServerSelectedFrame);

        selection.ChangeRegionPage(1); // Not possible with one page; selections remain.
        selection.ReplaceCatalog(LoginServerCatalog.Empty);
        motion.Advance(0, selection, interactive: false);
        Assert.Equal(-1, motion.RegionFlameFrame);
        Assert.Equal(-1, motion.ServerSelectedFrame);
    }

    [Fact]
    public void ServerSelectedLoopStartsAtOriginalAvm1Frame23()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));
        LoginRetailSelectionMotion motion = new(layout);
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(new LoginServerCatalog([
            new LoginRegion("region", "Region", [new LoginServer("realm", "Realm", "Protocol")])
        ]));
        selection.SelectRegionSlot(0);
        selection.SelectServerSlot(0);
        for (int tick = 0; tick < 47; tick++)
            motion.Advance(1.0 / 30, selection, interactive: true);
        Assert.Equal(22, motion.ServerSelectedFrame);
        for (int tick = 0; tick < 25; tick++)
            motion.Advance(1.0 / 30, selection, interactive: true);
        Assert.Equal(22, motion.ServerSelectedFrame);
        Assert.InRange(motion.RegionFlameFrame, 0, 24);
    }

    [Fact]
    public void RealmHoverIsBoundedToActualVisibleCellsAndSuppressedByDialogs()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));
        LoginRetailSelectionMotion motion = new(layout);
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(new LoginServerCatalog([
            new LoginRegion("region", "Region", [new LoginServer("realm", "Realm", "Protocol")])
        ]));
        LoginSpritePlacement region = layout.RegionHitAreas[0];
        LoginSpritePlacement server = layout.ServerHitAreas[0];
        motion.PointerMoved(true, server.X + 4, server.Y + 4);
        motion.Advance(1.0 / 30, selection, interactive: true);
        Assert.Equal(-1, motion.HoveredServerSlot); // No realm without a region.
        motion.PointerMoved(true, region.X + 4, region.Y + 4);
        motion.Advance(0, selection, interactive: true);
        Assert.Equal(0, motion.HoveredRegionSlot);
        Assert.Equal(0, motion.RegionBorderFrame);
        selection.SelectRegionSlot(0);
        motion.PointerMoved(true, server.X + 4, server.Y + 4);
        motion.Advance(0, selection, interactive: true);
        Assert.Equal(0, motion.HoveredServerSlot);
        motion.Advance(0.2, selection, interactive: false); // Modal intercepts the pointer.
        Assert.Equal(-1, motion.HoveredServerSlot);
        Assert.Equal(-1, motion.HoveredRegionSlot);
    }
}
