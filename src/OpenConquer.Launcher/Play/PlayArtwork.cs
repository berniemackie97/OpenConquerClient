using System.IO.Compression;
using System.Security.Cryptography;

namespace OpenConquer.Launcher.Play;

/// <summary>An embedded, Brotli-compressed straight-alpha RGBA image and the SHA-256 of its decoded pixels.</summary>
internal readonly record struct PlayImage(string Path, int Width, int Height, string Sha256);

/// <summary>The skin buttons of the retail <c>play.exe</c> dialog.</summary>
internal enum PlayButton
{
    /// <summary>Control 1007, <c>high0x.bmp</c>: "High Definition - Start".</summary>
    High,
    /// <summary>Control 1001, <c>low0x.bmp</c>: "Standard Definition - Start".</summary>
    Standard,
    /// <summary>Control 1008, <c>Simplify0x.bmp</c>: "Low Definition - Start".</summary>
    Low,
    /// <summary>Control 1010, the Low Definition confirmation's Continue.</summary>
    Continue,
    /// <summary>Control 1011, the Low Definition confirmation's Cancel.</summary>
    Return,
    /// <summary>Control 1004.</summary>
    Exit,
    /// <summary>Control 1002, <c>Office0x.bmp</c>.</summary>
    Site,
    /// <summary>Control 1003.</summary>
    Register,
}

internal enum PlayButtonState
{
    Up,
    Over,
    Down,
}

/// <summary>
/// The retail 5517 <c>play.exe</c> artwork (<c>Play/*.bmp</c>), converted offline with the skin colour key
/// RGB(0, 255, 0) made transparent. Every resource must decode to exactly its declared size and SHA-256.
/// </summary>
internal static class PlayArtwork
{
    private const int StateCount = 3;

    /// <summary><c>Play/Main.bmp</c>; its transparent pixels are exactly the area outside <c>Play/Main.rgn</c>.</summary>
    public static readonly PlayImage Main = new("Play/Main.rgba.br", 800, 600, "0414C24060BC77291D8A5BB7944BCBDFC2C6A4810B448CEDB0E104497A68DD7A");

    /// <summary><c>Play/MainS.bmp</c>, foreground picture 1.</summary>
    public static readonly PlayImage Side = new("Play/Side.rgba.br", 225, 600, "F51F9002BE078853A30D9C634762596E6D0115BE8101583F1D2B469EFD22FD41");

    /// <summary><c>Play/Version.bmp</c>, foreground picture 2.</summary>
    public static readonly PlayImage VersionLabel = new("Play/VersionLabel.rgba.br", 67, 21, "B43B0B6BE57E2085DA6637D81560066D4BC117B3830110E659B7D2BA9D78FA4A");

    /// <summary><c>Play/logo.bmp</c>, the disabled control 1005.</summary>
    public static readonly PlayImage Logo = new("Play/Logo.rgba.br", 198, 106, "E06381C305F90D0AC1A0E0B5CAAF51B4A3944968AEE8B2BBDE932EE635A9AFDA");

    /// <summary><c>Play/Hint.bmp</c>, the disabled control 1009 behind the Low Definition confirmation.</summary>
    public static readonly PlayImage Hint = new("Play/Hint.rgba.br", 397, 207, "4EFCB8DB0B7254D0B2C260472ED2BF8141E33FACDCBCE47066D4B145B78E8514");

