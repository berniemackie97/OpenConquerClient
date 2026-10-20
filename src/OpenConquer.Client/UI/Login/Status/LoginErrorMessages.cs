using System.Globalization;
using System.Text;
using OpenConquer.Content;
using OpenConquer.Content.Text;

namespace OpenConquer.Client.UI.Login.Status;

/// <summary>Retail resource lookup and plain-text formatting at the presentation boundary.</summary>
internal sealed class LoginErrorMessages(ClientStringResources numeric, ClientKeyedStringResources keyed, Encoding encoding)
{
    internal static LoginErrorMessages Load(IClientContentSource source, Encoding encoding) =>
        new(ClientStringResources.Load(source), ClientKeyedStringResources.Load(source), encoding);

    internal string Format(LoginFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure.Kind switch
        {
            // start.dat root frame1 DoAction_3, before CMD_ENTERGAME.
            LoginFailureKind.EmptyAccount => "Please Enter Your User Name.",
            LoginFailureKind.EmptyPassword => "Please Enter Your Password.",
            LoginFailureKind.Resource => Decode(numeric.GetEncoded(checked((int)failure.Code)).Span),
            LoginFailureKind.AccountRefusal => FormatAccount(failure.Code),
            LoginFailureKind.GameRefusal => Decode(failure.Bytes.Span),
            _ => throw new ArgumentOutOfRangeException(nameof(failure)),
        };
    }

    private string FormatAccount(uint code)
    {
        string key = string.Create(CultureInfo.InvariantCulture, $"STR_CONNECT_ERROR_MSG_TYPE_{code:000}");
        ReadOnlyMemory<byte> reason = keyed.GetEncoded(key);
        if (reason.IsEmpty)
        {
            reason = keyed.GetEncoded("STR_CONNECT_ERROR_MSG_TYPE_UNKNOWN");
        }

        // Interpret only the two retail placeholders. Never re-parse inserted server/resource text.
        string template = encoding.GetString(keyed.GetEncoded("STR_LOGIN_FAIL").Span);
        StringBuilder result = new();
        for (int index = 0; index < template.Length; index++)
        {
            if (template[index] == '%' && index + 1 < template.Length)
            {
                char specifier = template[index + 1];
                if (specifier is 's' or 'd' or '%')
                {
                    result.Append(specifier switch
                    {
                        's' => encoding.GetString(reason.Span),
                        'd' => unchecked((int)code).ToString(CultureInfo.InvariantCulture),
                        _ => "%",
                    });
                    index++;
                    continue;
                }
            }

            result.Append(template[index]);
        }

        // 1055 Process copies at most 256 bytes into its NUL-terminated failure buffer.
        byte[] bytes = encoding.GetBytes(result.ToString());
        return Decode(bytes.AsSpan(0, Math.Min(bytes.Length, 256)));
    }

    private string Decode(ReadOnlySpan<byte> bytes)
    {
        int nul = bytes.IndexOf((byte)0);
        string text = encoding.GetString(nul < 0 ? bytes : bytes[..nul]);
        // VCTOFLASH ERROR|text is split on '|'; only element1 reaches getError.
        int separator = text.IndexOf('|', StringComparison.Ordinal);
        return separator < 0 ? text : text[..separator];
    }
}
