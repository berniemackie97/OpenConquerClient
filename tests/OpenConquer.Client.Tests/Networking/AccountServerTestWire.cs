namespace OpenConquer.Client.Tests.Networking;

/// <summary>
/// Independent AccountServer-side wire fixtures. The vectors were computed outside the production cipher: the
/// server sends the inverse of the client's forward stream transform, positioned after the 8-byte 1059 frame.
/// </summary>
internal static class AccountServerTestWire
{
    /// <summary>1059 seed frame for seed 0x12345678 at stream position 0.</summary>
    internal const string SeedWire = "C54869128E317C0F";

    /// <summary>1060/0xF001 credential field for "testacc" / "password1" under <see cref="SeedWire"/>.</summary>
    internal const string TestAccountCredential = "B059847E5023FEDCD3BCE92F93A9A71008C9ED3654CD597508C9ED3654CD5975";

    /// <summary>0xF002 frames for result codes 0-5 at stream position 8; code 5 is undefined.</summary>
    internal static byte[] RegistrationResultWire(uint code) => Convert.FromHexString(code switch
    {
        0 => "7DF0135541BC57F6",
        1 => "7DF0135551BC57F6",
        2 => "7DF0135561BC57F6",
        3 => "7DF0135571BC57F6",
        4 => "7DF0135501BC57F6",
        5 => "7DF0135511BC57F6",
        _ => throw new ArgumentOutOfRangeException(nameof(code)),
    });

    /// <summary>A 1055 frame header (length 36, type 1055) at stream position 8.</summary>
    internal const string AuthenticationHeaderWire = "BFF0C21A";

    /// <summary>Server-side inverse of the client stream transform for one client-to-server stream from position 0.</summary>
    internal static void DecodeClientRequest(Span<byte> frame)
    {
        Span<byte> streamA = stackalloc byte[256];
        Span<byte> streamB = stackalloc byte[256];
        byte key = 0x9D;
        for (int i = 0; i < 256; i++)
        {
            streamA[i] = key;
            key = unchecked((byte)((((byte)(key * 0xFA) + 0x0F) * key) + 0x13));
        }

        key = 0x62;
        for (int i = 0; i < 256; i++)
        {
            streamB[i] = key;
            key = unchecked((byte)((((byte)(0x79 - (byte)(key * 0x5C))) * key) + 0x6D));
        }

        for (int i = 0; i < frame.Length; i++)
        {
            byte value = (byte)(frame[i] ^ 0xAB);
            value = (byte)((value << 4) | (value >> 4));
            frame[i] = (byte)(value ^ streamA[i & 255] ^ streamB[(i >> 8) & 255]);
        }
    }
}
