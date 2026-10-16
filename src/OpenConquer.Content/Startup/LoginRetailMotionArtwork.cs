using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Startup;

/// <summary>Offline-verified 5517 opening and bird media. No runtime SWF/video parsing.</summary>
public static class LoginRetailMotionArtwork
{
    public const int FramesPerSecond = 30;
    public const int OpeningFrameCount = 27;
    public const int BirdFrameCount = 602;
    public const int BirdAtlasWidth = 1024;
    public const int BirdAtlasHeight = 1024;
    private const string Prefix = "OpenConquer.Content.Assets.Login.RetailMotion.";
    private const int BirdRecordBytes = 24;
    private const string BirdTableHash = "DDE10F86E3C27EEBEF114E31387DF835B789438B4A730A9BD72C176F61AF596F";

    private readonly record struct Asset(string Id, int Width, int Height, string Sha256);
    private static readonly Asset[] s_assets =
    [
        new("Intro01", 1024, 768, "873E24093D798886B99372970007A07347AFDD38E4EA7FE7240DF172C68B7DF5"),
        new("Intro02", 1024, 768, "9C2D79AE3B116159464A88C464227750876D534B8744A2472C0255757EFA5D53"),
        new("Intro03", 1024, 768, "FFFD0FAD4F283826D8DAA6ED27F777964B146DAD0F6E3CE27C47CF96BA7F6AFC"),
        new("Intro04", 1024, 768, "2FBF7D1787B2637769B7521B49102A720EA5F37E67F1DE8DB49B932F4EE0A908"),
        new("Intro05", 1024, 768, "4B218D4603EF8A2A44F8FB8335637F2658764BC8140D2BFCBEAB4BAC242E358D"),
        new("Intro06", 1024, 768, "D78B72A81E6651D6DF3D3AF961125899C1903036C19E15E3397655DF89311F6B"),
        new("Intro07", 1024, 768, "9602B88E6557E87FF093E9D062C7350B62489AE719339498DDF38E41BAAF863E"),
        new("Intro08", 1024, 768, "9A400E8EEBB44ADDBA0061AD7B081B213811D8C66B8DE0817DD23F229FC722CC"),
        new("Intro09", 1024, 768, "3CC231192582CAD161A6C28540DFFD9A42CC00D75CDB33F9B74FFA61FF847A3F"),
        new("Intro10", 1024, 768, "8CBA7F71D38E95DE8238C5C913FD2D12EA5FA4E8444BA7487DD24DC5CE948393"),
        new("Intro11", 1024, 768, "B3821A92C6472E76C2FD33C9E01FC42683BC31DE1A6CC5FFE25C540452D5528D"),
        new("Intro12", 1024, 768, "CADD52E2C5A214EEFAEB7EE68A37AB4598B4666D062ACB3CC5D04EB9AD2E5C2A"),
        new("Intro13", 1024, 768, "0C244BD53C7C4D9984103551A750387ABB6C20462B3A2D67E77B9D7AE167E44F"),
        new("Intro14", 1024, 768, "473E43616A4FCE810391CFAD23BBDCC37C3B53831FB17876409A2C704226D99A"),
        new("Intro15", 1024, 768, "989537EAEEB1177EA101F0EF39BBA617DB137C1345395F36E83333AB44CE6A35"),
        new("Intro16", 1024, 768, "B4BF0ACFFCA2CAFA1167471363309DCE283D47AEDE37956C45DFA5800C656AE4"),
        new("Intro17", 1024, 768, "4D14F9CF99926CDF225ACB2751F52A85FB66C7B7361EB7EC195FE1ADB3A38E2C"),
        new("Intro18", 1024, 768, "40D06D827A2EF240AB790EE1BA3B89512C1F106A78B23B3003A989F3D7E8962E"),
        new("Intro19", 1024, 768, "6C11DE07DBDF451964921DEAB53F7561F37AC49DB38A289703E07089FEF78DA4"),
        new("Intro20", 1024, 768, "019B29C8496837F875F297195D6DF29406EEBD34F38394F48042EC72BA71CF89"),
        new("Intro21", 1024, 768, "5E299E34AA5CFBB4B0DC0F2E30DB71A64D91E1E6657B70E83B2E1D20AB59AAC7"),
        new("Intro22", 1024, 768, "2B85782E0D8BE1DB15772D1EF2E786E019AE605E93DDF5C784D1500F7E1627EA"),
        new("Intro23", 1024, 768, "7D3CB21A03ED1C61659C736136CE74DFE44E514412E85DB6079C69D984F390E0"),
        new("Intro24", 1024, 768, "AF219FB4DD46208DF59882D38968C10DFB621F60AA27C6A79FAE548F0F6E932E"),
        new("Intro25", 1024, 768, "FE6DDFF4B1986803578379334383CE92592AEE8420CFD22B1F10EADE71365629"),
        new("Intro26", 1024, 768, "8C334BD5CDE76C88EF9C08ED24B2B116188BA5B0EAB27EFDAE1EF49E206CAF15"),
        new("Intro27", 1024, 768, "A3B8081F84126FB1A1B11D37BCCB126E9BC96CADBEF9F25AAF7C38CD12F6BE03"),
        new("BirdAtlas", 1024, 1024, "30E0EF1BD8685015360C4827CC5DAA4A5560117E06A8596A064AD17557FAD624")
    ];