    // Ordered by button, then state; the three start buttons follow with their faded (variant 1) states.
    private static readonly PlayImage[] s_buttons =
    [
        new("Play/Buttons/HighUp.rgba.br", 210, 80, "52D9B36288E49DE92AD974E6A1071A4E617D15C503303029D83AB2FDE03AA502"),
        new("Play/Buttons/HighOver.rgba.br", 210, 80, "ACA83D4C0C49DF7FB52505DB214225E84BE006D3918F932187D8EBDC4CC63AAF"),
        new("Play/Buttons/HighDown.rgba.br", 210, 80, "4A711CB4D894600FED965B15CB6000918FB9AA0DCB58E9AE55836742F455CF2B"),
        new("Play/Buttons/HighUpFaded.rgba.br", 210, 80, "DEC4C6AB757D7FD2866881A8929BCC37B78CADC0C7D1382122DDAB5FE4D6629E"),
        new("Play/Buttons/HighOverFaded.rgba.br", 210, 80, "7DD933FAD0FD7E17562729A876D583950BD50861BDB80D926268F15A1D05EEC2"),
        new("Play/Buttons/HighDownFaded.rgba.br", 210, 80, "8E5F2D80F2504E61807E20E6E17BBFBD1C7C038610C5BAB2204E8A91D8DA5D72"),
        new("Play/Buttons/StandardUp.rgba.br", 210, 90, "F4FF3A742C4A97C518217F12783C99D0C927AC5EE58ECA1C3B237E43C82529FF"),
        new("Play/Buttons/StandardOver.rgba.br", 210, 90, "3D7775559904A9C04A419B38578FB97312EDCC9F86816D4892BD61BB90E99C98"),
        new("Play/Buttons/StandardDown.rgba.br", 210, 90, "2AD1D4871EB81CDDF568020DF9E4B557FB5BBF4073C3C1C829F0BA599275BEEE"),
        new("Play/Buttons/StandardUpFaded.rgba.br", 210, 90, "C6A39F56439DF3883CED3AEF8C2E58277B5CD8BA6C1EEA421884AF43036E91C1"),
        new("Play/Buttons/StandardOverFaded.rgba.br", 210, 90, "21A7C6FFD70D937E63492A258A77058FB24B65E9D3CA4618C7E6836C3E07B6ED"),
        new("Play/Buttons/StandardDownFaded.rgba.br", 210, 90, "B365D7F506844B75681B66C16C837DA5E29B5EF61C54DBE5BF22077D5E218584"),
        new("Play/Buttons/LowUp.rgba.br", 198, 77, "C3B31379D80C2DBE80CC8BB1E700BE0E678D615919025036C06C90FD13D5B1B0"),
        new("Play/Buttons/LowOver.rgba.br", 198, 77, "675AA9B8C512D081BD901CD2A67A9EF1C4529519AF08C15E82BA2BEF09291FCE"),
        new("Play/Buttons/LowDown.rgba.br", 198, 77, "48F7FD11F822877D2D26DEED5EC17F324B87D444A8A159417B6E695CF90C3EA3"),
        new("Play/Buttons/LowUpFaded.rgba.br", 198, 77, "A189BE9AE97E4142E169C6F8D874925809FE3369F8F670610D44A56B6CA5F754"),
        new("Play/Buttons/LowOverFaded.rgba.br", 198, 77, "0E4AF8921E5EDD8D2FC368F2465F2613994A88E28F27BAE5E437815631B48023"),
        new("Play/Buttons/LowDownFaded.rgba.br", 198, 77, "63B1BC3A46AD84CCB5792B81B22C2541B093E1F06E2A50CDF911830EE3CE78C1"),
        new("Play/Buttons/ContinueUp.rgba.br", 102, 32, "D0E22CE17F4FB01DB87809D03A8E56B4C7B6AA156DE0D407262ED54F0CF073C7"),
        new("Play/Buttons/ContinueOver.rgba.br", 102, 32, "8F256CD28DCD0A00F860BB8A0BB1DDA6ED051353636ABCF04CAC1251EC9A815C"),
        new("Play/Buttons/ContinueDown.rgba.br", 102, 32, "86281E465D593F361BE2307A99254943E22901F396F0AEFDDE230C0BC879940F"),
        new("Play/Buttons/ReturnUp.rgba.br", 102, 32, "4FCB0C8E1E1AA16F701D720059F6798A2931D3B214E8001074DDF0D3D81AC636"),
        new("Play/Buttons/ReturnOver.rgba.br", 102, 32, "DB53308D7ECD3440DFE342AFC3D2BED51A8CBCF626DCABB9BBC5D2BF5442728B"),
        new("Play/Buttons/ReturnDown.rgba.br", 102, 32, "C281109F2FCA91208745A0E1781292033FC18D898CA2A0957BFF303E4382DBAE"),
        new("Play/Buttons/ExitUp.rgba.br", 108, 30, "C3FEA67C323E58F50DC2AA7D9FABF5CF3A1E441851A144BB840AB6D7B907AAB3"),
        new("Play/Buttons/ExitOver.rgba.br", 108, 30, "36864381140349296A0B1733DFF722B0F921C2149745E4CBD6C8DE3D4B374741"),
        new("Play/Buttons/ExitDown.rgba.br", 108, 30, "02D3BE89ABEEE7F7880A89EB40DA7F888F294EE736C3CBDA94AFC243DE9317AC"),
        new("Play/Buttons/SiteUp.rgba.br", 108, 30, "B410E088CCC283D114E07A30096819C57FE85BBA1450A267E100CFFA10F5EF5A"),
        new("Play/Buttons/SiteOver.rgba.br", 108, 30, "273A37E8FC240216BBD9AD2CE123C428B124E43E52EF80CA2B34A6C4234697CD"),
        new("Play/Buttons/SiteDown.rgba.br", 108, 30, "05AF34BDD497608EF6EEB5613D083E37FF6784863E4088660E8C013250BD5413"),
        new("Play/Buttons/RegisterUp.rgba.br", 108, 30, "C1B662DCDA99DFBDFEA5DECB31A62C16E8BD5DAD39119B733A6EF7E857BE33EC"),
        new("Play/Buttons/RegisterOver.rgba.br", 108, 30, "39AAEA505EB31D5C4C4364131AA907856E1ABB39079813A3A13A4A3691D04654"),
        new("Play/Buttons/RegisterDown.rgba.br", 108, 30, "3E24E35C65D639E54F32033143091C6F1C6309461895338AF454D662FC71D78A"),
    ];

