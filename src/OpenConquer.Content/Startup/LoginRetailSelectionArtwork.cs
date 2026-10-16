using System.IO.Compression;
using System.Security.Cryptography;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Startup;

/// <summary>Bounded, hash-verified 5517 region and server timeline artwork; no runtime SWF decoding.</summary>
public static class LoginRetailSelectionArtwork
{
    public const int FramesPerSecond = 30;
    public const int RegionFlameFrames = 25;
    public const int RegionHoverFrames = 10;
    public const int ServerHoverFrames = 34;
    public const int ServerSelectedFrames = 47;
    public const int ServerSelectedLoopStart = 22; // Zero-based AVM1 label "aa" at source frame 23.

    public enum Sequence
    {
        RegionFlame, RegionHoverBorder, ServerHover, ServerSelected
    }

    public readonly record struct Sheet(int Width, int Height, int FrameWidth, int FrameHeight, int Columns, int Count, string Sha256);

    private const string Prefix = "OpenConquer.Content.Assets.Login.RetailSelection.";
    private static readonly Sheet[] s_sheets =
    [
        new(750, 250, 150, 50, 5, 25, "5E4D2A45870C80DAA195E2F9345E6FFACC69C2B327C2E5043784FC9C05F5B3B3"),
        new(790, 116, 158, 58, 5, 10, "9287382B9F167D36F0C6D02D2FFF1E4AAEC021DB73AF97809C5952F8715E5328"),
        new(1600, 600, 200, 120, 8, 34, "C8695D2B639DA9B12B49EC0B1786AF58E62FD392C2B9FEA9489E9605519EE4A3"),
        new(1760, 720, 220, 120, 8, 47, "C6DDCC3CE1ED6A121492763E369E534EBCBA5BD1A40EF902F5C2228A0C42938C"),
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
