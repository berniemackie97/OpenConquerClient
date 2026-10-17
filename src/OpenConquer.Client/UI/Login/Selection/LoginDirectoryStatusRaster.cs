namespace OpenConquer.Client.UI.Login.Selection;

/// <summary>Fixed 5×7 uppercase status alphabet, independent of fonts installed on the desktop host.</summary>
internal static class LoginDirectoryStatusRaster
{
    public const int GlyphWidth = 5;
    public const int GlyphHeight = 7;
    private const int CharacterStride = GlyphWidth + 1;

    public static (int Width, int Height, byte[] Pixels) Create(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length is 0 or > 48)
        {
            throw new ArgumentOutOfRangeException(nameof(text), "An operational status cannot exceed 48 characters.");
        }

        int width = checked(text.Length * CharacterStride - 1);
        byte[] pixels = new byte[checked(width * GlyphHeight * 4)];

        for (int index = 0; index < text.Length; index++)
        {
            ReadOnlySpan<byte> rows = Rows(text[index]);

            for (int y = 0; y < GlyphHeight; y++)
            {
                for (int x = 0; x < GlyphWidth; x++)
                {
                    if ((rows[y] & (1 << (GlyphWidth - 1 - x))) == 0)
                    {
                        continue;
                    }

                    int offset = checked((y * width + index * CharacterStride + x) * 4);
                    pixels[offset] = 245;
                    pixels[offset + 1] = 230;
                    pixels[offset + 2] = 194;
                    pixels[offset + 3] = 255;
                }
            }
        }

        return (width, GlyphHeight, pixels);
    }

    private static ReadOnlySpan<byte> Rows(char character) => character switch
    {
        ' ' => [0, 0, 0, 0, 0, 0, 0],
        'A' => [14, 17, 17, 31, 17, 17, 17],
        'B' => [30, 17, 17, 30, 17, 17, 30],
        'C' => [14, 17, 16, 16, 16, 17, 14],
        'D' => [30, 17, 17, 17, 17, 17, 30],
        'E' => [31, 16, 16, 30, 16, 16, 31],
        'F' => [31, 16, 16, 30, 16, 16, 16],
        'G' => [14, 17, 16, 23, 17, 17, 14],
        'H' => [17, 17, 17, 31, 17, 17, 17],
        'I' => [31, 4, 4, 4, 4, 4, 31],
        'J' => [7, 2, 2, 2, 18, 18, 12],
        'K' => [17, 18, 20, 24, 20, 18, 17],
        'L' => [16, 16, 16, 16, 16, 16, 31],
        'M' => [17, 27, 21, 21, 17, 17, 17],
        'N' => [17, 25, 21, 19, 17, 17, 17],
        'O' => [14, 17, 17, 17, 17, 17, 14],
        'P' => [30, 17, 17, 30, 16, 16, 16],
        'Q' => [14, 17, 17, 17, 21, 18, 13],
        'R' => [30, 17, 17, 30, 20, 18, 17],
        'S' => [15, 16, 16, 14, 1, 1, 30],
        'T' => [31, 4, 4, 4, 4, 4, 4],
        'U' => [17, 17, 17, 17, 17, 17, 14],
        'V' => [17, 17, 17, 17, 17, 10, 4],
        'W' => [17, 17, 17, 21, 21, 21, 10],
        'X' => [17, 17, 10, 4, 10, 17, 17],
        'Y' => [17, 17, 10, 4, 4, 4, 4],
        'Z' => [31, 1, 2, 4, 8, 16, 31],
        _ => throw new ArgumentException("Unsupported status character.", nameof(character)),
    };
}
