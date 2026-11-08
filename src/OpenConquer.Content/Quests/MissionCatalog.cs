using System.Globalization;
using OpenConquer.Content.Configuration;

namespace OpenConquer.Content.Quests;

public sealed class MissionCatalog
{
    public const string RelativePath = "ini/Questinfo.ini";
    public const int MaximumMissionIds = 240;

    private const int MaximumFileLength = 512 * 1024;
    private const int MaximumDeclaredRows = 4096;
    private readonly int[] _ids;

    private MissionCatalog(int[] ids) => _ids = ids;

    public ReadOnlyMemory<int> MissionIds => _ids;

    public static MissionCatalog Load(IClientContentSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        IniDocument document = IniDocument.LoadRequired(source, RelativePath, MaximumFileLength);
        if (!document.TryGetValue("TotalMission", "TotalMission", out string? rawCount) ||
            !int.TryParse(rawCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) ||
            count is < 1 or > MaximumDeclaredRows)
        {
            throw new InvalidDataException("The mission catalog has an invalid declared row count.");
        }

        SortedSet<int> ids = [];
        for (int section = 1; section <= count; section++)
        {
            string sectionName = section.ToString(CultureInfo.InvariantCulture);
            if (!document.TryGetValue(sectionName, "MissionId", out string? rawId) ||
                !TryReadId(rawId, out int missionId) || missionId <= 0)
            {
                throw new InvalidDataException($"The mission catalog has an invalid ID in section [{sectionName}].");
            }

            ids.Add(missionId);
            if (ids.Count > MaximumMissionIds)
            {
                throw new InvalidDataException($"The mission catalog exceeds {MaximumMissionIds} distinct IDs.");
            }
        }

        return new MissionCatalog(ids.ToArray());
    }

    private static bool TryReadId(string? raw, out int id)
    {
        if (raw is not null && raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(raw.AsSpan(2), NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture, out id);
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
    }
}
