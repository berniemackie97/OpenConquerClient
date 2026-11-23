using System.Text;
using OpenConquer.Content;
using OpenConquer.Content.Text;

namespace OpenConquer.Client.UI.Role;

internal sealed class RoleCreationText(ClientStringResources numeric, ClientKeyedStringResources keyed, Encoding encoding)
{
    private const int NameLengthId = 10037;
    private const int NameCharactersId = 11089;
    private const int InitializingId = 11238;
    private const int CreatingMapsId = 11239;
    private const int StartingId = 11240;
    private const string CreatedKey = "STR_CREATE_ROLE_SUCCESS";

    public static RoleCreationText Load(IClientContentSource source, Encoding encoding)
    {
        return new RoleCreationText(ClientStringResources.Load(source), ClientKeyedStringResources.Load(source), encoding);
    }

    public string NameLength => Decode(numeric.GetEncoded(NameLengthId).Span);

    public string NameCharacters => Decode(numeric.GetEncoded(NameCharactersId).Span);

    public string Created => Decode(keyed.GetEncoded(CreatedKey).Span);

    public string Initializing => Decode(numeric.GetEncoded(InitializingId).Span);

    public string CreatingMaps => Decode(numeric.GetEncoded(CreatingMapsId).Span);

    public string Starting => Decode(numeric.GetEncoded(StartingId).Span);

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
