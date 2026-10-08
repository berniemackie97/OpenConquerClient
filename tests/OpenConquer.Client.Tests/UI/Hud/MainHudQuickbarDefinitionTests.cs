using OpenConquer.Client.UI.Hud.Quickbar;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudQuickbarDefinitionTests
{
    [Fact]
    public void Definition_MatchesVerified5517GridContract()
    {
        Assert.Equal(0x3FD, MainHudQuickbarDefinition.ControlId);
        Assert.Equal(1, MainHudQuickbarDefinition.RowCount);
        Assert.Equal(10, MainHudQuickbarDefinition.ColumnCount);
        Assert.Equal(10, MainHudQuickbarDefinition.SlotCount);
        Assert.Equal(3, MainHudQuickbarDefinition.NativeContext);
        Assert.Equal(90, MainHudQuickbarDefinition.LocalX);
        Assert.Equal(98, MainHudQuickbarDefinition.LocalY);
        Assert.Equal(40, MainHudQuickbarDefinition.CellWidth);
        Assert.Equal(40, MainHudQuickbarDefinition.CellHeight);
        Assert.Equal(41, MainHudQuickbarDefinition.HorizontalStride);
        Assert.Equal(410, MainHudQuickbarDefinition.Width);
        Assert.Equal(40, MainHudQuickbarDefinition.Height);
        Assert.Equal("Compose_CoverPic", MainHudQuickbarDefinition.CoverAniSectionName);
        Assert.Equal(1, MainHudQuickbarDefinition.CoverFrameCount);
    }

    [Fact]
    public void ContentKinds_MatchVerifiedNativeValues()
    {
        Assert.Equal(1, (int)MainHudQuickbarContentKind.Item);
        Assert.Equal(2, (int)MainHudQuickbarContentKind.Action);
        Assert.Equal(3, (int)MainHudQuickbarContentKind.Magic);
        Assert.Equal(4, (int)MainHudQuickbarContentKind.XpMagic);
        Assert.Equal(5, (int)MainHudQuickbarContentKind.Dance);
        Assert.Equal(6, (int)MainHudQuickbarContentKind.WeaponSwap);
    }

    [Fact]
    public void ContentKinds_DoNotDefineAnEmptyState()
    {
        Assert.False(Enum.IsDefined((MainHudQuickbarContentKind)0));
    }
}
