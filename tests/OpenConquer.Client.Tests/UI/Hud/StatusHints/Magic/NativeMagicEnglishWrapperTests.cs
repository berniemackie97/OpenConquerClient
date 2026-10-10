using System.Text;
using OpenConquer.Client.UI.Hud.StatusHints.Magic;

namespace OpenConquer.Client.Tests.UI.Hud.StatusHints.Magic;

public sealed class NativeMagicEnglishWrapperTests
{
    [Fact]
    public void Wrap_UsesSingleLineFastPathWhenWidthIsStrictlyBelowLimit()
    {
        IReadOnlyList<byte[]> lines = Wrap("abc", 19);

        Assert.Equal(["abc"], Decode(lines));
    }

    [Fact]
    public void Wrap_UsesNativeBoundaryWhenWidthEqualsLimit()
    {
        IReadOnlyList<byte[]> lines = Wrap("abc", 18);

        Assert.Equal(["abc"], Decode(lines));
    }

    [Fact]
    public void Wrap_PreservesNativeUnbrokenWordAdvanceQuirk()
    {
        IReadOnlyList<byte[]> lines = Wrap(new string('a', 61), 180);

        Assert.Equal(2, lines.Count);
        Assert.Equal(30, lines[0].Length);
        Assert.Equal(31, lines[1].Length);
    }

    [Fact]
    public void Wrap_PreservesTerminalNewlineAndFinalEmptyLine()
    {
        IReadOnlyList<byte[]> lines = Wrap("abc\n", 180);

        Assert.Equal(["abc\n", ""], Decode(lines));
    }

    [Fact]
    public void Wrap_StripsTerminalCrLfFromStoredVectorEntries()
    {
        IReadOnlyList<byte[]> lines = Wrap("abc\r\n", 180);

        Assert.Equal(["abc", ""], Decode(lines));
    }

    [Fact]
    public void Wrap_PreservesEnglishNonAsciiBytewiseClassification()
    {
        byte[] encoded = [0xD6, 0xD0, 0xD6, 0xD0];

        IReadOnlyList<byte[]> lines = NativeMagicEnglishWrapper.Wrap(encoded, 12, 6, Measure);

        Assert.Equal(encoded, lines.SelectMany(static line => line).ToArray());
    }

    [Fact]
    public void Wrap_RejectsEmptyInputAndZeroPixelLimit()
    {
        Assert.Empty(NativeMagicEnglishWrapper.Wrap(ReadOnlySpan<byte>.Empty, 180, 6, Measure));
        Assert.Empty(NativeMagicEnglishWrapper.Wrap("abc"u8, 0, 6, Measure));
    }

    [Fact]
    public void Wrap_StopsAtNativeCStringTerminator()
    {
        byte[] source = [(byte)'A', 0, (byte)'B'];

        Assert.Equal(["A"], Decode(NativeMagicEnglishWrapper.Wrap(source, 180, 6, Measure)));
    }

    private static IReadOnlyList<byte[]> Wrap(string text, int pixelLimit)
    {
        return NativeMagicEnglishWrapper.Wrap(Encoding.Latin1.GetBytes(text), pixelLimit, 6, Measure);
    }

    private static int Measure(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length * 6;
    }

    private static string[] Decode(IReadOnlyList<byte[]> lines)
    {
        return lines.Select(Encoding.Latin1.GetString).ToArray();
    }
}
