using OpenConquer.Content.Configuration;

namespace OpenConquer.Content.Tests.Configuration;

public sealed class SelectedMagicCooldownTextConfigurationTests
{
    [Fact]
    public void Load_ReadsNativeSelectMagicNumValues()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(SelectedMagicCooldownTextConfiguration.RelativePath,
            """
            [SelectMagicNum]
            OffsetX=-7
            OffsetY=9
            FontSize=18
            Color=0xFF336699
            """);

        SelectedMagicCooldownTextConfiguration configuration = SelectedMagicCooldownTextConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(-7, configuration.OffsetX);
        Assert.Equal(9, configuration.OffsetY);
        Assert.Equal(18, configuration.FontSizePixels);
        Assert.Equal(0xFF336699u, configuration.ColorArgb);
    }

    [Fact]
    public void Load_WhenValuesAreMissingOrEmpty_UsesNativeDefaults()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(SelectedMagicCooldownTextConfiguration.RelativePath,
            """
            [SelectMagicNum]
            OffsetX=
            Color=
            """);

        SelectedMagicCooldownTextConfiguration configuration = SelectedMagicCooldownTextConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(SelectedMagicCooldownTextConfiguration.DefaultOffsetX, configuration.OffsetX);
        Assert.Equal(SelectedMagicCooldownTextConfiguration.DefaultOffsetY, configuration.OffsetY);
        Assert.Equal(SelectedMagicCooldownTextConfiguration.DefaultFontSizePixels, configuration.FontSizePixels);
        Assert.Equal(SelectedMagicCooldownTextConfiguration.DefaultColorArgb, configuration.ColorArgb);
    }

    [Theory]
    [InlineData("17pixels", 17)]
    [InlineData("-23tail", -23)]
    [InlineData("+31more", 31)]
    [InlineData("garbage", 0)]
    [InlineData("12", 12)]
    [InlineData("0x", 0)]
    public void Load_UsesNativeAtoiPrefixSemanticsForDecimalValues(string value, int expected)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(SelectedMagicCooldownTextConfiguration.RelativePath,
            $"""
            [SelectMagicNum]
            OffsetX={value}
            """);

        SelectedMagicCooldownTextConfiguration configuration = SelectedMagicCooldownTextConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(expected, configuration.OffsetX);
    }

    [Theory]
    [InlineData("0x2Ajunk", 42)]
    [InlineData("0XFFtail", 255)]
    [InlineData("0x7FFFFFFFmore", int.MaxValue)]
    [InlineData("0xG1", 0)]
    [InlineData("+0x10", 0)]
    public void Load_UsesNativeHexPrefixSemantics(string value, int expected)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(SelectedMagicCooldownTextConfiguration.RelativePath,
            $"""
            [SelectMagicNum]
            OffsetX={value}
            """);

        SelectedMagicCooldownTextConfiguration configuration = SelectedMagicCooldownTextConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(expected, configuration.OffsetX);
    }

    [Fact]
    public void Load_WhenHexConversionCannotAssign_PreservesCallerDefault()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(SelectedMagicCooldownTextConfiguration.RelativePath,
            """
            [SelectMagicNum]
            FontSize=0xG1
            """);

        SelectedMagicCooldownTextConfiguration configuration = SelectedMagicCooldownTextConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(SelectedMagicCooldownTextConfiguration.DefaultFontSizePixels, configuration.FontSizePixels);
    }

    [Theory]
    [InlineData("0xFFFFFFFF", 4294967295L)]
    [InlineData("0x80000000", 2147483648L)]
    [InlineData("-1", 4294967295L)]
    public void Load_PreservesNativeThirtyTwoBitColorPattern(string value, long expected)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(SelectedMagicCooldownTextConfiguration.RelativePath,
            $"""
            [SelectMagicNum]
            Color={value}
            """);

        SelectedMagicCooldownTextConfiguration configuration = SelectedMagicCooldownTextConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal((uint)expected, configuration.ColorArgb);
    }

    [Fact]
    public void Load_ResolvesPathSectionAndKeysCaseInsensitively()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("INI/InFo.InI",
            """
            [sElEcTmAgIcNuM]
            oFfSeTx=4
            oFfSeTy=-5
            fOnTsIzE=16
            cOlOr=0xFF102030
            """);

        SelectedMagicCooldownTextConfiguration configuration = SelectedMagicCooldownTextConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(4, configuration.OffsetX);
        Assert.Equal(-5, configuration.OffsetY);
        Assert.Equal(16, configuration.FontSizePixels);
        Assert.Equal(0xFF102030u, configuration.ColorArgb);
    }

    [Fact]
    public void Load_WhenInfoIniIsMissing_ThrowsFileNotFoundException()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);

        Assert.Throws<FileNotFoundException>(() => SelectedMagicCooldownTextConfiguration.Load(contentSource));
    }

    [Fact]
    public void Load_RejectsConfigurationBeyondSafetyLimit()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(SelectedMagicCooldownTextConfiguration.RelativePath, new byte[(64 * 1024) + 1]);

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);

        Assert.Throws<InvalidDataException>(() => SelectedMagicCooldownTextConfiguration.Load(contentSource));
    }
}
