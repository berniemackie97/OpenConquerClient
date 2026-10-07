using OpenConquer.Content.Configuration;

namespace OpenConquer.Content.Tests.Configuration;

public sealed class ClientFontSettingsConfigurationTests
{
    [Fact]
    public void Load_WhenFontSettingIsMissing_UsesGameFontAndNativeDefaults()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(GameFontConfiguration.RelativePath, "Arial 12");

        ClientFontSettingsConfiguration configuration = ClientFontSettingsConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(ClientFontSettingsConfiguration.DefaultChatFontHeightPixels, configuration.ChatFontHeightPixels);
        Assert.Equal(ClientFontSettingsConfiguration.NormalRenderTextStyle, configuration.DefaultRenderTextStyle);
        Assert.False(configuration.AntialiasEnabled);
        Assert.False(configuration.GuiFontShadowEnabled);
        Assert.Equal("Arial", configuration.ChatFontFaceName);
        Assert.Equal("Arial", configuration.GuiFontFaceName);
        Assert.Equal(ClientFontSettingsConfiguration.DefaultChatFontShadowColorArgb, configuration.ChatFontShadowColorArgb);
        Assert.Equal(ClientFontSettingsConfiguration.NativeDefaultCornerColorArgb, configuration.DefaultCornerColorArgb);
        Assert.Equal(ClientFontSettingsConfiguration.NativeDefaultCornerOffsetXPixels, ClientFontSettingsConfiguration.DefaultCornerOffsetXPixels);
        Assert.Equal(ClientFontSettingsConfiguration.NativeDefaultCornerOffsetYPixels, ClientFontSettingsConfiguration.DefaultCornerOffsetYPixels);
    }

    [Fact]
    public void Load_ReadsNativeFontSettingValues()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(GameFontConfiguration.RelativePath, "Arial 12");
        temporaryDirectory.WriteFile(ClientFontSettingsConfiguration.RelativePath,
            """
            GUIFontShadow=1
            GUIFontShadowColor=FF102030
            ChatFont=Tahoma
            ChatFontSize=18
            ChatFontShadowColor=FF405060
            Antialias=1
            GUIFont=Verdana
            """);

        ClientFontSettingsConfiguration configuration = ClientFontSettingsConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(18, configuration.ChatFontHeightPixels);
        Assert.Equal(ClientFontSettingsConfiguration.ShadowOffsetRenderTextStyle, configuration.DefaultRenderTextStyle);
        Assert.True(configuration.AntialiasEnabled);
        Assert.True(configuration.GuiFontShadowEnabled);
        Assert.Equal("Tahoma", configuration.ChatFontFaceName);
        Assert.Equal("Verdana", configuration.GuiFontFaceName);
        Assert.Equal(0xFF405060u, configuration.ChatFontShadowColorArgb);
        Assert.Equal(0xFF102030u, configuration.DefaultCornerColorArgb);
    }

    [Fact]
    public void Load_WhenChatFontIsMissing_InheritsGuiFont()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(GameFontConfiguration.RelativePath, "Arial 12");
        temporaryDirectory.WriteFile(ClientFontSettingsConfiguration.RelativePath,
            """
            GUIFont=Verdana
            ChatFontSize=16
            """);

        ClientFontSettingsConfiguration configuration = ClientFontSettingsConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal("Verdana", configuration.GuiFontFaceName);
        Assert.Equal("Verdana", configuration.ChatFontFaceName);
    }

    [Fact]
    public void Load_WhenGuiFontIsMissing_PreservesGameFontFallback()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(GameFontConfiguration.RelativePath, "Arial 12");
        temporaryDirectory.WriteFile(ClientFontSettingsConfiguration.RelativePath,
            """
            Antialias=1
            ChatFontSize=20
            """);

        ClientFontSettingsConfiguration configuration = ClientFontSettingsConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal("Arial", configuration.GuiFontFaceName);
        Assert.Equal("Arial", configuration.ChatFontFaceName);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("+1", true)]
    [InlineData("1tail", true)]
    [InlineData("0", false)]
    [InlineData("2", false)]
    [InlineData("-1", false)]
    [InlineData("invalid", false)]
    public void Load_AntialiasUsesNativeAtoiEqualsOneSemantics(string value, bool expected)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(GameFontConfiguration.RelativePath, "Arial 12");
        temporaryDirectory.WriteFile(ClientFontSettingsConfiguration.RelativePath, $"Antialias={value}\n");

        ClientFontSettingsConfiguration configuration = ClientFontSettingsConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(expected, configuration.AntialiasEnabled);
    }

    [Fact]
    public void Load_GuiFontShadowStyleRemainsEnabledAfterLaterZeroValue()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(GameFontConfiguration.RelativePath, "Arial 12");
        temporaryDirectory.WriteFile(ClientFontSettingsConfiguration.RelativePath,
            """
            GUIFontShadow=1
            GUIFontShadow=0
            """);

        ClientFontSettingsConfiguration configuration = ClientFontSettingsConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.False(configuration.GuiFontShadowEnabled);
        Assert.Equal(ClientFontSettingsConfiguration.ShadowOffsetRenderTextStyle, configuration.DefaultRenderTextStyle);
    }

    [Theory]
    [InlineData("20", 20)]
    [InlineData("+19", 19)]
    [InlineData("-7", -7)]
    [InlineData("17pixels", 17)]
    [InlineData("0", 16)]
    [InlineData("invalid", 16)]
    public void Load_ChatFontSizeUsesNativeAtoiSemantics(string value, int expected)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(GameFontConfiguration.RelativePath, "Arial 12");
        temporaryDirectory.WriteFile(ClientFontSettingsConfiguration.RelativePath, $"ChatFontSize={value}\n");

        ClientFontSettingsConfiguration configuration = ClientFontSettingsConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(expected, configuration.ChatFontHeightPixels);
    }

    [Theory]
    [InlineData("FF112233", 0xFF112233u)]
    [InlineData("0xFF112233", 0xFF112233u)]
    [InlineData("abcdef", 0x00ABCDEFu)]
    [InlineData("12tail", 0x12u)]
    [InlineData("invalid", 0u)]
    public void Load_ShadowColorsUseNativeBaseSixteenParsing(string value, uint expected)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(GameFontConfiguration.RelativePath, "Arial 12");
        temporaryDirectory.WriteFile(ClientFontSettingsConfiguration.RelativePath,
            $"""
            GUIFontShadowColor={value}
            ChatFontShadowColor={value}
            """);

        ClientFontSettingsConfiguration configuration = ClientFontSettingsConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(expected, configuration.DefaultCornerColorArgb);
        Assert.Equal(expected, configuration.ChatFontShadowColorArgb);
    }

    [Fact]
    public void Load_RecognizesFontSettingKeysCaseInsensitively()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(GameFontConfiguration.RelativePath, "Arial 12");
        temporaryDirectory.WriteFile("INI/fOnTsEtTiNg.InI",
            """
            gUiFoNtShAdOw=1
            gUiFoNtShAdOwCoLoR=FF010203
            cHaTfOnT=Tahoma
            cHaTfOnTsIzE=21
            cHaTfOnTsHaDoWcOlOr=FF040506
            aNtIaLiAs=1
            gUiFoNt=Verdana
            """);

        ClientFontSettingsConfiguration configuration = ClientFontSettingsConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(21, configuration.ChatFontHeightPixels);
        Assert.Equal(ClientFontSettingsConfiguration.ShadowOffsetRenderTextStyle, configuration.DefaultRenderTextStyle);
        Assert.True(configuration.AntialiasEnabled);
        Assert.True(configuration.GuiFontShadowEnabled);
        Assert.Equal("Tahoma", configuration.ChatFontFaceName);
        Assert.Equal("Verdana", configuration.GuiFontFaceName);
        Assert.Equal(0xFF040506u, configuration.ChatFontShadowColorArgb);
        Assert.Equal(0xFF010203u, configuration.DefaultCornerColorArgb);
    }

    [Fact]
    public void Load_IgnoresUnknownSettingsAndComments()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(GameFontConfiguration.RelativePath, "Arial 12");
        temporaryDirectory.WriteFile(ClientFontSettingsConfiguration.RelativePath,
            """
            ; ignored
            UnknownSetting=123
            Antialias=1
            """);

        ClientFontSettingsConfiguration configuration = ClientFontSettingsConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.True(configuration.AntialiasEnabled);
        Assert.Equal("Arial", configuration.GuiFontFaceName);
        Assert.Equal("Arial", configuration.ChatFontFaceName);
    }

    [Fact]
    public void Load_RejectsFontSettingBeyondSafetyLimit()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(GameFontConfiguration.RelativePath, "Arial 12");
        temporaryDirectory.WriteFile(ClientFontSettingsConfiguration.RelativePath, new byte[(64 * 1024) + 1]);

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);

        Assert.Throws<InvalidDataException>(() => ClientFontSettingsConfiguration.Load(contentSource));
    }
}
