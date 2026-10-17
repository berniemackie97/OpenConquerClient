using System.IO.Compression;
using System.Security.Cryptography;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Startup;

/// <summary>Offline-converted artwork from the clean 5517 start.dat and logo.swf movies.</summary>
public static class LoginRetailFlashArtwork
{
    public const int LogoFrameCount = 25;
    public const int LogoFramesPerSecond = 30;
    private const string ResourcePrefix = "OpenConquer.Content.Assets.Login.Retail.";

    public enum Button
    {
        Register, Purchase, Help, Accounts, Save, Clear,
    }

    public enum State
    {
        Up, Over, Down,
    }

    private readonly record struct Asset(string Id, int Width, int Height, string Sha256);
    // Decoded RGBA SHA-256 from the original SWF symbols; generated and cross-checked offline.
    private static readonly Asset[] s_assets =
    [
        new("RegisterUp", 82, 21, "7D0C934BE3980D1D07D73706BBD4039F08BEF72892791F1BC9371952861E72C2"),
        new("RegisterOver", 82, 21, "6DEDA0F087EC41D3BDC106FAAE8EA98254F71107904592098CA4056D6CD1CF72"),
        new("RegisterDown", 82, 21, "97FC0FCD8B36EFA47EB0E9EF5D63D0AAA52F0B4E02228C924FEB4D8959026F8A"),
        new("PurchaseUp", 109, 21, "3853893B9C990EAD32D942FD264061F1EF4EE836DDD04D7185F736A2719EF7E7"),
        new("PurchaseOver", 109, 21, "48BCF7842A79254C3A12F2BED8E6106BAFCDA9D02CF1D409C27CFD739AF899CE"),
        new("PurchaseDown", 109, 21, "AF2FC4DF4FC45FA0DB2A1095AA43C7B0CE22CCB757E0CD9D8E64CDBF39022EBF"),
        new("HelpUp", 57, 23, "DB68CB506617B052CED2F0B2BE5C1AA07C08AC76A25A4C3664DD0FE6E2EE0795"),
        new("HelpOver", 57, 23, "32FA32C901DD08B18B5F8CB1BCBD8F7A7BF0ACF566446AEADCA1A8484A4D8011"),
        new("HelpDown", 57, 23, "818DE64B3668D39F0A85F29FD9B7E5C35426A0651D92B77BBBC3AC4DFC104F32"),
        new("AccountsUp", 17, 15, "759D4152237793B1FBC15B22C4F9494E31F7F655B8B0CAA4A081D82D0C84EC9F"),
        new("AccountsOver", 17, 15, "E955B2B76F807FAB7489F8528FB08F1C50B61ED7F0102F337B8337D0DD565F33"),
        new("AccountsDown", 17, 15, "1B228B6C9B28352B363494FD605FBD1DE530C3F8F19DC07B4FA4657FBA2A63C5"),
        new("SaveUp", 22, 19, "C28CBDA5B479CC4920300A97F3D298910272A32CD0CD0FB6812D9BA8F410BA3C"),
        new("SaveOver", 22, 19, "FC22C051FE6BE55D7B108D270419D859D8E0015A52987ACF68491EA0E360B1BF"),
        new("SaveDown", 22, 19, "19C8A36140D9661CBC0D4076E39A4928CD90C2314869CF01F05BBED0CBA133AB"),
        new("ClearUp", 20, 21, "E52660C2AAD3072110B29FB5FF5EFC9610C54143DD928BE079B55E5F00BBE065"),
        new("ClearOver", 20, 21, "1F09ECC6BF5212C7AB9F96DD1B015EEB3E3F7B42360EB68C7146522D06C58DED"),
        new("ClearDown", 20, 21, "E0A05885490EFC93676455ED8691ED80179963E7C33389E3C2779CFF166733BD"),
        new("Logo01", 248, 78, "2C2810F30A3AAB5612CCF25F1E49380B64B261C914478C44A8AB0CF16A890734"),
        new("Logo02", 248, 78, "5CE8C0E981FD81647D1992E6A7F7822A4FFC9C4ED40D44B15C6742911FC725A6"),
        new("Logo03", 248, 78, "609825FA03D007AD8D7ED5F1EB00947E0FBE26538A8D3F3E833A0448C3985519"),
        new("Logo04", 248, 78, "28A0AC26522448701BB8A9BB030F868E00EF7F1EF5C8510686125E5EAA976CA2"),
        new("Logo05", 248, 78, "A2D44ED78915537E0EB12618D0280EA107D48A6ABA13A2238A9541D264C61997"),
        new("Logo06", 248, 78, "A2D44ED78915537E0EB12618D0280EA107D48A6ABA13A2238A9541D264C61997"),
        new("Logo07", 248, 78, "0195D3D343AF5CCB1C4658EE03DA749306EC38F5703D732D1B64AAB296282BAA"),
        new("Logo08", 248, 78, "0195D3D343AF5CCB1C4658EE03DA749306EC38F5703D732D1B64AAB296282BAA"),
        new("Logo09", 248, 78, "5097DEB155A6EF9BF7D968344732F019031D667AFA6EBF09E0909CA2A44D7174"),
        new("Logo10", 248, 78, "5097DEB155A6EF9BF7D968344732F019031D667AFA6EBF09E0909CA2A44D7174"),
        new("Logo11", 248, 78, "CC5A312BF2F398A9FE9FE69C4DB9176D5825C83C4B2DFAC713D9A767FAA1912E"),
        new("Logo12", 248, 78, "A69ECD87E98D241EFE856197C59040FDDAD88E9DF745D39B21CEB54022B57F4F"),
        new("Logo13", 248, 78, "4C90C9778101E50A6B5E09E69E997A5611E1CAE5D1636AF672C141104E9BD7F5"),
        new("Logo14", 248, 78, "20E794478E0B5E6D6C13B1181CB46C20B26E05EDBF7BA88CD3079CF1001DF138"),
        new("Logo15", 248, 78, "20E794478E0B5E6D6C13B1181CB46C20B26E05EDBF7BA88CD3079CF1001DF138"),
        new("Logo16", 248, 78, "865634507BEC36AB2AC691F8F9924E50597814DBAA9298A19D1A49D925191D36"),
        new("Logo17", 248, 78, "F0E18EBFD4441B222DE2007160A8C3235B96A1C864F33BAE89DF0CC9D1BB8A74"),
        new("Logo18", 248, 78, "F0E18EBFD4441B222DE2007160A8C3235B96A1C864F33BAE89DF0CC9D1BB8A74"),
        new("Logo19", 248, 78, "4334EBFA072E043B9A893B7CB2EFF53C6F1A2637E07AD7BE699B6A8C4C899714"),
        new("Logo20", 248, 78, "449C5F770CE1ED11FE862C6A21E6CDAC87D470FCE6F43A7A95887A5461F5F1B4"),
        new("Logo21", 248, 78, "449C5F770CE1ED11FE862C6A21E6CDAC87D470FCE6F43A7A95887A5461F5F1B4"),
        new("Logo22", 248, 78, "58D64192BDD1BDCD58D802FFE20C8DA8B7955AB14C95EC5CD146CF9BED20D40A"),
        new("Logo23", 248, 78, "04F716C1A8AC0BC8EAB7800943954DB3925D3DA3CB50A833BD2BD1C1620AC2DA"),
        new("Logo24", 248, 78, "04F716C1A8AC0BC8EAB7800943954DB3925D3DA3CB50A833BD2BD1C1620AC2DA"),
        new("Logo25", 248, 78, "4A1BEDCE38970FBF3032BB2BECD3EAAC6CCDD165348BA5F6A691322FEA992282")
    ];

