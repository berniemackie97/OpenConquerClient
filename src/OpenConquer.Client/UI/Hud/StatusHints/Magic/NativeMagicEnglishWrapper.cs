namespace OpenConquer.Client.UI.Hud.StatusHints.Magic;

internal delegate int NativeMagicTextWidth(ReadOnlySpan<byte> encodedText);

internal static class NativeMagicEnglishWrapper
{
    private const int MaximumNativeBoundaries = 513;

    public static IReadOnlyList<byte[]> Wrap(ReadOnlySpan<byte> encodedText, int pixelLimit, int globalFontWidth, NativeMagicTextWidth measure)
    {
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentOutOfRangeException.ThrowIfNegative(globalFontWidth);

        int nullIndex = encodedText.IndexOf((byte)0);

        if (nullIndex >= 0)
        {
            encodedText = encodedText[..nullIndex];
        }

        if (encodedText.IsEmpty || pixelLimit == 0)
        {
            return [];
        }

        if (!encodedText.Contains((byte)'\n') && measure(encodedText) < pixelLimit)
        {
            return [encodedText.ToArray()];
        }

        int effectiveLimit = Math.Max(pixelLimit, checked(2 * globalFontWidth + 1));

        Span<int> boundaries = stackalloc int[MaximumNativeBoundaries];
        boundaries.Clear();

        int boundaryIndex = 0;
        int position = 0;
        int count = 0;
        int lastWidth = 0;
        bool afterNewline = false;

        while (position < encodedText.Length)
        {
            count++;

            if (encodedText[position] == (byte)'\n')
            {
                count = 0;
                boundaryIndex++;
                ValidateBoundary(boundaryIndex);
                boundaries[boundaryIndex] = position + 1;

                if (!afterNewline)
                {
                    boundaryIndex++;
                    ValidateBoundary(boundaryIndex);
                }
                else
                {
                    boundaries[boundaryIndex - 1] = position + 1;
                }

                boundaries[boundaryIndex] = position + 1;
                afterNewline = true;
            }
            else
            {
                int start = boundaries[boundaryIndex];
                lastWidth = measure(encodedText.Slice(start, count));

                if (lastWidth > effectiveLimit)
                {
                    int scan = position;

                    while (scan > start)
                    {
                        if (encodedText[scan] == (byte)' ')
                        {
                            lastWidth = measure(encodedText.Slice(start, scan - start));
                            position = scan;
                            break;
                        }

                        scan--;
                    }

                    count = 0;

                    if (!afterNewline)
                    {
                        boundaryIndex++;
                        ValidateBoundary(boundaryIndex);
                    }
                    else
                    {
                        afterNewline = false;
                    }

                    boundaries[boundaryIndex] = position;
                }
            }

            position++;
        }

        if ((lastWidth < effectiveLimit && count != 0) || position == encodedText.Length)
        {
            if (!afterNewline)
            {
                boundaryIndex++;
                ValidateBoundary(boundaryIndex);
            }

            boundaries[boundaryIndex] = position;
        }

        List<byte[]> lines = new(boundaryIndex);

        for (int index = 0; index < boundaryIndex; index++)
        {
            ReadOnlySpan<byte> line = encodedText.Slice(boundaries[index], boundaries[index + 1] - boundaries[index]);

            if (line.EndsWith("\r\n"u8))
            {
                line = line[..^2];
            }

            lines.Add(line.ToArray());
        }

        return lines;
    }

    private static void ValidateBoundary(int index)
    {
        if ((uint)index >= MaximumNativeBoundaries)
        {
            throw new InvalidDataException("Native magic hint wrapping exceeded the supported line-vector boundary.");
        }
    }
}
