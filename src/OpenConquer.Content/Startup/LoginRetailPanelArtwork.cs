using System.IO.Compression;
using System.Security.Cryptography;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Startup;

/// <summary>Source-verified 5517 modal timeline frames and dynamic history-row skins.</summary>
public static class LoginRetailPanelArtwork
{
    private const string ResourcePrefix = "OpenConquer.Content.Assets.Login.RetailPanel.";
    private readonly record struct Asset(string Name, int Width, int Height, string Sha256);
    private static readonly Asset[] s_assets =
    [
        new("Save02", 1024, 768, "BBD05CF6097AC9B1F89EA29D2542C1B7B67EE46848393895F5A9E43FA1F621E5"),
        new("Save03", 1024, 768, "DE5CF3AC124E2D4F16845DF47B0444E53286C9A502CB4EE5CFDF25D871B60411"),
        new("Save04", 1024, 768, "38C117127A66257D87CD7305E63D0C4401E63B4579840DB92EE32D490264EAA5"),
        new("Save05", 1024, 768, "EAA16728D8C9D28A22FFF85A0DC78F59986397550AE35988F5E1E65DBCB25985"),
        new("Save06", 1024, 768, "3866574DDA33A09E6B67A10B70275AB5EC1A15CE4B22B02AFCB41218FAC51C0E"),
        new("Save07", 1024, 768, "EAA16728D8C9D28A22FFF85A0DC78F59986397550AE35988F5E1E65DBCB25985"),
        new("Save08", 1024, 768, "38C117127A66257D87CD7305E63D0C4401E63B4579840DB92EE32D490264EAA5"),
        new("Save09", 1024, 768, "DE5CF3AC124E2D4F16845DF47B0444E53286C9A502CB4EE5CFDF25D871B60411"),
        new("Clear02", 1024, 768, "BBD05CF6097AC9B1F89EA29D2542C1B7B67EE46848393895F5A9E43FA1F621E5"),
        new("Clear03", 1024, 768, "DE5CF3AC124E2D4F16845DF47B0444E53286C9A502CB4EE5CFDF25D871B60411"),
        new("Clear04", 1024, 768, "38C117127A66257D87CD7305E63D0C4401E63B4579840DB92EE32D490264EAA5"),
        new("Clear05", 1024, 768, "EAA16728D8C9D28A22FFF85A0DC78F59986397550AE35988F5E1E65DBCB25985"),
        new("Clear06", 1024, 768, "E2DA41EB0939D3FB755B8430641E3D2E08B8C133338A4DEE5A3FBAE6D4D65899"),
        new("Clear07", 1024, 768, "EAA16728D8C9D28A22FFF85A0DC78F59986397550AE35988F5E1E65DBCB25985"),
        new("Clear08", 1024, 768, "38C117127A66257D87CD7305E63D0C4401E63B4579840DB92EE32D490264EAA5"),
        new("Clear09", 1024, 768, "DE5CF3AC124E2D4F16845DF47B0444E53286C9A502CB4EE5CFDF25D871B60411"),
        new("Row1", 166, 25, "81DF786E557AA0C2B044452B14D05C2174FDE84010F5E21B4E41A38DC3F0AD2B"),
        new("Row2", 166, 25, "860417D610D934DA6A8E014CB684173B8641B2CB1260F093EA039C20FC9573CA"),
        new("OkOver", 96, 26, "828D65A4FC3A999A28D0C4A582589ECDA6B3B2689E39710900DC5EFC6C840DA1"),
        new("OkDown", 96, 26, "F087E1F4F6232E45C137EB112D64D5288910DCB058CE11FA44CFA2CD8E778CFA"),
        new("CancelOver", 96, 26, "10629CD4129577E9D8F81234684C1EE0E108149C91A825F26DA72A789CAEE397"),
        new("CancelDown", 96, 26, "84A597C866A4699F4892AE8E1031D9C4B5B3D1C53CEB5FBA4C0F758EBDDBEDD3"),
    ];

    public static RgbaImage LoadDialog(int kind, int frame)
    {
        if (kind is < 0 or > 1 || frame is < 2 or > 9)
            throw new ArgumentOutOfRangeException(nameof(frame));
        return Load(s_assets[checked(kind * 8 + frame - 2)]);
    }

    public static string DialogDigest(int kind, int frame)
    {
        if (kind is < 0 or > 1 || frame is < 2 or > 9)
            throw new ArgumentOutOfRangeException(nameof(frame));
        return s_assets[kind * 8 + frame - 2].Sha256;
    }

    public static RgbaImage LoadRow(bool highlighted) => Load(s_assets[highlighted ? 17 : 16]);

    public static RgbaImage LoadDialogButton(int button, int state)
    {
        if (button is < 0 or > 1 || state is < 1 or > 2)
            throw new ArgumentOutOfRangeException(nameof(state));
        return Load(s_assets[18 + button * 2 + state - 1]);
    }

    private static RgbaImage Load(Asset asset)
    {
        using Stream source = typeof(LoginRetailPanelArtwork).Assembly.GetManifestResourceStream(ResourcePrefix + asset.Name + ".rgba.br")
            ?? throw new InvalidDataException("A trusted 5517 login resource is missing.");
        using BrotliStream decoder = new(source, CompressionMode.Decompress);
        byte[] rgba = GC.AllocateUninitializedArray<byte>(checked(asset.Width * asset.Height * 4));
        try
        {
            decoder.ReadExactly(rgba);
        }
        catch (EndOfStreamException ex) { throw new InvalidDataException("Truncated retail UI resource.", ex); }
        if (decoder.ReadByte() != -1 || !SHA256.HashData(rgba).AsSpan().SequenceEqual(Convert.FromHexString(asset.Sha256)))
            throw new InvalidDataException("Retail UI resource failed integrity verification.");
        return new RgbaImage(asset.Width, asset.Height, rgba);
    }
}
