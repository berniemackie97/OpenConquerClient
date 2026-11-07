namespace OpenConquer.Networking.Game.Login;

/// <summary>
/// The values the 251 report carries (<c>CMsgAction__BuildSilentInfoReport251</c> 0x6FEC2D): +8 the silent-data
/// checksum and +16 the silent-data version. <c>CSilentInfo</c> starts with checksum 0 (0x6BB2F7) and version 1 when
/// <c>ini/SlientVersion.ini</c> cannot be opened (0x6BB3AC).
/// </summary>
internal readonly record struct GameSilentInfoReport(uint Checksum, uint Version)
{
    internal static GameSilentInfoReport WithoutFiles => new(0, 1);

    private static readonly uint[] s_table = CreateTable();

    /// <summary>
    /// <c>0x6BB1E2</c>: r = 0; for each byte r = ((r &lt;&lt; 8) | byte) ^ T[r &gt;&gt; 24], where T is the reflected
    /// CRC-32 table (polynomial 0xEDB88320) at 0x9432D8. This is not standard CRC-32.
    /// </summary>
    internal static uint ComputeChecksum(ReadOnlySpan<byte> data)
    {
        uint result = 0;
        foreach (byte value in data)
        {
            result = unchecked((result << 8) | value) ^ s_table[result >> 24];
        }

        return result;
    }

    /// <summary>
    /// <c>0x6BB3AC</c>: <c>atoi</c> over the first 128 bytes read from <c>ini/SlientVersion.ini</c>: leading white space,
    /// an optional sign, then decimal digits accumulated with 32-bit wrap-around, stopping at the first other byte.
    /// </summary>
    internal static uint ParseVersion(ReadOnlySpan<byte> text)
    {
        text = text[..Math.Min(text.Length, 128)];
        int index = 0;
        while (index < text.Length && text[index] is (byte)' ' or (>= 0x09 and <= 0x0D))
        {
            index++;
        }

        bool negative = false;
        if (index < text.Length && text[index] is (byte)'+' or (byte)'-')
        {
            negative = text[index] == '-';
            index++;
        }

        int total = 0;
        while (index < text.Length && text[index] is >= (byte)'0' and <= (byte)'9')
        {
            total = unchecked((total * 10) + (text[index] - '0'));
            index++;
        }

        return unchecked((uint)(negative ? -total : total));
    }

    private static uint[] CreateTable()
    {
        uint[] table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            uint value = index;
            for (int bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? (value >> 1) ^ 0xEDB88320 : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }
}
