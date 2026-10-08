using System.Text;
using OpenConquer.Content.Text;

namespace OpenConquer.Content.Tests.Text;

public sealed class ClientStringResourcesTests
{
    [Fact]
    public void Parse_PreservesEncodedBytesAndValuesAfterFirstEquals()
    {
        byte[] encoded = [(byte)'1', (byte)'=', 0xD6, 0xD0, (byte)'=', (byte)'2', (byte)'\r', (byte)'\n'];
        ClientStringResources resources = ClientStringResources.Parse(encoded);

        Assert.Equal(new byte[] { 0xD6, 0xD0, (byte)'=', (byte)'2' }, resources.GetEncoded(1).ToArray());
        Assert.Equal(1, resources.Count);
    }

    [Fact]
    public void Parse_UsesNativeFirstByteCommentsAtoiAndDuplicateReplacement()
    {
        byte[] encoded = Encoding.ASCII.GetBytes(";ignored=3\n  ;not-a-comment=first\n+17tail=old\n17=new\nmissing=zero\n17=\n");
        ClientStringResources resources = ClientStringResources.Parse(encoded);

        Assert.Equal("", Encoding.ASCII.GetString(resources.GetEncoded(17).Span));
        Assert.Equal("zero", Encoding.ASCII.GetString(resources.GetEncoded(0).Span));
        Assert.Equal(2, resources.Count);
    }

    [Fact]
    public void Parse_StopsCStringValuesAtNul()
    {
        byte[] encoded = [(byte)'1', (byte)'=', (byte)'A', 0, (byte)'B', (byte)'=', (byte)'C', (byte)'\n'];
        ClientStringResources resources = ClientStringResources.Parse(encoded);

        Assert.Equal("A", Encoding.ASCII.GetString(resources.GetEncoded(1).Span));
    }

    [Fact]
    public void Parse_SplitsLongLinesAtNativeFgetsBoundary()
    {
        byte[] encoded = Encoding.ASCII.GetBytes("9=" + new string('X', 1021) + "\n1=ok\n");
        ClientStringResources resources = ClientStringResources.Parse(encoded);

        Assert.Equal(1021, resources.GetEncoded(9).Length);
        Assert.Equal("ok", Encoding.ASCII.GetString(resources.GetEncoded(1).Span));
    }

    [Theory]
    [InlineData("10426=Arabic\n", true)]
    [InlineData("10426=aRaBiC\n", true)]
    [InlineData("10426=English\n", false)]
    [InlineData("10426=Arabic \n", false)]
    public void UsesArabicLayout_MatchesNativeLanguageSelection(string encoded, bool expected)
    {
        ClientStringResources resources = ClientStringResources.Parse(Encoding.ASCII.GetBytes(encoded));

        Assert.Equal(expected, resources.UsesArabicLayout);
    }

    [Fact]
    public void GetEncoded_UsesUnsignedNativeMissingStringFallback()
    {
        ClientStringResources resources = ClientStringResources.Parse([]);

        Assert.Equal("SE:4294967295", Encoding.ASCII.GetString(resources.GetEncoded(-1).Span));
    }

    [Fact]
    public void Parse_RejectsUnsupportedIdentifierOverflow()
    {
        Assert.Throws<InvalidDataException>(() => ClientStringResources.Parse("2147483648=value\n"u8));
        Assert.Throws<InvalidDataException>(() => ClientStringResources.Parse("-2147483649=value\n"u8));
    }

    [Fact]
    public void Load_UsesLooseOnlyContent()
    {
        using TemporaryContentDirectory directory = new();
        directory.WriteFile(ClientStringResources.RelativePath, "10070=Walk/Run\n");

        ClientStringResources resources = ClientStringResources.Load(new ClientContentRoot(directory.RootPath));

        Assert.Equal("Walk/Run", Encoding.ASCII.GetString(resources.GetEncoded(10070).Span));
    }
}