    /// <summary><c>Play/Cursor/Normal.ani</c>, byte-identical to the game's <c>data/Cursor/Normal.ani</c>.</summary>
    public static readonly PlayImage[] CursorFrames =
    [
        new("Play/Cursor/Normal0.rgba.br", 32, 32, "21B4788930EB5F60B1BC0B904D4724E04A828505D9A5021FC6867D28D60C47D7"),
        new("Play/Cursor/Normal1.rgba.br", 32, 32, "EB2B3DEFAB9C823C8727B61259F6C3004FD83ADB3B7813BE2B5770836BCEFC75"),
        new("Play/Cursor/Normal2.rgba.br", 32, 32, "4FDFD09BD5A4744789D255512A877C62AD4D35308E0CBC9D521FD71AE3C904DE"),
        new("Play/Cursor/Normal3.rgba.br", 32, 32, "8B7AF6A11E4B0765F227254810D389D7D3B26B838AD2CE15DF59FAABC38F6E05"),
        new("Play/Cursor/Normal4.rgba.br", 32, 32, "54C287EAA5FC69B4C9838BEBBD2215F06E95BE9EB045F470251F924E69C800BE"),
    ];

    /// <summary>The <c>Normal.ani</c> rate: ten jiffies of 1/60 s per frame.</summary>
    public static TimeSpan CursorFrameDuration => TimeSpan.FromSeconds(10d / 60d);

    public static PlayImage Button(PlayButton button, PlayButtonState state, bool faded = false)
    {
        if (!Enum.IsDefined(button) || !Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(button));
        }

        if (faded && button > PlayButton.Low)
        {
            throw new ArgumentException("Only the start buttons have faded artwork.", nameof(faded));
        }

        int startButtons = Math.Min((int)button, (int)PlayButton.Continue);
        int index = startButtons * StateCount * 2 + Math.Max(0, (int)button - (int)PlayButton.Continue) * StateCount
            + (faded ? StateCount : 0) + (int)state;
        return s_buttons[index];
    }

    /// <summary>Decodes an image to straight-alpha RGBA, verifying its length and SHA-256.</summary>
    public static byte[] Load(PlayImage image)
    {
        int length = checked(image.Width * image.Height * 4);
        using Stream resource = typeof(PlayArtwork).Assembly.GetManifestResourceStream("Assets/" + image.Path)
            ?? throw new InvalidDataException($"Embedded launcher artwork '{image.Path}' is missing.");
        using BrotliStream content = new(resource, CompressionMode.Decompress);
        byte[] pixels = GC.AllocateUninitializedArray<byte>(length);

        try
        {
            content.ReadExactly(pixels);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException($"Embedded launcher artwork '{image.Path}' is truncated.", exception);
        }

        if (content.ReadByte() != -1)
        {
            throw new InvalidDataException($"Embedded launcher artwork '{image.Path}' is longer than declared.");
        }

        if (!SHA256.HashData(pixels).AsSpan().SequenceEqual(Convert.FromHexString(image.Sha256)))
        {
            throw new InvalidDataException($"Embedded launcher artwork '{image.Path}' failed integrity verification.");
        }

        return pixels;
    }
}
