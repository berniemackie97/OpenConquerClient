using OpenConquer.Client.UI.Hud.SelectedSkill;
using OpenConquer.Content;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudSelectedSkillAssetsTests
{
    [Fact]
    public void MissingCatalogs_ReturnMissingFrames()
    {
        using TemporaryContentDirectory content = new();
        MainHudSelectedSkillAssets assets = new(new ClientContentRoot(content.RootPath));

        Assert.Null(assets.GetSelectedFrame("Magic0", 0));
        Assert.Null(assets.GetCoverFrame());
    }

    [Fact]
    public void MissingSelectedSection_ReturnsMissingFrame()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Magic.ani", "[Magic0]\nFrameAmount=1\nFrame0=data/main/MainImgMagic.dds\n");
        content.WriteBytes("data/main/MainImgMagic.dds", DdsTestData.CreateDxt3Dds(64, 64));

        MainHudSelectedSkillAssets assets = new(new ClientContentRoot(content.RootPath));

        Assert.Null(assets.GetSelectedFrame("MagicSkillType1000", 0));
    }

    [Fact]
    public void ZeroFrameSelectedSection_ReturnsMissingFrame()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Magic.ani", "[MagicSkillType1270]\nFrameAmount=0\nFrame0=data/main/Relive01.dds\nFrame1=data/main/Relive02.dds\n");

        MainHudSelectedSkillAssets assets = new(new ClientContentRoot(content.RootPath));

        Assert.Null(assets.GetSelectedFrame("MagicSkillType1270", 0));
    }

    [Fact]
    public void MissingUnselectedFrame_DoesNotSuppressSelectedFrame()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Magic.ani",
            "[MagicSkillType1000]\nFrameAmount=2\n"
            + "Frame0=data/main/a.dds\n"
            + "Frame1=data/main/missing.dds\n");

        content.WriteBytes("data/main/a.dds", DdsTestData.CreateDxt3Dds(64, 64));

        MainHudSelectedSkillAssets assets = new(new ClientContentRoot(content.RootPath));

        Assert.NotNull(assets.GetSelectedFrame("MagicSkillType1000", 0));
    }

    [Fact]
    public void MissingSelectedFrame_ReturnsMissingFrame()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Magic.ani",
            "[MagicSkillType1000]\nFrameAmount=2\n"
            + "Frame0=data/main/a.dds\n"
            + "Frame1=data/main/missing.dds\n");

        content.WriteBytes("data/main/a.dds", DdsTestData.CreateDxt3Dds(64, 64));

        MainHudSelectedSkillAssets assets = new(new ClientContentRoot(content.RootPath));

        Assert.Null(assets.GetSelectedFrame("MagicSkillType1000", 1));
    }

    [Fact]
    public void SelectedFrame_UsesNativeUnsignedModuloSelection()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Magic.ani",
            "[MagicSkillType1000]\nFrameAmount=2\n"
            + "Frame0=data/main/a.dds\n"
            + "Frame1=data/main/b.dds\n");

        content.WriteBytes("data/main/a.dds", DdsTestData.CreateDxt3Dds(50, 50));
        content.WriteBytes("data/main/b.dds", DdsTestData.CreateDxt3Dds(52, 52));

        MainHudSelectedSkillAssets assets = new(new ClientContentRoot(content.RootPath));

        Assert.Equal(52, assets.GetSelectedFrame("MagicSkillType1000", 1)!.Width);
        Assert.Equal(50, assets.GetSelectedFrame("MagicSkillType1000", 2)!.Width);
        Assert.Equal(52, assets.GetSelectedFrame("MagicSkillType1000", uint.MaxValue)!.Width);
    }

    [Fact]
    public void SelectedFramesAreNotRetainedAcrossLookups()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Magic.ani", "[Magic0]\nFrameAmount=1\nFrame0=data/main/MainImgMagic.dds\n");
        content.WriteBytes("data/main/MainImgMagic.dds", DdsTestData.CreateDxt3Dds(64, 64));

        MainHudSelectedSkillAssets assets = new(new ClientContentRoot(content.RootPath));

        object? first = assets.GetSelectedFrame("Magic0", 0);
        object? second = assets.GetSelectedFrame("Magic0", 0);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void CoverFrame_LoadsNativeImage0Section()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", "[Image0]\nFrameAmount=1\nFrame0=data/main/ImageDisable.dds\n");
        content.WriteBytes("data/main/ImageDisable.dds", DdsTestData.CreateDxt3Dds(64, 64));

        MainHudSelectedSkillAssets assets = new(new ClientContentRoot(content.RootPath));

        Assert.NotNull(assets.GetCoverFrame());
        Assert.Equal(64, assets.GetCoverFrame()!.Width);
        Assert.Equal(64, assets.GetCoverFrame()!.Height);
    }
}