    public readonly record struct BirdGlyph(int AtlasX, int AtlasY, int Width, int Height, int StageX, int StageY)
    {
        public bool IsVisible => Width > 0 && Height > 0;
    }

    // Frame coordinates are generated offline from the original sprite-22 frames.
    // Provenance and per-frame transforms are retained in STARTUP-001Z-motion-provenance.json.
    private static readonly BirdGlyph[] s_birdFrames = LoadBirdFrames();

    public static BirdGlyph BirdAt(int frame)
    {
        if (frame is < 0 or >= BirdFrameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(frame));
        }

        return s_birdFrames[frame];
    }

    public static string IntroDigest(int frame)
    {
        if (frame is < 0 or >= OpeningFrameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(frame));
        }

        return s_assets[frame].Sha256;
    }

    public static RgbaImage LoadOpening(int frame)
    {
        if (frame is < 0 or >= OpeningFrameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(frame));
        }

        return Load(s_assets[frame]);
    }

    public static RgbaImage LoadBirdAtlas() => Load(s_assets[OpeningFrameCount]);

    private static BirdGlyph[] LoadBirdFrames()
    {
        using Stream stream = typeof(LoginRetailMotionArtwork).Assembly.GetManifestResourceStream(Prefix + "BirdFrames.bin")
            ?? throw new InvalidDataException("Missing trusted 5517 bird timeline.");
        byte[] bytes = new byte[BirdFrameCount * BirdRecordBytes];
        try
        {
            stream.ReadExactly(bytes);
        }
        catch (EndOfStreamException exception) { throw new InvalidDataException("Truncated 5517 bird timeline.", exception); }
        if (stream.ReadByte() != -1 || !SHA256.HashData(bytes).AsSpan().SequenceEqual(Convert.FromHexString(BirdTableHash)))
        {
            throw new InvalidDataException("Invalid 5517 bird timeline integrity.");
        }

        BirdGlyph[] frames = new BirdGlyph[BirdFrameCount];
        for (int index = 0; index < frames.Length; index++)
        {
            ReadOnlySpan<byte> data = bytes.AsSpan(index * BirdRecordBytes, BirdRecordBytes);
            int x = BinaryPrimitives.ReadInt32LittleEndian(data);
            int y = BinaryPrimitives.ReadInt32LittleEndian(data[4..]);
            int width = BinaryPrimitives.ReadInt32LittleEndian(data[8..]);
            int height = BinaryPrimitives.ReadInt32LittleEndian(data[12..]);
            int stageX = BinaryPrimitives.ReadInt32LittleEndian(data[16..]);
            int stageY = BinaryPrimitives.ReadInt32LittleEndian(data[20..]);
            if (width < 0 || height < 0 || (width == 0) != (height == 0) ||
                (width != 0 && (x < 0 || y < 0 || (long)x + width > BirdAtlasWidth || (long)y + height > BirdAtlasHeight)))
            {
                throw new InvalidDataException($"Invalid 5517 bird frame {index}.");
            }

            frames[index] = new BirdGlyph(x, y, width, height, stageX, stageY);
        }
        return frames;
    }

    private static RgbaImage Load(Asset asset)
    {
        using Stream source = typeof(LoginRetailMotionArtwork).Assembly.GetManifestResourceStream(Prefix + asset.Id + ".rgba.br")
            ?? throw new InvalidDataException($"Missing retail motion resource: {asset.Id}.");
        using BrotliStream decoder = new(source, CompressionMode.Decompress);
        byte[] rgba = GC.AllocateUninitializedArray<byte>(checked(asset.Width * asset.Height * 4));
        try
        {
            decoder.ReadExactly(rgba);
        }
        catch (EndOfStreamException exception) { throw new InvalidDataException($"Truncated retail motion: {asset.Id}.", exception); }
        if (decoder.ReadByte() != -1 || !SHA256.HashData(rgba).AsSpan().SequenceEqual(Convert.FromHexString(asset.Sha256)))
        {
            throw new InvalidDataException($"Retail motion integrity failure: {asset.Id}.");
        }

        return new RgbaImage(asset.Width, asset.Height, rgba);
    }
}
