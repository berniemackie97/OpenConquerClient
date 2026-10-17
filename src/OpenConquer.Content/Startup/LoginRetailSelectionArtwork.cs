using System.IO.Compression;
using System.Security.Cryptography;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Startup;

/// <summary>Bounded, hash-verified 5517 server-tile timeline artwork (start.dat sprites 83, 178 and 34); no runtime SWF decoding.</summary>
public static class LoginRetailSelectionArtwork
{
    public const int FramesPerSecond = 30;
    public const int ServerHoverFrames = 34;
    public const int ServerSelectedFrames = 47;
    public const int ServerSelectedLoopStart = 22; // Zero-based AVM1 label "aa" at source frame 23.

    public enum Sequence
    {
        ServerHover,
        ServerSelected,
        RealmStatus
    }

    public readonly record struct Sheet(int Width, int Height, int FrameWidth, int FrameHeight, int Columns, int Count, string Sha256);

    private const string Prefix = "OpenConquer.Content.Assets.Login.RetailSelection.";
    private static readonly Sheet[] s_sheets =
    [
        new(1600, 600, 200, 120, 8, 34, "C8695D2B639DA9B12B49EC0B1786AF58E62FD392C2B9FEA9489E9605519EE4A3"),
        new(1760, 720, 220, 120, 8, 47, "C6DDCC3CE1ED6A121492763E369E534EBCBA5BD1A40EF902F5C2228A0C42938C"),
        new(72, 18, 18, 18, 4, 4, "5FA3C29BE37231F2CB83C54440C737C45BEFFB10694687D40680224BE3A5C7E2"),
    ];

    public static Sheet Describe(Sequence sequence) => s_sheets[Validate(sequence)];

    public static RgbaImage Load(Sequence sequence)
    {
        int index = Validate(sequence);
        Sheet sheet = s_sheets[index];
        string resource = Prefix + sequence + ".rgba.br";
        using Stream source = typeof(LoginRetailSelectionArtwork).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidDataException($"Missing trusted 5517 selection resource: {sequence}.");
        using BrotliStream decoder = new(source, CompressionMode.Decompress);
        byte[] pixels = GC.AllocateUninitializedArray<byte>(checked(sheet.Width * sheet.Height * 4));
        try
        {
            decoder.ReadExactly(pixels);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException($"Truncated 5517 selection artwork: {sequence}.", exception);
        }

        if (decoder.ReadByte() != -1 ||
            !SHA256.HashData(pixels).AsSpan().SequenceEqual(Convert.FromHexString(sheet.Sha256)))
        {
            throw new InvalidDataException($"5517 selection artwork integrity failure: {sequence}.");
        }

        return new RgbaImage(sheet.Width, sheet.Height, pixels);
    }

    private static int Validate(Sequence sequence)
    {
        if (!Enum.IsDefined(sequence))
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }
        return (int)sequence;
    }
}
