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

        // Format the native narrow strings before ACP decoding. 754AD8 copies 0x100
        // bytes to ebp-0x930; 754B1C writes NUL at ebp-0x831 (offset 255).
        byte[] buffer = new byte[255];
        int length = 0;
        ReadOnlySpan<byte> template = keyed.GetEncoded("STR_LOGIN_FAIL").Span;
        for (int index = 0; index < template.Length && length < buffer.Length && template[index] != 0; index++)
        {
            if (template[index] == '%' && index + 1 < template.Length)
            {
                byte specifier = template[index + 1];
                if (specifier is (byte)'s' or (byte)'d' or (byte)'%')
                {
                    if (specifier == 's')
                    {
                        ReadOnlySpan<byte> value = reason.Span;
                        int nul = value.IndexOf((byte)0);
                        Append(nul < 0 ? value : value[..nul]);
                    }
                    else if (specifier == 'd')
                    {
                        Append(Encoding.ASCII.GetBytes(unchecked((int)code).ToString(CultureInfo.InvariantCulture)));
                    }
                    else
                    {
                        buffer[length++] = (byte)'%';
                    }

                    index++;
                    continue;
                }
            }

            buffer[length++] = template[index];
        }

        return Decode(buffer.AsSpan(0, length));

        void Append(ReadOnlySpan<byte> value)
        {
            int count = Math.Min(value.Length, buffer.Length - length);
            value[..count].CopyTo(buffer.AsSpan(length));
            length += count;
        }
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
