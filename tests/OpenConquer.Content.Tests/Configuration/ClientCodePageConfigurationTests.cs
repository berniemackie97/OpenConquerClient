using OpenConquer.Content.Configuration;
using OpenConquer.Content.Tests.Wdf;

namespace OpenConquer.Content.Tests.Configuration;

public sealed class ClientCodePageConfigurationTests
{
    [Fact]
    public void Load_WhenConfigurationIsMissing_PreservesNativeUnsetAndUsesDeterministicDefault()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        ClientCodePageConfiguration configuration = ClientCodePageConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(ClientCodePageConfiguration.NativeUnsetCodePage, configuration.ConfiguredCodePage);
        Assert.Equal(ClientCodePageConfiguration.DefaultEffectiveCodePage, configuration.EffectiveCodePage);
    }

    [Theory]
    [InlineData("0", 0, 936)]
    [InlineData("936", 936, 936)]
    [InlineData("950", 950, 950)]
    [InlineData("1252", 1252, 1252)]
    [InlineData("-1", -1, -1)]
    public void Load_PreservesConfiguredCodePageAndResolvesEffectiveValue(string contents, int expectedConfiguredCodePage, int expectedEffectiveCodePage)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(ClientCodePageConfiguration.RelativePath, contents);

        ClientCodePageConfiguration configuration = ClientCodePageConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(expectedConfiguredCodePage, configuration.ConfiguredCodePage);
        Assert.Equal(expectedEffectiveCodePage, configuration.EffectiveCodePage);
    }

    [Theory]
    [InlineData("  936", 936)]
    [InlineData("\t+950 trailing", 950)]
    [InlineData("-1xyz", -1)]
    [InlineData("1252 // comment", 1252)]
    [InlineData("936\0ignored", 936)]
    public void Load_UsesNativeStyleIntegerPrefixParsing(string contents, int expectedCodePage)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(ClientCodePageConfiguration.RelativePath, contents);

        ClientCodePageConfiguration configuration = ClientCodePageConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(expectedCodePage, configuration.ConfiguredCodePage);
        Assert.Equal(expectedCodePage, configuration.EffectiveCodePage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid")]
    [InlineData("+")]
    [InlineData("-")]
    public void Load_InvalidFirstLinePreservesNativeUnsetAndUsesDeterministicDefault(string contents)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(ClientCodePageConfiguration.RelativePath, contents);

        ClientCodePageConfiguration configuration = ClientCodePageConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(ClientCodePageConfiguration.NativeUnsetCodePage, configuration.ConfiguredCodePage);
        Assert.Equal(ClientCodePageConfiguration.DefaultEffectiveCodePage, configuration.EffectiveCodePage);
    }

    [Fact]
    public void Load_ReadsOnlyFirstLineAndAcceptsCrLf()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(ClientCodePageConfiguration.RelativePath, "950\r\n936\r\n");

        ClientCodePageConfiguration configuration = ClientCodePageConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(950, configuration.ConfiguredCodePage);
        Assert.Equal(950, configuration.EffectiveCodePage);
    }

    [Fact]
    public void Load_ResolvesConfigurationPathCaseInsensitively()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("INI/cOdEpAgE.InI", "950");

        ClientCodePageConfiguration configuration = ClientCodePageConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(950, configuration.ConfiguredCodePage);
        Assert.Equal(950, configuration.EffectiveCodePage);
    }

    [Fact]
    public void Load_DoesNotFallBackToPackagedContent()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/package.ini", "ini.wdf\n");
        temporaryDirectory.WriteFile("ini.wdf", WdfTestArchiveBuilder.CreateSingleEntry(ClientCodePageConfiguration.RelativePath, "950\n"u8));

        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(temporaryDirectory.RootPath);

        using (contentSource.OpenRequiredRead(ClientCodePageConfiguration.RelativePath, ContentLookupMode.PackageOnly))
        {
        }

        ClientCodePageConfiguration configuration = ClientCodePageConfiguration.Load(contentSource);

        Assert.Equal(ClientCodePageConfiguration.NativeUnsetCodePage, configuration.ConfiguredCodePage);
        Assert.Equal(ClientCodePageConfiguration.DefaultEffectiveCodePage, configuration.EffectiveCodePage);
    }

    [Fact]
    public void Load_AcceptsFirstLineAtSafetyLimit()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        byte[] contents = new byte[256];
        contents.AsSpan().Fill((byte)' ');
        "936"u8.CopyTo(contents);

        temporaryDirectory.WriteFile(ClientCodePageConfiguration.RelativePath, contents);

        ClientCodePageConfiguration configuration = ClientCodePageConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(936, configuration.ConfiguredCodePage);
        Assert.Equal(936, configuration.EffectiveCodePage);
    }

    [Fact]
    public void Load_RejectsFirstLineBeyondSafetyLimit()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        byte[] contents = new byte[257];
        contents.AsSpan().Fill((byte)' ');

        temporaryDirectory.WriteFile(ClientCodePageConfiguration.RelativePath, contents);

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);

        Assert.Throws<InvalidDataException>(() => ClientCodePageConfiguration.Load(contentSource));
    }

    [Theory]
    [InlineData("2147483647", int.MaxValue)]
    [InlineData("-2147483648", int.MinValue)]
    public void ParseConfiguredCodePage_AcceptsInt32Boundaries(string contents, int expected)
    {
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(contents);

        int actual = ClientCodePageConfiguration.ParseConfiguredCodePage(bytes);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("2147483648")]
    [InlineData("-2147483649")]
    public void ParseConfiguredCodePage_RejectsInt32Overflow(string contents)
    {
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(contents);

        Assert.Throws<InvalidDataException>(() => ClientCodePageConfiguration.ParseConfiguredCodePage(bytes));
    }
}