    public static RgbaImage LoadButton(Button button, State state)
    {
        if (!Enum.IsDefined(button))
        {
            throw new ArgumentOutOfRangeException(nameof(button));
        }

        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        return Load(s_assets[checked((int)button * 3 + (int)state)]);
    }

    public static RgbaImage LoadLogoFrame(int frame)
    {
        if (frame is < 0 or >= LogoFrameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(frame));
        }

        return Load(s_assets[18 + frame]);
    }

    private static RgbaImage Load(Asset asset)
    {
        string resource = ResourcePrefix + asset.Id + ".rgba.br";
        using Stream source = typeof(LoginRetailFlashArtwork).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidDataException($"Missing trusted retail login resource: {asset.Id}.");
        using BrotliStream decoder = new(source, CompressionMode.Decompress);
        byte[] rgba = GC.AllocateUninitializedArray<byte>(checked(asset.Width * asset.Height * 4));
        try
        {
            decoder.ReadExactly(rgba);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException($"Truncated retail login resource: {asset.Id}.", exception);
        }
        if (decoder.ReadByte() != -1 ||
            !SHA256.HashData(rgba).AsSpan().SequenceEqual(Convert.FromHexString(asset.Sha256)))
        {
            throw new InvalidDataException($"Retail login resource integrity failure: {asset.Id}.");
        }

        return new RgbaImage(asset.Width, asset.Height, rgba);
    }
}
