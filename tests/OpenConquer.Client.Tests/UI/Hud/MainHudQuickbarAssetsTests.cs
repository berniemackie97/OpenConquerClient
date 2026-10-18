using OpenConquer.Client.UI.Hud.Quickbar;
using OpenConquer.Content;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudQuickbarAssetsTests
{
    [Fact]
    public void MissingAni_ReturnsMissingFrame()
    {
        using TemporaryContentDirectory content = new();
        MainHudQuickbarAssets assets = new(new ClientContentRoot(content.RootPath));

        Assert.Null(assets.GetCoverFrame());
        Assert.Null(assets.GetControlFrame0("ButtonA1"));
        Assert.Null(assets.GetMagicFrame0("MagicSkillType1000"));
        Assert.Null(assets.GetItemFrame(1000));
    }

    [Fact]
    public void SectionLoadIsEagerAndMissingLaterFrameMakesSectionUnavailable()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", "[ButtonA1]\nFrameAmount=2\nFrame0=data/test/a.dds\nFrame1=data/test/b.dds\n");
        content.WriteBytes("data/test/a.dds", DdsTestData.CreateDxt3Dds(64, 64));

        MainHudQuickbarAssets assets = new(new ClientContentRoot(content.RootPath));

        Assert.Null(assets.GetControlFrame0("ButtonA1"));
    }

    [Fact]
    public void ZeroFrameMagicSection_ReturnsMissingFrame()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Magic.ani", "[MagicSkillType1270]\nFrameAmount=0\nFrame0=data/main/Relive01.dds\nFrame1=data/main/Relive02.dds\n");

        MainHudQuickbarAssets assets = new(new ClientContentRoot(content.RootPath));

        Assert.Null(assets.GetMagicFrame0("MagicSkillType1270"));
    }

    [Fact]
    public void SectionFramesAreNotRetainedAcrossLookups()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", "[ButtonA1]\nFrameAmount=1\nFrame0=data/test/a.dds\n");
        content.WriteBytes("data/test/a.dds", DdsTestData.CreateDxt3Dds(64, 64));

        MainHudQuickbarAssets assets = new(new ClientContentRoot(content.RootPath));

        object? first = assets.GetControlFrame0("ButtonA1");
        object? second = assets.GetControlFrame0("ButtonA1");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void ItemLookupFallsBackToItemDefault()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/ItemMinIcon.Ani", "[ItemDefault]\nFrameAmount=1\nFrame0=data/test/default.dds\n");
        content.WriteBytes("data/test/default.dds", DdsTestData.CreateDxt3Dds(64, 64));

        MainHudQuickbarAssets assets = new(new ClientContentRoot(content.RootPath));

        Assert.NotNull(assets.GetItemFrame(999999));
        Assert.Equal(64, assets.GetItemFrame(999999)!.Width);
        Assert.Equal(64, assets.GetItemFrame(999999)!.Height);
    }

    [Fact]
    public void CoverLoadsVerifiedSection()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", "[Compose_CoverPic]\nFrameAmount=1\nFrame0=data/test/cover.dds\n");
        content.WriteBytes("data/test/cover.dds", DdsTestData.CreateDxt3Dds(64, 64));

        MainHudQuickbarAssets assets = new(new ClientContentRoot(content.RootPath));

        Assert.NotNull(assets.GetCoverFrame());
        Assert.Equal(64, assets.GetCoverFrame()!.Width);
        Assert.Equal(64, assets.GetCoverFrame()!.Height);
    }
}
