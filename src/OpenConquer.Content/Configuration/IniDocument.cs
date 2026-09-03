using System.Text;

namespace OpenConquer.Content.Configuration;

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
    /// Retail parses INI files through <c>IniStore_LoadFile</c> (<c>0x76EF13</c>), which opens them
    /// with <c>fopen(path, "r")</c> at <c>0x76EF68</c>; the package layer is never consulted.
    /// </remarks>
    public static IniDocument LoadRequired(
        IClientContentSource source,
        string contentPath,
        int maximumLength)
    {
        byte[] bytes = ContentRead.ReadRequiredBytes(source, contentPath, ContentLookupMode.LooseOnly, maximumLength);
        return Parse(bytes);
    }

    public static IniDocument Load(
        Stream stream,
        string contentPath,
        int maximumLength)
    {
        byte[] bytes = ContentRead.ReadBytes(stream, contentPath, maximumLength);
        return Parse(bytes);
    }

    public bool TryGetValue(string sectionName, string keyName, out string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyName);

        if (_sections.TryGetValue(sectionName, out Dictionary<string, string>? section)
            && section.TryGetValue(keyName, out string? foundValue))
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
        Dictionary<string, Dictionary<string, string>> sections = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? currentSection = null;

        foreach (string rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();

            if (line.Length == 0 || line[0] is ';' or '#')
            {
                continue;
            }

            if (line.Length >= 2 && line[0] == '[' && line[^1] == ']')
            {
                string sectionName = line[1..^1].Trim();

                if (sectionName.Length == 0)
                {
                    currentSection = null;
                    continue;
                }

                if (!sections.TryGetValue(sectionName, out currentSection))
                {
                    currentSection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    sections.Add(sectionName, currentSection);
                }

                continue;
            }

            if (currentSection is null)
            {
                continue;
            }

            int delimiterIndex = line.IndexOf('=');

            if (delimiterIndex <= 0)
            {
                continue;
            }

            string keyName = line[..delimiterIndex].Trim();

            if (keyName.Length == 0)
            {
                continue;
            }

            currentSection[keyName] = line[(delimiterIndex + 1)..].Trim();
        }

        return new IniDocument(sections);
    }
}
