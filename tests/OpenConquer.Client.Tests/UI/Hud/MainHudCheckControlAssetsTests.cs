using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using OpenConquer.Client.UI.Hud;
using OpenConquer.Content;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudCheckControlAssetsTests
{
    [Fact]
    public void Load_MissingControlAni_ReturnsAllControlsUnavailable()
    {
        using TemporaryContentDirectory content = new();

        MainHudCheckControlAssets assets = MainHudCheckControlAssets.Load(new ClientContentRoot(content.RootPath));

        foreach (MainHudCheckControlDefinition definition in MainHudCheckControlDefinitions.NativeDrawOrder)
        {
            Assert.False(assets.IsAvailable(definition.Id));
        }
    }

    [Fact]
    public void Load_LoadsAllNativeControlFrames()
    {
        using TemporaryContentDirectory content = new();
        WriteCompleteContent(content);

        MainHudCheckControlAssets assets = MainHudCheckControlAssets.Load(new ClientContentRoot(content.RootPath));

        foreach (MainHudCheckControlDefinition definition in MainHudCheckControlDefinitions.NativeDrawOrder)
        {
            Assert.True(assets.IsAvailable(definition.Id));

            for (int frameIndex = 0; frameIndex < MainHudCheckControlDefinitions.FrameCount; frameIndex++)
            {
                Assert.NotNull(assets.GetFrame(definition.Id, frameIndex));
                Assert.Equal(32, assets.GetFrame(definition.Id, frameIndex)!.Width);
                Assert.Equal(32, assets.GetFrame(definition.Id, frameIndex)!.Height);
            }
        }
    }

    [Fact]
    public void Load_MissingSectionDisablesOnlyThatControl()
    {
        using TemporaryContentDirectory content = new();
        WriteCompleteContent(content, omittedSection: "Check43");

        MainHudCheckControlAssets assets = MainHudCheckControlAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.True(assets.IsAvailable(MainHudCheckControlId.Check40));
        Assert.False(assets.IsAvailable(MainHudCheckControlId.Check43));
        Assert.True(assets.IsAvailable(MainHudCheckControlId.Check46));
        Assert.True(assets.IsAvailable(MainHudCheckControlId.Button411));
    }

    [Fact]
    public void Load_MissingRequiredFrameDisablesOnlyThatControl()
    {
        using TemporaryContentDirectory content = new();
        WriteCompleteContent(content, missingFrameSection: "Check46", missingFrameIndex: 1);

        MainHudCheckControlAssets assets = MainHudCheckControlAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.True(assets.IsAvailable(MainHudCheckControlId.Check40));
        Assert.True(assets.IsAvailable(MainHudCheckControlId.Check43));
        Assert.False(assets.IsAvailable(MainHudCheckControlId.Check46));
        Assert.True(assets.IsAvailable(MainHudCheckControlId.Button411));
    }

    [Theory]
    [InlineData("Check40", 1)]
    [InlineData("Check43", 3)]
    [InlineData("Check46", 0)]
    [InlineData("Button411", 4)]
    public void Load_RejectsUnexpectedFrameCount(string sectionName, int frameCount)
    {
        using TemporaryContentDirectory content = new();
        WriteCompleteContent(content, overriddenFrameCountSection: sectionName, overriddenFrameCount: frameCount);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => MainHudCheckControlAssets.Load(new ClientContentRoot(content.RootPath)));

        Assert.Contains($"[{sectionName}]", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Check40", 0, 64, 32)]
    [InlineData("Check43", 1, 32, 64)]
    [InlineData("Check46", 0, 16, 32)]
    [InlineData("Button411", 1, 64, 64)]
    public void Load_RejectsUnexpectedFrameDimensions(string sectionName, int frameIndex, int width, int height)
    {
        using TemporaryContentDirectory content = new();
        WriteCompleteContent(content, malformedSection: sectionName, malformedFrameIndex: frameIndex, malformedWidth: width, malformedHeight: height);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => MainHudCheckControlAssets.Load(new ClientContentRoot(content.RootPath)));

        Assert.Contains($"[{sectionName}]", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"{width}x{height}", exception.Message, StringComparison.Ordinal);
        Assert.Contains("32x32", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GetFrame_RejectsFramesOutsideNativeStateRange(int frameIndex)
    {
        using TemporaryContentDirectory content = new();
        WriteCompleteContent(content);

        MainHudCheckControlAssets assets = MainHudCheckControlAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.Throws<ArgumentOutOfRangeException>(() => assets.GetFrame(MainHudCheckControlId.Check40, frameIndex));
    }

    private static void WriteCompleteContent(
        TemporaryContentDirectory content,
        string? omittedSection = null,
        string? missingFrameSection = null,
        int missingFrameIndex = -1,
        string? overriddenFrameCountSection = null,
        int overriddenFrameCount = -1,
        string? malformedSection = null,
        int malformedFrameIndex = -1,
        int malformedWidth = 0,
        int malformedHeight = 0)
    {
        StringBuilder ani = new();

        foreach (MainHudCheckControlDefinition definition in MainHudCheckControlDefinitions.NativeDrawOrder)
        {
            if (definition.AniSectionName == omittedSection)
            {
                continue;
            }

            int declaredFrameCount = definition.AniSectionName == overriddenFrameCountSection
                ? overriddenFrameCount
                : MainHudCheckControlDefinitions.FrameCount;

            ani.Append('[').Append(definition.AniSectionName).AppendLine("]");
            ani.Append("FrameAmount=").Append(declaredFrameCount.ToString(CultureInfo.InvariantCulture)).Append('\n');

            for (int frameIndex = 0; frameIndex < declaredFrameCount; frameIndex++)
            {
                string path = FramePath(definition.AniSectionName, frameIndex);
                ani.Append("Frame").Append(frameIndex.ToString(CultureInfo.InvariantCulture)).Append('=').Append(path).Append('\n');

                if (definition.AniSectionName == missingFrameSection && frameIndex == missingFrameIndex)
                {
                    continue;
                }

                int width = definition.AniSectionName == malformedSection && frameIndex == malformedFrameIndex ? malformedWidth : 32;
                int height = definition.AniSectionName == malformedSection && frameIndex == malformedFrameIndex ? malformedHeight : 32;
                content.WriteBytes(path, CreateDxt3Dds(width, height));
            }

            ani.Append('\n');
        }

        content.WriteText("ani/Control.ani", ani.ToString());
    }

    private static string FramePath(string sectionName, int frameIndex) => $"data/test/{sectionName}_{frameIndex}.dds";

    private static byte[] CreateDxt3Dds(int width, int height)
    {
        const int PixelDataOffset = 128;
        int blockCountX = (width + 3) / 4;
        int blockCountY = (height + 3) / 4;
        int encodedLength = checked(blockCountX * blockCountY * 16);
        byte[] dds = new byte[PixelDataOffset + encodedLength];

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
