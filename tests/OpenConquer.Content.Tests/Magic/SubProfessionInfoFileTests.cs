using System.Text;
using OpenConquer.Content.Magic;

namespace OpenConquer.Content.Tests.Magic;

public sealed class SubProfessionInfoFileTests
{
    [Fact]
    public void Parse_ResolvesBaseClassTitleInsteadOfPhaseTitle()
    {
        SubProfessionInfoFile file = Parse(
            "//Performer\n" +
            "[6]\n" +
            "title=Performer\n" +
            "[61]\n" +
            "title=P1 Performer\n");

        Assert.True(file.TryGetTitle(6, out ReadOnlyMemory<byte> title));
        Assert.Equal("Performer", Encoding.Latin1.GetString(title.Span));

        Assert.True(file.TryGetTitle(61, out ReadOnlyMemory<byte> phaseTitle));
        Assert.Equal("P1 Performer", Encoding.Latin1.GetString(phaseTitle.Span));
    }

    [Fact]
    public void Parse_UsesNativeCaseInsensitiveSectionAndTitleKey()
    {
        SubProfessionInfoFile file = Parse("[6]\nTiTlE=Old\n[6]\ntitle=Performer\n");

        Assert.True(file.TryGetTitle(6, out ReadOnlyMemory<byte> title));
        Assert.Equal("Performer", Encoding.Latin1.GetString(title.Span));
    }

    [Fact]
    public void Parse_PreservesNonAsciiBytesAndTranslatesLiteralEscapes()
    {
        SubProfessionInfoFile file = Parse("[6]\ntitle=\u00D6\u00D0\\nPerformer\\tClass\n");

        Assert.True(file.TryGetTitle(6, out ReadOnlyMemory<byte> title));
        Assert.Equal(
            new byte[] { 0xD6, 0xD0, (byte)'\n', (byte)'P', (byte)'e', (byte)'r', (byte)'f',
                (byte)'o', (byte)'r', (byte)'m', (byte)'e', (byte)'r', (byte)'\t',
                (byte)'C', (byte)'l', (byte)'a', (byte)'s', (byte)'s' },
            title.ToArray());
    }

    [Fact]
    public void Parse_MissingOrEmptyTitleDoesNotInventAClassName()
    {
        SubProfessionInfoFile file = Parse("[6]\nintro=Performer class\n[7]\ntitle=\n");

        Assert.False(file.TryGetTitle(0, out _));
        Assert.False(file.TryGetTitle(6, out _));
        Assert.False(file.TryGetTitle(7, out _));
        Assert.False(file.TryGetTitle(999, out _));
    }

    [Fact]
    public void Load_RequiresLooseContent()
    {
        using TemporaryContentDirectory directory = new();
        directory.WriteFile("INI/SubProfessionInfo.INI", "[6]\ntitle=Performer\n");

        SubProfessionInfoFile file = SubProfessionInfoFile.Load(new ClientContentRoot(directory.RootPath));

        Assert.True(file.TryGetTitle(6, out ReadOnlyMemory<byte> title));
        Assert.Equal("Performer", Encoding.Latin1.GetString(title.Span));
    }

    [Fact]
    public void Parse_RejectsOversizedContent()
    {
        Assert.Throws<InvalidDataException>(() => SubProfessionInfoFile.Parse(new byte[1024 * 1024 + 1]));
    }

    private static SubProfessionInfoFile Parse(string encoded)
    {
        return SubProfessionInfoFile.Parse(Encoding.Latin1.GetBytes(encoded));
    }
}
