using System.Buffers.Binary;
using System.Text;
using OpenConquer.Client.UI.Hud;
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
        content.WriteBytes("data/test/a.dds", CreateDxt3Dds(64, 64));

        MainHudQuickbarAssets assets = new(new ClientContentRoot(content.RootPath));

        Assert.Null(assets.GetControlFrame0("ButtonA1"));
    }

    [Fact]
    public void ItemLookupFallsBackToItemDefault()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/ItemMinIcon.Ani", "[ItemDefault]\nFrameAmount=1\nFrame0=data/test/default.dds\n");
        content.WriteBytes("data/test/default.dds", CreateDxt3Dds(64, 64));

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
        content.WriteBytes("data/test/cover.dds", CreateDxt3Dds(64, 64));

        MainHudQuickbarAssets assets = new(new ClientContentRoot(content.RootPath));

        Assert.NotNull(assets.GetCoverFrame());
        Assert.Equal(64, assets.GetCoverFrame()!.Width);
        Assert.Equal(64, assets.GetCoverFrame()!.Height);
    }

    private static byte[] CreateDxt3Dds(int width, int height)
    {
        const int Offset = 128;
        int encodedLength = checked(((width + 3) / 4) * ((height + 3) / 4) * 16);
        byte[] dds = new byte[Offset + encodedLength];

        BinaryPrimitives.WriteUInt32LittleEndian(dds, 0x20534444);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(4), 124);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(8), 0x00081007);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(12), checked((uint)height));
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(16), checked((uint)width));
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(20), checked((uint)encodedLength));
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(76), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(80), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(84), 0x33545844);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(108), 0x00001000);
        return dds;
    }

    private sealed class TemporaryContentDirectory : IDisposable
    {
        public TemporaryContentDirectory()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "OpenConquer.Client.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath
        {
            get;
        }

        public void WriteText(string relativePath, string contents) => WriteBytes(relativePath, Encoding.Latin1.GetBytes(contents));

        public void WriteBytes(string relativePath, ReadOnlySpan<byte> contents)
        {
            string path = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, contents);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
