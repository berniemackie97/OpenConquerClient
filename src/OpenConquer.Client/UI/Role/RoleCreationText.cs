using System.Text;
using OpenConquer.Content;
using OpenConquer.Content.Text;

namespace OpenConquer.Client.UI.Role;

/// <summary>
/// The host-side create-role texts, read from the client's string tables as the native host reads them
/// (20_notes Architecture/12_create_role_flow.md, "StrRes / Cn_Res"), and decoded at the Flash bridge code page.
/// </summary>
internal sealed class RoleCreationText(ClientStringResources numeric, ClientKeyedStringResources keyed, Encoding encoding)
{
    private const int NameLengthId = 10037;
    private const int NameCharactersId = 11089;
    private const int InitializingId = 11238;
    private const int CreatingMapsId = 11239;
    private const int StartingId = 11240;
    private const string CreatedKey = "STR_CREATE_ROLE_SUCCESS";

    public static RoleCreationText Load(IClientContentSource source, Encoding encoding) =>
        new(ClientStringResources.Load(source), ClientKeyedStringResources.Load(source), encoding);

    /// <summary>StrRes 10037: the name is empty or not 4 to 15 bytes.</summary>
    public string NameLength => Decode(numeric.GetEncoded(NameLengthId).Span);

    /// <summary>StrRes 11089: a character of the name fails the glyph check.</summary>
    public string NameCharacters => Decode(numeric.GetEncoded(NameCharactersId).Span);

    /// <summary>Cn_Res <c>STR_CREATE_ROLE_SUCCESS</c>, after channel 2100 <c>ANSWER_OK</c>.</summary>
    public string Created => Decode(keyed.GetEncoded(CreatedKey).Span);

    /// <summary>StrRes 11238, after channel 2101 <c>ANSWER_OK</c> admits the created character.</summary>
    public string Initializing => Decode(numeric.GetEncoded(InitializingId).Span);

    /// <summary>StrRes 11239, on each map tick until the loading bar may resume.</summary>
    public string CreatingMaps => Decode(numeric.GetEncoded(CreatingMapsId).Span);

    /// <summary>StrRes 11240, with <c>PROGRESS 80</c> once <c>CMD_GAMESTART</c> and the map are both ready.</summary>
    public string Starting => Decode(numeric.GetEncoded(StartingId).Span);

    /// <summary>A create-role channel refusal: untrusted server text, cut at its NUL, without control characters.</summary>
    public string Refusal(ReadOnlySpan<byte> bytes)
    {
        string text = Decode(bytes);
        return string.Create(text.Length, text, static (destination, source) =>
        {
            for (int index = 0; index < source.Length; index++)
            {
                destination[index] = char.IsControl(source[index]) ? ' ' : source[index];
            }
        });
    }

    private string Decode(ReadOnlySpan<byte> bytes)
    {
        int nul = bytes.IndexOf((byte)0);
        return encoding.GetString(nul < 0 ? bytes : bytes[..nul]);
    }
}
