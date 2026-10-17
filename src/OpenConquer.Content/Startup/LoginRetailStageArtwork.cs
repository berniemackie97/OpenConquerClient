using System.IO.Compression;
using System.Security.Cryptography;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Startup;

/// <summary>
/// Hash-verified 5517 <c>start.dat</c> stage layers and <c>defaultR.swf</c>/<c>unopenedR.swf</c> region art,
/// baked offline through their authored matrices onto the 1024x768 stage grid. No runtime SWF decoding.
/// </summary>
public static class LoginRetailStageArtwork
{
    public const int RegionHoverFrames = 10;
    public const int RegionFlameFrames = 25;
    public const int ButtonStates = 3;

    public enum Layer
    {
        /// <summary>Root depth 216, shape 400 at identity.</summary>
        StageBorder,
        /// <summary>Sprite 259 depth 1, sprite 183 with its 195/256 alpha transform, stage-aligned.</summary>
        RegionPanel,
        /// <summary>defaultR shape 3 bitmap 2 (its baked caption omitted), holder-relative at (0, -1).</summary>
        RegionTile,
        /// <summary>unopenedR sprite 3, holder-relative at (0, 0).</summary>
        RegionUnopened,
        /// <summary>defaultR depth 3 sprite 9 for root frames 2-8 and 10-12, holder-relative at (-10, -6).</summary>
        RegionHover,
        /// <summary>defaultR sprite 61 nesting sprite 60's 25 frames, holder-relative at (2, 0).</summary>
        RegionFlame,
        /// <summary>Sprite 259 leftBtn, DefineButton2 199 Up/Over/Down, stage-aligned.</summary>
        RegionPagePrevious,
        /// <summary>Sprite 259 rightBtn, DefineButton2 192 Up/Over/Down, stage-aligned.</summary>
        RegionPageNext,
    }

    /// <summary>One image or a left-to-right, top-to-bottom frame atlas.</summary>
    public readonly record struct Sheet(int Width, int Height, int FrameWidth, int FrameHeight, int Columns, int Count, string Sha256);

    private const string Prefix = "OpenConquer.Content.Assets.Login.RetailStage.";

    private static readonly Sheet[] s_sheets =
    [
        new(1024, 768, 1024, 768, 1, 1, "76A47E69EF101456A8925BB84A137B3D738C8BF6BCF7AD2F0DED9A0FD5CC5AA8"),
        new(340, 434, 340, 434, 1, 1, "DF314FC0B3E49BA4255E05750F72BF0F765C79DD0B71883E149AD1343342986B"),
        new(151, 53, 151, 53, 1, 1, "466A1A4A2219102246874C9407FBC8790A4F7828C465692B8321091D241CBF38"),
        new(151, 53, 151, 53, 1, 1, "BBCC706CC21EFD39DAF06B306C5A8D5A185037288F8787378FD7BD86DC81424F"),
        new(850, 130, 170, 65, 5, RegionHoverFrames, "B3558A7B60937C264B15D6D23EBA65E924F2DACE7374ACD7F4A0C4EE2DC233E2"),
        new(740, 250, 148, 50, 5, RegionFlameFrames, "E8480C78CB0E8E51BA83B340034B7E84CE333A009092678C1111DDC9146EE5C2"),
        new(114, 34, 38, 34, 3, ButtonStates, "EAAF849A2835277D469D498F46642E1B3D4D176AB45D737D45DE9BB6994A9B69"),
        new(120, 34, 40, 34, 3, ButtonStates, "364E2013C86146F75C013C1828E891B348D984DFDBB1580B9D93D18541354D41"),
    ];

    public static Sheet Describe(Layer layer) => s_sheets[Validate(layer)];

    public static RgbaImage Load(Layer layer)
    {
        Sheet sheet = s_sheets[Validate(layer)];
        using Stream source = typeof(LoginRetailStageArtwork).Assembly.GetManifestResourceStream(Prefix + layer + ".rgba.br")
            ?? throw new InvalidDataException($"Missing verified 5517 stage artwork: {layer}.");
        using BrotliStream decoder = new(source, CompressionMode.Decompress);
        byte[] pixels = GC.AllocateUninitializedArray<byte>(checked(sheet.Width * sheet.Height * 4));

        try
        {
            decoder.ReadExactly(pixels);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException($"Truncated 5517 stage artwork: {layer}.", exception);
        }

        if (decoder.ReadByte() != -1 || !SHA256.HashData(pixels).AsSpan().SequenceEqual(Convert.FromHexString(sheet.Sha256)))
        {
            throw new InvalidDataException($"5517 stage artwork failed integrity verification: {layer}.");
        }

        return new RgbaImage(sheet.Width, sheet.Height, pixels);
    }

    private static int Validate(Layer layer) => Enum.IsDefined(layer) ? (int)layer : throw new ArgumentOutOfRangeException(nameof(layer));
}
