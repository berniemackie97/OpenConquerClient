using System.Globalization;
using System.Text;
using OpenConquer.Content.Tables;

namespace OpenConquer.Content.Progression;

public sealed class LevelExperienceTable
{
    public const string RelativePath = "ini/LevExp.dat";

    private const int MaximumFileLength = 256 * 1024;
    private const int MaximumLevel = 150;
    private readonly ulong[] _nextLevelExperience;

    private LevelExperienceTable(ulong[] nextLevelExperience) =>
        _nextLevelExperience = nextLevelExperience;

    public ulong GetNextLevelExperience(uint level) =>
        level is >= 1 and <= MaximumLevel && level <= _nextLevelExperience.Length
            ? _nextLevelExperience[level - 1] : 0;

    public static LevelExperienceTable Load(IClientContentSource source)
    {
        byte[] encoded = ContentReader.ReadRequiredBytes(source, RelativePath,
            ContentLookupMode.LooseOnly, MaximumFileLength);
        return Parse(encoded);
    }

    public static LevelExperienceTable Parse(ReadOnlySpan<byte> encoded)
    {
        if (encoded.IsEmpty || encoded.Length > MaximumFileLength)
        {
            throw new InvalidDataException("Level experience data has an invalid length.");
        }

        byte[] decoded = encoded.ToArray();
        DelimitedTableCipher.DecodeInPlace(decoded, 0x4D2);

        List<ulong> requirements = [];
        using StringReader reader = new(Encoding.Latin1.GetString(decoded));
        int lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] fields = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 5 ||
                !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int key) ||
                !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int level) ||
                !long.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out long requirement) ||
                !long.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
                !uint.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
                level is < 1 or > MaximumLevel)
            {
                throw new InvalidDataException($"Level experience record {lineNumber} is invalid.");
            }

            if (key != 0)
            {
                continue;
            }

            if (level != requirements.Count + 1 || requirement < 0)
            {
                throw new InvalidDataException($"Player experience record {lineNumber} is out of sequence.");
            }

            requirements.Add((ulong)requirement);
        }

        if (requirements.Count == 0)
        {
            throw new InvalidDataException("Level experience data contains no player levels.");
        }

        return new LevelExperienceTable([.. requirements]);
    }
}
