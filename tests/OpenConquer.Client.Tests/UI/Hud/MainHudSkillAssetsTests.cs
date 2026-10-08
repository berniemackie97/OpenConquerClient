using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using OpenConquer.Client.UI.Hud.SkillExperience;
using OpenConquer.Content;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudSkillAssetsTests
{
    [Fact]
    public void Load_MissingControlAni_ReturnsUnavailableSkill()
    {
        using TemporaryContentDirectory content = new();

        MainHudSkillAssets assets = MainHudSkillAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.False(assets.HasSkill);
    }

    [Fact]
    public void Load_MissingProgress42Section_ReturnsUnavailableSkill()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", "[Progress41]\nFrameAmount=1\nFrame0=data/main/other.dds\n");

        MainHudSkillAssets assets = MainHudSkillAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.False(assets.HasSkill);
    }

    [Theory]
    [InlineData("data/main/ProgressPower.dds")]
    [InlineData("data/main/ProgressPowerH.dds")]
    public void Load_MissingRequiredAsset_DisablesEntireSkillResource(string missingPath)
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", SkillSection());
        WriteSkillAssets(content, missingPath);

        MainHudSkillAssets assets = MainHudSkillAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.False(assets.HasSkill);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void Load_RejectsUnexpectedVerifiedFrameCount(int frameCount)
    {
        using TemporaryContentDirectory content = new();

        StringBuilder section = new();
        section.AppendLine("[Progress42]");
        section.Append("FrameAmount=").Append(frameCount.ToString(CultureInfo.InvariantCulture)).Append('\n');

        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            section.Append("Frame").Append(frameIndex.ToString(CultureInfo.InvariantCulture)).Append("=data/main/frame").Append(frameIndex.ToString(CultureInfo.InvariantCulture)).AppendLine(".dds");
        }

        content.WriteText("ani/Control.ani", section.ToString());

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => MainHudSkillAssets.Load(new ClientContentRoot(content.RootPath)));

        Assert.Contains("[Progress42]", exception.Message, StringComparison.Ordinal);
        Assert.Contains("exactly 3 frames", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("data/main/ProgressPower.dds", 64, 128)]
    [InlineData("data/main/ProgressPower.dds", 128, 64)]
    [InlineData("data/main/ProgressPowerH.dds", 64, 128)]
    [InlineData("data/main/ProgressPowerH.dds", 128, 64)]
    public void Load_RejectsUnexpectedDimensionsForEveryUniqueNativeAsset(string malformedPath, int width, int height)
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", SkillSection());
        WriteSkillAssets(content, malformedPath: malformedPath, malformedWidth: width, malformedHeight: height);

        Assert.Throws<InvalidDataException>(() => MainHudSkillAssets.Load(new ClientContentRoot(content.RootPath)));
    }

    [Fact]
    public void Load_LoadsAllThreeVerifiedNativeFrames()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", SkillSection());
        WriteSkillAssets(content);

        MainHudSkillAssets assets = MainHudSkillAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.True(assets.HasSkill);
        AssertDimensions(assets.GetSkillFrame(0), 128, 128);
        AssertDimensions(assets.GetSkillFrame(1), 128, 128);
        AssertDimensions(assets.GetSkillFrame(2), 128, 128);
    }

    [Fact]
    public void Load_RetainsDuplicateProgressPowerAniEntries()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", SkillSection());
        WriteSkillAssets(content);

        MainHudSkillAssets assets = MainHudSkillAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.NotNull(assets.GetSkillFrame(0));
        Assert.NotNull(assets.GetSkillFrame(1));
    }

    [Fact]
    public void FrameAccess_RejectsIndicesOutsideNativeFrameCount()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", SkillSection());
        WriteSkillAssets(content);

        MainHudSkillAssets assets = MainHudSkillAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.Throws<ArgumentOutOfRangeException>(() => assets.GetSkillFrame(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => assets.GetSkillFrame(3));
    }

    private static string SkillSection()
    {
        return "[Progress42]\nFrameAmount=3\nFrame0=data/main/ProgressPower.dds\nFrame1=data/main/ProgressPower.dds\nFrame2=data/main/ProgressPowerH.dds\n";
    }

    private static void WriteSkillAssets(TemporaryContentDirectory content, string? missingPath = null, string? malformedPath = null, int malformedWidth = 0, int malformedHeight = 0)
    {
        WriteSkillAsset(content, "data/main/ProgressPower.dds", missingPath, malformedPath, malformedWidth, malformedHeight);
        WriteSkillAsset(content, "data/main/ProgressPowerH.dds", missingPath, malformedPath, malformedWidth, malformedHeight);
    }

    private static void WriteSkillAsset(TemporaryContentDirectory content, string path, string? missingPath, string? malformedPath, int malformedWidth, int malformedHeight)
    {
        if (string.Equals(path, missingPath, StringComparison.Ordinal))
        {
            return;
        }

        int width = 128;
        int height = 128;

        if (string.Equals(path, malformedPath, StringComparison.Ordinal))
        {
            width = malformedWidth;
            height = malformedHeight;
        }

        content.WriteBytes(path, CreateDxt3Dds(width, height));
    }

    private static void AssertDimensions(OpenConquer.Content.Images.RgbaImage? image, int width, int height)
    {
        Assert.NotNull(image);
        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
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
            string directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException($"'{relativePath}' has no parent directory.");

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
