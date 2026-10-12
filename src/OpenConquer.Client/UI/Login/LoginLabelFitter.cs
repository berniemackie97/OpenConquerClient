using System.Buffers;
using System.Globalization;
using System.Text;

namespace OpenConquer.Client.UI.Login;

/// <summary>Provides grapheme-safe truncation and pixel-exact placement inside verified login cells.</summary>
internal static class LoginLabelFitter
{
    public static IEnumerable<string> Candidates(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        yield return name;

        int[] starts = StringInfo.ParseCombiningCharacters(name);

        for (int index = starts.Length - 1; index >= 0; index--)
        {
            yield return name[..starts[index]] + "...";
        }
    }

    public static bool HasMultiScalarTextElements(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        int[] starts = StringInfo.ParseCombiningCharacters(name);
        ReadOnlySpan<char> text = name.AsSpan();

        for (int index = 0; index < starts.Length; index++)
        {
            int next = index + 1 < starts.Length ? starts[index + 1] : text.Length;
            ReadOnlySpan<char> element = text[starts[index]..next];

            if (Rune.DecodeFromUtf16(element, out _, out int consumed) != OperationStatus.Done || consumed != element.Length)
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryCenter(LoginSpritePlacement cell, int paddingPixels, int inkLeft, int inkTop, int inkRight, int inkBottom, out int x, out int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(paddingPixels);

        long inkWidth = (long)inkRight - inkLeft;
        long inkHeight = (long)inkBottom - inkTop;
        long availableWidth = (long)cell.Width - 2L * paddingPixels;

        if (inkWidth <= 0 || inkHeight <= 0 || availableWidth <= 0 || inkWidth > availableWidth || inkHeight > cell.Height)
        {
            x = y = 0;
            return false;
        }

        long originX = (long)cell.X + paddingPixels + (availableWidth - inkWidth) / 2 - inkLeft;
        long originY = (long)cell.Y + (cell.Height - inkHeight) / 2 - inkTop;

        x = checked((int)originX);
        y = checked((int)originY);
        return true;
    }
}
