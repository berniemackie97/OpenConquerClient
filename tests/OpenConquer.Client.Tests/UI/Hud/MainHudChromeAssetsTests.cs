using OpenConquer.Client.UI.Hud.Chrome;
using OpenConquer.Content;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudChromeAssetsTests
{
    [Fact]
    public void Load_MissingControlAni_ReturnsUnavailableChrome()
    {
        using TemporaryContentDirectory content = new();

        MainHudChromeAssets assets = MainHudChromeAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.Null(assets.ProgressBackground);
        Assert.Null(assets.DialogFrame0);
        Assert.Null(assets.DialogFrame1);
        Assert.False(assets.HasDialogPanels);
    }

    [Fact]
    public void Load_MissingProgressSection_DoesNotPreventDialogPanels()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", DialogSection());
        content.WriteBytes("data/main/mainDialog1.dds", DdsTestData.CreateDxt3Dds(256, 256));
        content.WriteBytes("data/main/mainDialog2.dds", DdsTestData.CreateDxt3Dds(256, 128));

        MainHudChromeAssets assets = MainHudChromeAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.Null(assets.ProgressBackground);
        Assert.NotNull(assets.DialogFrame0);
        Assert.NotNull(assets.DialogFrame1);
        Assert.True(assets.HasDialogPanels);
    }

    [Fact]
    public void Load_MissingDialogSection_DoesNotPreventProgressBackground()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", ProgressSection());
        content.WriteBytes("data/main/ProgressBk.dds", DdsTestData.CreateDxt3Dds(256, 256));

        MainHudChromeAssets assets = MainHudChromeAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.NotNull(assets.ProgressBackground);
        Assert.Null(assets.DialogFrame0);
        Assert.Null(assets.DialogFrame1);
        Assert.False(assets.HasDialogPanels);
    }

    [Fact]
    public void Load_MissingDialogFrame_DisablesDialogPanelsAsAUnit()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", ProgressSection() + DialogSection());
        content.WriteBytes("data/main/ProgressBk.dds", DdsTestData.CreateDxt3Dds(256, 256));
        content.WriteBytes("data/main/mainDialog1.dds", DdsTestData.CreateDxt3Dds(256, 256));

        MainHudChromeAssets assets = MainHudChromeAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.NotNull(assets.ProgressBackground);
        Assert.Null(assets.DialogFrame0);
        Assert.Null(assets.DialogFrame1);
        Assert.False(assets.HasDialogPanels);
    }

    [Theory]
    [InlineData("[Progress45]\nFrameAmount=2\nFrame0=data/main/ProgressBk.dds\nFrame1=data/main/Unused.dds\n", "Progress45")]
    [InlineData("[Dialog4]\nFrameAmount=1\nFrame0=data/main/mainDialog1.dds\n", "Dialog4")]
    public void Load_RejectsUnexpectedVerifiedFrameCounts(string controlAni, string sectionName)
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", controlAni);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => MainHudChromeAssets.Load(new ClientContentRoot(content.RootPath)));

        Assert.Contains($"[{sectionName}]", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Progress45", 128, 256)]
    [InlineData("Progress45", 256, 128)]
    [InlineData("Dialog4.Frame0", 128, 256)]
    [InlineData("Dialog4.Frame1", 256, 256)]
    public void Load_RejectsUnexpectedVerifiedDimensions(string target, int width, int height)
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", ProgressSection() + DialogSection());
        content.WriteBytes("data/main/ProgressBk.dds", DdsTestData.CreateDxt3Dds(target == "Progress45" ? width : 256, target == "Progress45" ? height : 256));
        content.WriteBytes("data/main/mainDialog1.dds", DdsTestData.CreateDxt3Dds(target == "Dialog4.Frame0" ? width : 256, target == "Dialog4.Frame0" ? height : 256));
        content.WriteBytes("data/main/mainDialog2.dds", DdsTestData.CreateDxt3Dds(target == "Dialog4.Frame1" ? width : 256, target == "Dialog4.Frame1" ? height : 128));

        Assert.Throws<InvalidDataException>(() => MainHudChromeAssets.Load(new ClientContentRoot(content.RootPath)));
    }

    [Fact]
    public void Load_LoadsTheVerifiedNativeAssetShapes()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", ProgressSection() + DialogSection());
        content.WriteBytes("data/main/ProgressBk.dds", DdsTestData.CreateDxt3Dds(256, 256));
        content.WriteBytes("data/main/mainDialog1.dds", DdsTestData.CreateDxt3Dds(256, 256));
        content.WriteBytes("data/main/mainDialog2.dds", DdsTestData.CreateDxt3Dds(256, 128));

        MainHudChromeAssets assets = MainHudChromeAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.Equal(256, assets.ProgressBackground!.Width);
        Assert.Equal(256, assets.ProgressBackground.Height);
        Assert.Equal(256, assets.DialogFrame0!.Width);
        Assert.Equal(256, assets.DialogFrame0.Height);
        Assert.Equal(256, assets.DialogFrame1!.Width);
        Assert.Equal(128, assets.DialogFrame1.Height);
        Assert.True(assets.HasDialogPanels);
    }

    private static string ProgressSection()
    {
        return "[Progress45]\nFrameAmount=1\nFrame0=data/main/ProgressBk.dds\n";
    }

    private static string DialogSection()
    {
        return "[Dialog4]\nFrameAmount=2\nFrame0=data/main/mainDialog1.dds\nFrame1=data/main/mainDialog2.dds\n";
    }
}
