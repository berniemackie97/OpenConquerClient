using System.Buffers.Binary;
using System.Text;
using OpenConquer.Client.UI.Hud;
using OpenConquer.Content;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudVitalsAssetsTests
{
    [Fact]
    public void Load_MissingControlAni_ReturnsUnavailableVitals()
    {
        using TemporaryContentDirectory content = new();

        MainHudVitalsAssets assets = MainHudVitalsAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.False(assets.HasLife);
        Assert.False(assets.HasMana);
        Assert.False(assets.HasStamina);
        Assert.False(assets.HasExtendedStamina);
    }

    [Fact]
    public void Load_MissingSection_DoesNotPreventOtherGauges()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", ManaSection() + StaminaSection() + ExtendedStaminaSection());
        WriteAllGaugeAssets(content);

        MainHudVitalsAssets assets = MainHudVitalsAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.False(assets.HasLife);
        Assert.True(assets.HasMana);
        Assert.True(assets.HasStamina);
        Assert.True(assets.HasExtendedStamina);
    }

    [Theory]
    [InlineData("data/main/ProgressHPH.dds", false, true, true, true)]
    [InlineData("data/main/ProgressMPA.dds", true, false, true, true)]
    [InlineData("data/main/ProgressForceA.dds", true, true, false, true)]
    [InlineData("data/main/ProgressForce2A.dds", true, true, true, false)]
    public void Load_MissingFrame_DisablesOnlyItsGauge(string missingPath, bool hasLife, bool hasMana, bool hasStamina, bool hasExtendedStamina)
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", AllSections());
        WriteAllGaugeAssets(content, missingPath);

        MainHudVitalsAssets assets = MainHudVitalsAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.Equal(hasLife, assets.HasLife);
        Assert.Equal(hasMana, assets.HasMana);
        Assert.Equal(hasStamina, assets.HasStamina);
        Assert.Equal(hasExtendedStamina, assets.HasExtendedStamina);
    }

    [Theory]
    [InlineData("[Progress40]\nFrameAmount=2\nFrame0=data/main/a.dds\nFrame1=data/main/b.dds\n", "Progress40", 3)]
    [InlineData("[Progress41]\nFrameAmount=2\nFrame0=data/main/a.dds\nFrame1=data/main/b.dds\n", "Progress41", 3)]
    [InlineData("[Progress46]\nFrameAmount=1\nFrame0=data/main/a.dds\n", "Progress46", 2)]
    [InlineData("[Progress47]\nFrameAmount=1\nFrame0=data/main/a.dds\n", "Progress47", 2)]
    public void Load_RejectsUnexpectedVerifiedFrameCounts(string controlAni, string sectionName, int expectedFrameCount)
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", controlAni);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => MainHudVitalsAssets.Load(new ClientContentRoot(content.RootPath)));

        Assert.Contains($"[{sectionName}]", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"exactly {expectedFrameCount} frames", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("data/main/ProgressHP.dds", 64, 128)]
    [InlineData("data/main/ProgressHPA.dds", 128, 64)]
    [InlineData("data/main/ProgressHPH.dds", 64, 64)]
    [InlineData("data/main/ProgressMP.dds", 64, 128)]
    [InlineData("data/main/ProgressMPA.dds", 128, 64)]
    [InlineData("data/main/ProgressMPH.dds", 64, 64)]
    [InlineData("data/main/ProgressForce.dds", 64, 128)]
    [InlineData("data/main/ProgressForceA.dds", 128, 64)]
    [InlineData("data/main/ProgressForce2.dds", 16, 32)]
    [InlineData("data/main/ProgressForce2A.dds", 32, 16)]
    public void Load_RejectsUnexpectedDimensionsForEveryDeclaredFrame(string malformedPath, int width, int height)
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", AllSections());
        WriteAllGaugeAssets(content, malformedPath: malformedPath, malformedWidth: width, malformedHeight: height);

        Assert.Throws<InvalidDataException>(() => MainHudVitalsAssets.Load(new ClientContentRoot(content.RootPath)));
    }

    [Fact]
    public void Load_LoadsAndRetainsAllTenVerifiedNativeFrames()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", AllSections());
        WriteAllGaugeAssets(content);

        MainHudVitalsAssets assets = MainHudVitalsAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.True(assets.HasLife);
        Assert.True(assets.HasMana);
        Assert.True(assets.HasStamina);
        Assert.True(assets.HasExtendedStamina);

        AssertDimensions(assets.GetLifeFrame(0), 128, 128);
        AssertDimensions(assets.GetLifeFrame(1), 128, 128);
        AssertDimensions(assets.GetLifeFrame(2), 128, 128);
        AssertDimensions(assets.GetManaFrame(0), 128, 128);
        AssertDimensions(assets.GetManaFrame(1), 128, 128);
        AssertDimensions(assets.GetManaFrame(2), 128, 128);
        AssertDimensions(assets.GetStaminaFrame(0), 128, 128);
        AssertDimensions(assets.GetStaminaFrame(1), 128, 128);
        AssertDimensions(assets.GetExtendedStaminaFrame(0), 32, 32);
        AssertDimensions(assets.GetExtendedStaminaFrame(1), 32, 32);
    }

    [Fact]
    public void Load_ResolvesRetailProgressForce2APathCaseInsensitively()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", AllSections());
        WriteAllGaugeAssets(content, extendedStaminaAlternatePath: "data/main/ProgressForce2a.dds");

        MainHudVitalsAssets assets = MainHudVitalsAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.True(assets.HasExtendedStamina);
        AssertDimensions(assets.GetExtendedStaminaFrame(1), 32, 32);
    }

    [Fact]
    public void FrameAccess_RejectsIndicesOutsideDeclaredNativeFrameCounts()
    {
        using TemporaryContentDirectory content = new();

        content.WriteText("ani/Control.ani", AllSections());
        WriteAllGaugeAssets(content);

        MainHudVitalsAssets assets = MainHudVitalsAssets.Load(new ClientContentRoot(content.RootPath));

        Assert.Throws<ArgumentOutOfRangeException>(() => assets.GetLifeFrame(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => assets.GetManaFrame(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => assets.GetStaminaFrame(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => assets.GetExtendedStaminaFrame(2));
    }

    private static string AllSections() => LifeSection() + ManaSection() + StaminaSection() + ExtendedStaminaSection();

    private static string LifeSection()
    {
        return "[Progress40]\nFrameAmount=3\nFrame0=data/main/ProgressHP.dds\nFrame1=data/main/ProgressHPA.dds\nFrame2=data/main/ProgressHPH.dds\n";
    }

    private static string ManaSection()
    {
        return "[Progress41]\nFrameAmount=3\nFrame0=data/main/ProgressMP.dds\nFrame1=data/main/ProgressMPA.dds\nFrame2=data/main/ProgressMPH.dds\n";
    }

    private static string StaminaSection()
    {
        return "[Progress46]\nFrameAmount=2\nFrame0=data/main/ProgressForce.dds\nFrame1=data/main/ProgressForceA.dds\n";
    }

    private static string ExtendedStaminaSection()
    {
        return "[Progress47]\nFrameAmount=2\nFrame0=data/main/ProgressForce2.dds\nFrame1=data/main/ProgressForce2A.dds\n";
    }

    private static void WriteAllGaugeAssets(TemporaryContentDirectory content, string? missingPath = null, string? malformedPath = null, int malformedWidth = 0, int malformedHeight = 0, string extendedStaminaAlternatePath = "data/main/ProgressForce2A.dds")
    {
        WriteGaugeAsset(content, "data/main/ProgressHP.dds", 128, 128, missingPath, malformedPath, malformedWidth, malformedHeight);
        WriteGaugeAsset(content, "data/main/ProgressHPA.dds", 128, 128, missingPath, malformedPath, malformedWidth, malformedHeight);
        WriteGaugeAsset(content, "data/main/ProgressHPH.dds", 128, 128, missingPath, malformedPath, malformedWidth, malformedHeight);
        WriteGaugeAsset(content, "data/main/ProgressMP.dds", 128, 128, missingPath, malformedPath, malformedWidth, malformedHeight);
        WriteGaugeAsset(content, "data/main/ProgressMPA.dds", 128, 128, missingPath, malformedPath, malformedWidth, malformedHeight);
        WriteGaugeAsset(content, "data/main/ProgressMPH.dds", 128, 128, missingPath, malformedPath, malformedWidth, malformedHeight);
        WriteGaugeAsset(content, "data/main/ProgressForce.dds", 128, 128, missingPath, malformedPath, malformedWidth, malformedHeight);
        WriteGaugeAsset(content, "data/main/ProgressForceA.dds", 128, 128, missingPath, malformedPath, malformedWidth, malformedHeight);
        WriteGaugeAsset(content, "data/main/ProgressForce2.dds", 32, 32, missingPath, malformedPath, malformedWidth, malformedHeight);
        WriteGaugeAsset(content, extendedStaminaAlternatePath, 32, 32, missingPath, malformedPath, malformedWidth, malformedHeight, comparisonPath: "data/main/ProgressForce2A.dds");
    }

    private static void WriteGaugeAsset(TemporaryContentDirectory content, string path, int width, int height, string? missingPath, string? malformedPath, int malformedWidth, int malformedHeight, string? comparisonPath = null)
    {
        string logicalPath = comparisonPath ?? path;

        if (string.Equals(logicalPath, missingPath, StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(logicalPath, malformedPath, StringComparison.Ordinal))
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
