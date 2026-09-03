using System.Text;

namespace OpenConquer.Content.Configuration;

internal sealed class LegacyIniDocument
{
    private readonly Dictionary<string, Dictionary<string, string>> _sections;

    private LegacyIniDocument(Dictionary<string, Dictionary<string, string>> sections)
    {
        _sections = sections;
    }

    public static LegacyIniDocument LoadRequired(
        IClientContentSource source,
        string contentPath,
        int maximumLength)
    {
        byte[] bytes = ContentRead.ReadRequiredBytes(source, contentPath, maximumLength);
        return Parse(bytes);
    }

    public static LegacyIniDocument Load(
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

    private static LegacyIniDocument Parse(ReadOnlySpan<byte> bytes)
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

        return new LegacyIniDocument(sections);
    }
}
