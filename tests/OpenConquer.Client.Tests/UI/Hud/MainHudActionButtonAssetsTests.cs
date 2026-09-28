using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using OpenConquer.Client.UI.Hud;
using OpenConquer.Content;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudActionButtonAssetsTests
{
    [Fact]
    public void Load_MissingControlAni_ReturnsUnavailable()
    {
        using TemporaryContentDirectory content = new();
        MainHudActionButtonAssets assets = MainHudActionButtonAssets.Load(new ClientContentRoot(content.RootPath));

        foreach (MainHudActionButtonDefinition definition in MainHudActionButtonDefinitions.NativeDrawOrder)
            Assert.False(assets.IsAvailable(definition.Id));

        foreach (MainHudPkButtonSkin skin in Enum.GetValues<MainHudPkButtonSkin>())
            Assert.False(assets.IsPkSkinAvailable(skin));
    }

    [Fact]
    public void Load_LoadsAllNativeButtonAndPkSkinFrames()
    {
        using TemporaryContentDirectory content = new();
        WriteCompleteContent(content);

        MainHudActionButtonAssets assets = MainHudActionButtonAssets.Load(new ClientContentRoot(content.RootPath));

        foreach (MainHudActionButtonDefinition definition in MainHudActionButtonDefinitions.NativeDrawOrder)
        {
            Assert.True(assets.IsAvailable(definition.Id));

            for (int frameIndex = 0; frameIndex < definition.ExpectedFrameCount; frameIndex++)
            {
                Assert.NotNull(assets.GetFrame(definition.Id, frameIndex));
                Assert.Equal(64, assets.GetFrame(definition.Id, frameIndex)!.Width);
                Assert.Equal(32, assets.GetFrame(definition.Id, frameIndex)!.Height);
            }
        }

        foreach (MainHudPkButtonSkin skin in Enum.GetValues<MainHudPkButtonSkin>())
        {
            Assert.True(assets.IsPkSkinAvailable(skin));
            Assert.NotNull(assets.GetPkFrame(skin, 0));
            Assert.NotNull(assets.GetPkFrame(skin, 1));
        }
    }

    [Fact]
    public void Load_MissingSectionDisablesOnlyThatControl()
    {
        using TemporaryContentDirectory content = new();
        WriteCompleteContent(content, omittedSection: "Button42");

        MainHudActionButtonAssets assets = MainHudActionButtonAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.False(assets.IsAvailable(MainHudActionButtonId.Button42));
        Assert.True(assets.IsAvailable(MainHudActionButtonId.Button40));
        Assert.True(assets.IsAvailable(MainHudActionButtonId.Main3OrganiseBtn));
        Assert.True(assets.IsPkSkinAvailable(MainHudPkButtonSkin.Button49));
    }

    [Fact]
    public void Load_MissingRequiredFrameDisablesOnlyThatSection()
    {
        using TemporaryContentDirectory content = new();
        WriteCompleteContent(content, missingFrameSection: "Button41", missingFrameIndex: 2);

        MainHudActionButtonAssets assets = MainHudActionButtonAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.False(assets.IsAvailable(MainHudActionButtonId.Button41));
        Assert.True(assets.IsAvailable(MainHudActionButtonId.Button40));
        Assert.True(assets.IsAvailable(MainHudActionButtonId.Main3OrganiseBtn));
    }

    [Theory]
    [InlineData("Button40", 1)]
    [InlineData("Button41", 2)]
    [InlineData("Main3_MissionBtn", 4)]
    [InlineData("Main3_OrganiseBtn", 3)]
    [InlineData("Button49", 3)]
    public void Load_RejectsUnexpectedFrameCount(string sectionName, int frameCount)
    {
        using TemporaryContentDirectory content = new();
        WriteCompleteContent(content, overriddenFrameCountSection: sectionName, overriddenFrameCount: frameCount);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => MainHudActionButtonAssets.Load(new ClientContentRoot(content.RootPath)));

        Assert.Contains($"[{sectionName}]", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Button40", 0, 32, 32)]
    [InlineData("Button41", 2, 64, 64)]
    [InlineData("Main3_MissionBtn", 2, 32, 32)]
    [InlineData("Main3_OrganiseBtn", 3, 64, 64)]
    [InlineData("Button412", 1, 32, 32)]
    public void Load_RejectsUnexpectedFrameDimensions(string sectionName, int frameIndex, int width, int height)
    {
        using TemporaryContentDirectory content = new();
        WriteCompleteContent(content, malformedSection: sectionName, malformedFrameIndex: frameIndex, malformedWidth: width, malformedHeight: height);

        Assert.Throws<InvalidDataException>(() => MainHudActionButtonAssets.Load(new ClientContentRoot(content.RootPath)));
    }

    [Fact]
    public void FrameAccess_RejectsIndicesOutsideNativeFrameCounts()
    {
        using TemporaryContentDirectory content = new();
        WriteCompleteContent(content);

        MainHudActionButtonAssets assets = MainHudActionButtonAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.Throws<ArgumentOutOfRangeException>(() => assets.GetFrame(MainHudActionButtonId.Button40, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => assets.GetFrame(MainHudActionButtonId.Button40, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => assets.GetFrame(MainHudActionButtonId.Main3OrganiseBtn, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => assets.GetPkFrame(MainHudPkButtonSkin.Button47, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => assets.GetPkFrame(MainHudPkButtonSkin.Button47, 2));
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
        Dictionary<string, int> sections = new(StringComparer.Ordinal);

        foreach (MainHudActionButtonDefinition definition in MainHudActionButtonDefinitions.NativeDrawOrder)
            sections[definition.AniSectionName] = definition.ExpectedFrameCount;

        foreach (MainHudPkButtonSkin skin in Enum.GetValues<MainHudPkButtonSkin>())
            sections.TryAdd(MainHudPkButtonSkins.GetAniSectionName(skin), 2);

        StringBuilder ani = new();

        foreach ((string sectionName, int nativeFrameCount) in sections)
        {
            if (sectionName == omittedSection)
            {
                continue;
            }

            int declaredFrameCount = sectionName == overriddenFrameCountSection ? overriddenFrameCount : nativeFrameCount;

            ani.Append('[').Append(sectionName).AppendLine("]");
            ani.Append("FrameAmount=").Append(declaredFrameCount.ToString(CultureInfo.InvariantCulture)).Append('\n');

            for (int frameIndex = 0; frameIndex < declaredFrameCount; frameIndex++)
            {
                string path = FramePath(sectionName, frameIndex);
                ani.Append("Frame").Append(frameIndex.ToString(CultureInfo.InvariantCulture)).Append('=').Append(path).Append('\n');

                if (sectionName == missingFrameSection && frameIndex == missingFrameIndex)
                {
                    continue;
                }

                int width = sectionName == malformedSection && frameIndex == malformedFrameIndex ? malformedWidth : 64;
                int height = sectionName == malformedSection && frameIndex == malformedFrameIndex ? malformedHeight : 32;
                content.WriteBytes(path, CreateDxt3Dds(width, height));
            }

            ani.Append('\n');
        }

        content.WriteText("ani/Control.ani", ani.ToString());
    }

    private static string FramePath(string sectionName, int frameIndex) => $"data/test/{sectionName}_{frameIndex}.dds";

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
