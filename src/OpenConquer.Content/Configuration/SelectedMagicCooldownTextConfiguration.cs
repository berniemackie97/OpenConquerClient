using System.Globalization;

namespace OpenConquer.Content.Configuration;

/// <summary>
/// Loads the selected-magic cooldown-number configuration used by the main HUD.
/// </summary>
public sealed class SelectedMagicCooldownTextConfiguration
{
    public const string RelativePath = "ini/info.ini";
    public const int DefaultOffsetX = 0;
    public const int DefaultOffsetY = 0;
    public const int DefaultFontSizePixels = 20;
    public const uint DefaultColorArgb = uint.MaxValue;

    private const int MaximumFileLength = 64 * 1024;
    private const string SectionName = "SelectMagicNum";

    private SelectedMagicCooldownTextConfiguration(int offsetX, int offsetY, int fontSizePixels, uint colorArgb)
    {
        OffsetX = offsetX;
        OffsetY = offsetY;
        FontSizePixels = fontSizePixels;
        ColorArgb = colorArgb;
    }

    public int OffsetX
    {
        get;
    }

    public int OffsetY
    {
        get;
    }

    public int FontSizePixels
    {
        get;
    }

    public uint ColorArgb
    {
        get;
    }

    public static SelectedMagicCooldownTextConfiguration Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        IniDocument document = IniDocument.LoadRequired(contentSource, RelativePath, MaximumFileLength);

        return new SelectedMagicCooldownTextConfiguration(ReadInt32(document, "OffsetX", DefaultOffsetX), ReadInt32(document, "OffsetY", DefaultOffsetY), ReadInt32(document, "FontSize", DefaultFontSizePixels), ReadColorArgb(document));
    }

    private static int ReadInt32(IniDocument document, string keyName, int defaultValue)
    {
        return document.TryGetValue(SectionName, keyName, out string? value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : defaultValue;
    }

    private static uint ReadColorArgb(IniDocument document)
    {
        if (!document.TryGetValue(SectionName, "Color", out string? value))
        {
            return DefaultColorArgb;
        }

        ReadOnlySpan<char> text = value.AsSpan().Trim();

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && uint.TryParse(text[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint hex))
        {
            return hex;
        }

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int signed)
            ? unchecked((uint)signed)
            : DefaultColorArgb;
    }
}
