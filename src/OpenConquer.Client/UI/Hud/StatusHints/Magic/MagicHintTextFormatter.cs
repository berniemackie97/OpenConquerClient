using System.Globalization;
using System.Text;

namespace OpenConquer.Client.UI.Hud.StatusHints.Magic;

internal static class MagicHintTextFormatter
{
    public static byte[] Format(ReadOnlySpan<byte> encodedFormat, int maximumBytes, params object[] arguments)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        ArgumentNullException.ThrowIfNull(arguments);

        string format = Encoding.Latin1.GetString(encodedFormat);
        StringBuilder output = new(Math.Min(Math.Min(format.Length, 1024), maximumBytes));
        int argumentIndex = 0;

        for (int index = 0; index < format.Length; index++)
        {
            char character = format[index];

            if (character != '%')
            {
                if (output.Length < maximumBytes)
                {
                    output.Append(character);
                }
                continue;
            }

            index++;

            if (index >= format.Length)
            {
                throw new InvalidDataException("Magic text ends with an incomplete format directive.");
            }

            if (format[index] == '%')
            {
                if (output.Length < maximumBytes)
                {
                    output.Append('%');
                }
                continue;
            }

            if (format.AsSpan(index).StartsWith("0.3f", StringComparison.Ordinal))
            {
                object argument = NextArgument(arguments, ref argumentIndex);
                double percentage = argument is double value
                    ? value
                    : throw new InvalidDataException("The percentage directive requires a double.");

                AppendBounded(output, percentage.ToString("F3", CultureInfo.InvariantCulture), maximumBytes);
                index += 3;
                continue;
            }

            object next = NextArgument(arguments, ref argumentIndex);

            switch (format[index])
            {
                case 'u':
                    AppendBounded(output, next switch
                    {
                        uint value => value.ToString(CultureInfo.InvariantCulture),
                        int value => unchecked((uint)value).ToString(CultureInfo.InvariantCulture),
                        _ => throw new InvalidDataException("The unsigned directive requires a 32-bit integer."),
                    }, maximumBytes);
                    break;

                case 'd':
                    AppendBounded(output, next switch
                    {
                        int value => value.ToString(CultureInfo.InvariantCulture),
                        uint value => unchecked((int)value).ToString(CultureInfo.InvariantCulture),
                        _ => throw new InvalidDataException("The signed directive requires a 32-bit integer."),
                    }, maximumBytes);
                    break;

                case 's':
                    switch (next)
                    {
                        case ReadOnlyMemory<byte> value:
                            AppendEncoded(output, value.Span, maximumBytes);
                            break;
                        case byte[] value:
                            AppendEncoded(output, value, maximumBytes);
                            break;
                        case string value:
                            AppendBounded(output, value, maximumBytes);
                            break;
                        default:
                            throw new InvalidDataException("The string directive requires an encoded string.");
                    }
                    break;

                default:
                    throw new InvalidDataException($"Unsupported magic format directive '%{format[index]}'.");
            }
        }

        if (argumentIndex != arguments.Length)
        {
            throw new InvalidDataException("Magic text received unused formatting arguments.");
        }

        return Encoding.Latin1.GetBytes(output.ToString());
    }

    public static byte[] TranslateEscapes(ReadOnlySpan<byte> encoded)
    {
        byte[] output = new byte[encoded.Length];
        int length = 0;

        for (int index = 0; index < encoded.Length; index++)
        {
            if (encoded[index] == (byte)'\\' && index + 1 < encoded.Length)
            {
                byte next = encoded[index + 1];

                if (next is (byte)'n' or (byte)'t')
                {
                    output[length++] = next == (byte)'n' ? (byte)'\n' : (byte)'\t';
                    index++;
                    continue;
                }
            }

            output[length++] = encoded[index];
        }

        if (length == output.Length)
        {
            return output;
        }

        Array.Resize(ref output, length);
        return output;
    }

    private static void AppendBounded(StringBuilder output, ReadOnlySpan<char> value, int maximumBytes)
    {
        int remaining = Math.Min(maximumBytes - output.Length, value.Length);
        if (remaining > 0)
        {
            output.Append(value[..remaining]);
        }
    }

    private static void AppendEncoded(StringBuilder output, ReadOnlySpan<byte> value, int maximumBytes)
    {
        int remaining = Math.Min(maximumBytes - output.Length, value.Length);
        if (remaining > 0)
        {
            output.Append(Encoding.Latin1.GetString(value[..remaining]));
        }
    }

    private static object NextArgument(object[] arguments, ref int index)
    {
        if ((uint)index >= arguments.Length)
        {
            throw new InvalidDataException("Magic text requires more formatting arguments.");
        }

        return arguments[index++];
    }
}
