using System.Globalization;
using System.Text;

namespace OpenConquer.Client.UI.Hud.StatusHints.Magic;

internal static class NativeMagicStringFormatter
{
    public static byte[] Format(ReadOnlySpan<byte> encodedFormat, int maximumBytes, params object[] arguments)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        ArgumentNullException.ThrowIfNull(arguments);

        string format = Encoding.Latin1.GetString(encodedFormat);
        StringBuilder output = new(Math.Min(format.Length + 32, 1024));
        int argumentIndex = 0;

        for (int index = 0; index < format.Length; index++)
        {
            char character = format[index];

            if (character != '%')
            {
                output.Append(character);
                continue;
            }

            index++;

            if (index >= format.Length)
            {
                throw new InvalidDataException("Native magic text ends with an incomplete format directive.");
            }

            if (format[index] == '%')
            {
                output.Append('%');
                continue;
            }

            if (format.AsSpan(index).StartsWith("0.3f", StringComparison.Ordinal))
            {
                object argument = NextArgument(arguments, ref argumentIndex);
                double percentage = argument is double value
                    ? value
                    : throw new InvalidDataException("The native percentage directive requires a double.");

                output.Append(percentage.ToString("F3", CultureInfo.InvariantCulture));
                index += 3;
                continue;
            }

            object next = NextArgument(arguments, ref argumentIndex);

            switch (format[index])
            {
                case 'u':
                    output.Append(next switch
                    {
                        uint value => value.ToString(CultureInfo.InvariantCulture),
                        int value => unchecked((uint)value).ToString(CultureInfo.InvariantCulture),
                        _ => throw new InvalidDataException("The native unsigned directive requires a 32-bit integer."),
                    });
                    break;

                case 'd':
                    output.Append(next switch
                    {
                        int value => value.ToString(CultureInfo.InvariantCulture),
                        uint value => unchecked((int)value).ToString(CultureInfo.InvariantCulture),
                        _ => throw new InvalidDataException("The native signed directive requires a 32-bit integer."),
                    });
                    break;

                case 's':
                    output.Append(next switch
                    {
                        ReadOnlyMemory<byte> value => Encoding.Latin1.GetString(value.Span),
                        byte[] value => Encoding.Latin1.GetString(value),
                        string value => value,
                        _ => throw new InvalidDataException("The native string directive requires an encoded string."),
                    });
                    break;

                default:
                    throw new InvalidDataException($"Unsupported native magic format directive '%{format[index]}'.");
            }
        }

        if (argumentIndex != arguments.Length)
        {
            throw new InvalidDataException("Native magic text received unused formatting arguments.");
        }

        byte[] encoded = Encoding.Latin1.GetBytes(output.ToString());

        return encoded.Length <= maximumBytes ? encoded : encoded.AsSpan(0, maximumBytes).ToArray();
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

        return output.AsSpan(0, length).ToArray();
    }

    private static object NextArgument(object[] arguments, ref int index)
    {
        if ((uint)index >= arguments.Length)
        {
            throw new InvalidDataException("Native magic text requires more formatting arguments.");
        }

        return arguments[index++];
    }
}
