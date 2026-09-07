using System.Text.Json;

namespace OpenConquer.Launcher.Settings;

internal enum SettingsIssue
{
    Invalid, NewerVersion, Unavailable, Changed
}

internal abstract record SettingsReadResult
{
    private SettingsReadResult()
    {
    }
    internal sealed record Loaded(DisplayPreferences Preferences, string? Revision) : SettingsReadResult;
    internal sealed record Rejected(SettingsIssue Issue, string? Revision = null) : SettingsReadResult;
}

internal static class DisplaySettingsDocument
{
    internal const int MaximumLength = 4096;

    internal static SettingsReadResult Parse(ReadOnlyMemory<byte> bytes, string revision)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 4 });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new SettingsReadResult.Rejected(SettingsIssue.Invalid, revision);
            }

            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    return new SettingsReadResult.Rejected(SettingsIssue.Invalid, revision);
                }
            }

            if (!root.TryGetProperty("schemaVersion", out JsonElement version) ||
                version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int schema) || schema < 1)
            {
                return new SettingsReadResult.Rejected(SettingsIssue.Invalid, revision);
            }

            if (schema > 1)
            {
                return new SettingsReadResult.Rejected(SettingsIssue.NewerVersion, revision);
            }

            if (names.Count != 5 || !TryReadInt(root, "width", out int width) || !TryReadInt(root, "height", out int height)
                || !DisplayPreferences.IsValidSize(width, height) || !root.TryGetProperty("windowMode", out JsonElement mode)
                || mode.ValueKind != JsonValueKind.String || !root.TryGetProperty("presentation", out JsonElement presentation)
                || presentation.ValueKind != JsonValueKind.String)
            {
                return new SettingsReadResult.Rejected(SettingsIssue.Invalid, revision);
            }

            GameWindowMode? windowMode = mode.GetString() switch
            {
                "resizable" => GameWindowMode.Resizable,
                "fixed" => GameWindowMode.Fixed,
                "fullscreen" => GameWindowMode.Fullscreen,
                _ => null
            };
            GamePresentation? scaling = presentation.GetString() switch
            {
                "fit" => GamePresentation.Fit,
                "integer" => GamePresentation.Integer,
                "stretch" => GamePresentation.Stretch,
                _ => null
            };
            return windowMode is { } validMode && scaling is { } validScaling
                ? new SettingsReadResult.Loaded(new DisplayPreferences(width, height, validMode, validScaling), revision)
                : new SettingsReadResult.Rejected(SettingsIssue.Invalid, revision);
        }
        catch (JsonException) { return new SettingsReadResult.Rejected(SettingsIssue.Invalid, revision); }
    }

    internal static byte[] Serialize(DisplayPreferences preferences)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteNumber("width", preferences.Width);
            writer.WriteNumber("height", preferences.Height);
            writer.WriteString("windowMode", preferences.WindowMode switch
            {
                GameWindowMode.Resizable => "resizable",
                GameWindowMode.Fixed => "fixed",
                GameWindowMode.Fullscreen => "fullscreen",
                _ => throw new InvalidOperationException("Invalid window mode.")
            });
            writer.WriteString("presentation", preferences.Presentation switch
            {
                GamePresentation.Fit => "fit",
                GamePresentation.Integer => "integer",
                GamePresentation.Stretch => "stretch",
                _ => throw new InvalidOperationException("Invalid presentation.")
            });
            writer.WriteEndObject();
        }
        return buffer.ToArray();
    }

    private static bool TryReadInt(JsonElement root, string name, out int value)
    {
        value = 0;
        return root.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value);
    }
}
