using System.Text;

namespace OpenConquer.Client.UI.Login.Status;

/// <summary>Plain, clipped multiline text for the read-only Flash error field.</summary>
internal static class LoginErrorTextLayout
{
    internal static IReadOnlyList<string> Wrap(string text, int width, Func<string, int> measure)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        List<string> lines = [];
        foreach (string paragraph in text.Replace('\r', '\n').Split('\n'))
        {
            string remaining = paragraph;
            while (remaining.Length > 0 && measure(remaining) > width)
            {
                int fits = 0;
                int wordBreak = 0;
                foreach (Rune rune in remaining.EnumerateRunes())
                {
                    int next = fits + rune.Utf16SequenceLength;
                    if (fits > 0 && measure(remaining[..next]) > width)
                    {
                        break;
                    }

                    fits = next;
                    if (rune.Value is ' ' or '\t')
                    {
                        wordBreak = fits;
                    }
                }

                int split = wordBreak > 0 ? wordBreak : fits;
                lines.Add(remaining[..split]);
                remaining = remaining[split..];
            }

            lines.Add(remaining);
        }

        return lines;
    }
}
