using System.Globalization;
using System.Text;

namespace OpenConquer.Content.Configuration;

public sealed class GameSetupConfiguration
{
    public const string RelativePath = "ini/GameSetup.Ini";

    private const string ScreenModeSectionName = "ScreenModeRecord";
    private const string ScreenModeKeyName = "ScreenMode";

    private GameSetupConfiguration(int screenMode)
    {
        ScreenMode = screenMode;
    }

    public int ScreenMode
    {
        get;
    }

    public int LogicalWidthPixels => ScreenMode is 0 or 1 ? 800 : 1024;

    public int LogicalHeightPixels => ScreenMode is 0 or 1 ? 600 : 768;

    public static GameSetupConfiguration Load(ClientContentRoot contentRoot)
    {
        ArgumentNullException.ThrowIfNull(contentRoot);

        string configurationPath = contentRoot.ResolveRequiredFile(RelativePath);
        bool inScreenModeSection = false;

        foreach (string rawLine in File.ReadLines(configurationPath, Encoding.Latin1))
        {
            string line = rawLine.Trim();

            if (line.Length == 0 || line[0] is ';' or '#')
            {
                continue;
            }

            if (line.Length >= 2 && line[0] == '[' && line[^1] == ']')
            {
                string sectionName = line[1..^1].Trim();

                inScreenModeSection = string.Equals(sectionName, ScreenModeSectionName, StringComparison.OrdinalIgnoreCase);

                continue;
            }

            if (!inScreenModeSection)
            {
                continue;
            }

            int delimiterIndex = line.IndexOf('=');

            if (delimiterIndex <= 0)
            {
                continue;
            }

            string keyName = line[..delimiterIndex].Trim();

            if (!string.Equals(keyName, ScreenModeKeyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string value = line[(delimiterIndex + 1)..].Trim();

            if (!int.TryParse(value, style: NumberStyles.Integer, CultureInfo.InvariantCulture, out int screenMode) || screenMode is < 0 or > 3)
            {
                throw new InvalidDataException($"'{RelativePath}' contains an invalid [{ScreenModeSectionName}] {ScreenModeKeyName} value '{value}'. Expected an integer from 0 through 3.");
            }

            return new GameSetupConfiguration(screenMode);
        }

        throw new InvalidDataException($"'{RelativePath}' does not define [{ScreenModeSectionName}] {ScreenModeKeyName}.");
    }
}
