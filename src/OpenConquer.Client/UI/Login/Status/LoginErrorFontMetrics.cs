using System.Text;

namespace OpenConquer.Client.UI.Login.Status;

/// <summary>Arial14 device-text advances measured in the retail Flash7 control on Windows.</summary>
internal static class LoginErrorFontMetrics
{
    internal const int LineAdvance = 18;
    internal const int BaselineCorrection = -1; // GDI ascent13 versus the existing font renderer's baseline14.
    private static readonly byte[] s_advances =
    [
        4, 5, 5, 8, 8, 12, 9, 3, 5, 5, 5, 8, 4, 5, 4, 4,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 4, 4, 8, 8, 8, 8,
        14, 9, 9, 10, 10, 9, 9, 11, 9, 3, 6, 9, 8, 11, 9, 11,
        9, 11, 10, 9, 9, 9, 9, 13, 8, 9, 8, 4, 4, 4, 5, 8,
        5, 8, 8, 7, 8, 8, 4, 8, 8, 3, 3, 7, 3, 11, 8, 8,
        8, 8, 5, 7, 4, 8, 7, 9, 6, 7, 6, 5, 3, 5, 8, 11,
        8, 0, 3, 8, 5, 14, 8, 8, 5, 14, 9, 5, 14, 0, 8, 0,
        0, 3, 3, 5, 5, 5, 8, 14, 4, 14, 7, 5, 13, 0, 6, 9,
        4, 5, 8, 8, 8, 8, 3, 8, 5, 10, 4, 8, 8, 5, 10, 8,
        6, 8, 5, 5, 5, 8, 8, 5, 5, 5, 5, 8, 12, 12, 12, 9,
        9, 9, 9, 9, 9, 9, 14, 10, 9, 9, 9, 9, 4, 4, 4, 4,
        10, 10, 11, 11, 11, 11, 11, 8, 11, 10, 10, 10, 10, 9, 9, 9,
        8, 8, 8, 8, 8, 8, 12, 7, 8, 8, 8, 8, 4, 4, 4, 4,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 7, 8, 7,
    ];
    private static readonly int[] s_extendedScalars =
    [
        0x20AC, 0x0081, 0x201A, 0x0192, 0x201E, 0x2026, 0x2020, 0x2021,
        0x02C6, 0x2030, 0x0160, 0x2039, 0x0152, 0x008D, 0x017D, 0x008F,
        0x0090, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2013, 0x2014,
        0x02DC, 0x2122, 0x0161, 0x203A, 0x0153, 0x009D, 0x017E, 0x0178,
    ];

    internal static bool TryGetAdvance(Rune rune, out int advance)
    {
        int code = rune.Value;
        if (code is not (>= 32 and <= 127) and not (>= 160 and <= 255))
        {
            int index = Array.IndexOf(s_extendedScalars, code);
            code = index < 0 ? -1 : index + 128;
        }
        advance = code < 0 ? 0 : s_advances[code - 32];
        return code >= 0;
    }
}
