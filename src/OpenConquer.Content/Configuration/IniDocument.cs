using System.Text;

namespace OpenConquer.Content.Configuration;

/// <summary>
/// Parses the retail client's shared INI-store grammar.
/// </summary>
/// <remarks>
/// This intentionally models <c>IniStore_LoadFile</c> and its verified parser helpers rather than
/// applying conventional managed INI normalization. In particular, section names are not trimmed,
/// leading whitespace prevents a key/value line from being considered, and ordinary spaces in
/// values are significant.
/// </remarks>
internal sealed class IniDocument
{
    private readonly Dictionary<string, Dictionary<string, string>> _sections;

    private IniDocument(Dictionary<string, Dictionary<string, string>> sections)
    {
        _sections = sections;
    }

    /// <summary>
    /// Loads a required INI document with <see cref="ContentLookupMode.LooseOnly"/>.
    /// </summary>
    /// <remarks>
    /// Retail parses INI files through <c>IniStore_LoadFile</c> (<c>0x76EF0E</c>), which opens them
    /// with <c>fopen(path, "r")</c> at <c>0x76EF68</c>; the package layer is never consulted.
    /// </remarks>
    public static IniDocument LoadRequired(IClientContentSource source, string contentPath, int maximumLength)
    {
        byte[] bytes = ContentRead.ReadRequiredBytes(source, contentPath, ContentLookupMode.LooseOnly, maximumLength);

        return Parse(bytes);
    }

    public static IniDocument Load(Stream stream, string contentPath, int maximumLength)
    {
        byte[] bytes = ContentRead.ReadBytes(stream, contentPath, maximumLength);

        return Parse(bytes);
    }

    public bool TryGetValue(string sectionName, string keyName, out string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyName);

        if (_sections.TryGetValue(sectionName, out Dictionary<string, string>? section) && section.TryGetValue(keyName, out string? foundValue))
        {
            value = foundValue;
            return true;
        }

        value = null;
        return false;
    }

    private static IniDocument Parse(ReadOnlySpan<byte> bytes)
    {
        string text = Encoding.Latin1.GetString(bytes);

        Dictionary<string, Dictionary<string, string>> sections = new(
            StringComparer.OrdinalIgnoreCase
        );

        Dictionary<string, string>? currentSection = null;

        int lineStart = 0;

        while (lineStart < text.Length)
        {
            int lineFeedIndex = text.IndexOf('\n', lineStart);

            bool hasLineFeed = lineFeedIndex >= 0;

            int lineEnd = hasLineFeed ? lineFeedIndex : text.Length;

            // fopen(..., "r") uses text mode on the retail Windows client, so CRLF reaches fgets
            // as a single LF. Model that translation before applying the verified parser grammar.
            if (hasLineFeed && lineEnd > lineStart && text[lineEnd - 1] == '\r')
            {
                lineEnd--;
            }

            ReadOnlySpan<char> line = text.AsSpan(lineStart, lineEnd - lineStart);

            // fgets retains the terminating LF. IniStore_IsCandidateKeyValueLine uses the C-string
            // length, so the terminator contributes one character to that verified length check.
            int nativeLineLength = line.Length + (hasLineFeed ? 1 : 0);

            if (TryParseSectionName(line, out string? sectionName))
            {
                if (!sections.TryGetValue(sectionName, out currentSection))
                {
                    currentSection = new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase
                    );

                    sections.Add(sectionName, currentSection);
                }
            }
            else if (
                currentSection is not null
                && TryParseKeyValue(line, nativeLineLength, out string? keyName, out string? value)
            )
            {
                currentSection[keyName] = value;
            }

            if (!hasLineFeed)
            {
                break;
            }

            lineStart = lineFeedIndex + 1;
        }

        return new IniDocument(sections);
    }

    /// <summary>
    /// Models <c>IniStore_TryParseSectionHeaderLowercase</c> (<c>0x76ED5D</c>).
    /// </summary>
    private static bool TryParseSectionName(ReadOnlySpan<char> line, out string sectionName)
    {
        if (line.Length == 0 || line[0] != '[')
        {
            sectionName = string.Empty;
            return false;
        }

        int relativeClosingBracketIndex = line[1..].IndexOf(']');

        if (relativeClosingBracketIndex < 0)
        {
            sectionName = string.Empty;
            return false;
        }

        sectionName = line.Slice(start: 1, length: relativeClosingBracketIndex).ToString();

        return true;
    }

    /// <summary>
    /// Models <c>IniStore_TryParseKeyValueLowercaseKey</c> (<c>0x76EDF5</c>) after the
    /// <c>IniStore_IsCandidateKeyValueLine</c> (<c>0x76ECF7</c>) gate.
    /// </summary>
    private static bool TryParseKeyValue(
        ReadOnlySpan<char> line,
        int nativeLineLength,
        out string keyName,
        out string value
    )
    {
        if (!IsCandidateKeyValueLine(line, nativeLineLength))
        {
            keyName = string.Empty;
            value = string.Empty;
            return false;
        }

        int delimiterIndex = line.IndexOf('=');

        if (delimiterIndex <= 0)
        {
            keyName = string.Empty;
            value = string.Empty;
            return false;
        }

        int keyEnd = delimiterIndex;

        while (keyEnd > 0 && line[keyEnd - 1] is ' ' or '\t')
        {
            keyEnd--;
        }

        if (keyEnd == 0)
        {
            keyName = string.Empty;
            value = string.Empty;
            return false;
        }

        keyName = line[..keyEnd].ToString();

        int valueStart = delimiterIndex + 1;

        while (valueStart < line.Length && line[valueStart] is ' ' or '\t')
        {
            valueStart++;
        }

        int valueEnd = valueStart;

        while (valueEnd < line.Length && line[valueEnd] is not ('\t' or ';' or '\r' or '\n'))
        {
            valueEnd++;
        }

        value = line[valueStart..valueEnd].ToString();

        return true;
    }

    private static bool IsCandidateKeyValueLine(ReadOnlySpan<char> line, int nativeLineLength)
    {
        return nativeLineLength > 2
            && line.Length > 0
            && line[0] is not ('\t' or '\n' or '\r' or ' ' or '/' or ';' or '=' or '\\');
    }
}
