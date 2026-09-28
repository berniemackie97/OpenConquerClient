using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using OpenConquer.Client.UI.Hud;
using OpenConquer.Content;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudOrganiseButtonAssetsTests
{
    [Fact]
    public void Load_MissingControlAni_ReturnsUnavailable()
    {
        using TemporaryContentDirectory content = new();

        MainHudOrganiseButtonAssets assets = MainHudOrganiseButtonAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.False(assets.IsAvailable);
    }

    [Fact]
    public void Load_MissingSection_ReturnsUnavailable()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", "[Other]\nFrameAmount=1\nFrame0=data/main/other.dds\n");

        MainHudOrganiseButtonAssets assets = MainHudOrganiseButtonAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.False(assets.IsAvailable);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Load_RejectsUnexpectedFrameCount(int frameCount)
    {
        using TemporaryContentDirectory content = new();

        StringBuilder section = new();
        section.AppendLine("[Main3_OrganiseBtn]");
        section.Append("FrameAmount=").Append(frameCount.ToString(CultureInfo.InvariantCulture)).Append('\n');

        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            section.Append("Frame").Append(frameIndex.ToString(CultureInfo.InvariantCulture))
                .Append("=data/main/frame").Append(frameIndex.ToString(CultureInfo.InvariantCulture)).AppendLine(".dds");
        }

        content.WriteText("ani/Control.ani", section.ToString());

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => MainHudOrganiseButtonAssets.Load(new ClientContentRoot(content.RootPath)));

        Assert.Contains("[Main3_OrganiseBtn]", exception.Message, StringComparison.Ordinal);
        Assert.Contains("exactly 4 frames", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Load_MissingRequiredFrameDisablesEntireControl(int missingFrameIndex)
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", Section());
        WriteFrames(content, missingFrameIndex: missingFrameIndex);

        MainHudOrganiseButtonAssets assets = MainHudOrganiseButtonAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.False(assets.IsAvailable);
    }

    [Theory]
    [InlineData(0, 32, 32)]
    [InlineData(0, 64, 64)]
    [InlineData(1, 32, 32)]
    [InlineData(1, 64, 64)]
    [InlineData(2, 32, 32)]
    [InlineData(2, 64, 64)]
    [InlineData(3, 32, 32)]
    [InlineData(3, 64, 64)]
    public void Load_RejectsUnexpectedFrameDimensions(int malformedFrameIndex, int width, int height)
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", Section());
        WriteFrames(content, malformedFrameIndex: malformedFrameIndex, malformedWidth: width, malformedHeight: height);

        Assert.Throws<InvalidDataException>(
            () => MainHudOrganiseButtonAssets.Load(new ClientContentRoot(content.RootPath)));
    }

    [Fact]
    public void Load_LoadsAllFourVerifiedFrames()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", Section());
        WriteFrames(content);

        MainHudOrganiseButtonAssets assets = MainHudOrganiseButtonAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.True(assets.IsAvailable);

        for (int frameIndex = 0; frameIndex < 4; frameIndex++)
        {
            Assert.NotNull(assets.GetFrame(frameIndex));
            Assert.Equal(64, assets.GetFrame(frameIndex)!.Width);
            Assert.Equal(32, assets.GetFrame(frameIndex)!.Height);
        }
    }

    [Fact]
    public void FrameAccess_RejectsIndicesOutsideNativeFrameCount()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", Section());
        WriteFrames(content);

        MainHudOrganiseButtonAssets assets = MainHudOrganiseButtonAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.Throws<ArgumentOutOfRangeException>(() => assets.GetFrame(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => assets.GetFrame(4));
    }

    private static string Section()
    {
        return """
            [Main3_OrganiseBtn]
            FrameAmount=4
            Frame0=data/main/organise0.dds
            Frame1=data/main/organise1.dds
            Frame2=data/main/organise2.dds
            Frame3=data/main/organise3.dds
            """;
    }

    private static void WriteFrames(
        TemporaryContentDirectory content,
        int missingFrameIndex = -1,
        int malformedFrameIndex = -1,
        int malformedWidth = 0,
        int malformedHeight = 0)
    {
        for (int frameIndex = 0; frameIndex < 4; frameIndex++)
        {
            if (frameIndex == missingFrameIndex)
            {
                continue;
            }

            int width = frameIndex == malformedFrameIndex ? malformedWidth : 64;
            int height = frameIndex == malformedFrameIndex ? malformedHeight : 32;

            content.WriteBytes($"data/main/organise{frameIndex}.dds", CreateDxt3Dds(width, height));
        }
    }

    private static byte[] CreateDxt3Dds(int width, int height)
    {
        const int pixelDataOffset = 128;

        int blockCountX = (width + 3) / 4;
        int blockCountY = (height + 3) / 4;
        int encodedLength = checked(blockCountX * blockCountY * 16);
        byte[] dds = new byte[pixelDataOffset + encodedLength];

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

        public void WriteText(string relativePath, string contents)
        {
            WriteBytes(relativePath, Encoding.Latin1.GetBytes(contents));
        }

        public void WriteBytes(string relativePath, ReadOnlySpan<byte> contents)
        {
            string path = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException($"'{relativePath}' has no parent directory.");

            Directory.CreateDirectory(directory);
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
