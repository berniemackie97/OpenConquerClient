using System.Text.Json;
using OpenConquer.Launcher.Settings;

namespace OpenConquer.Launcher.Play;

/// <summary>The last start mode and the per-mode counters: the launcher's <c>ini/GameSetup.ini [GameMode]</c>.</summary>
internal readonly record struct GameModeRecord(GameModeUsage Usage, GameMode LastMode);

/// <summary>
/// <c>play.json</c> beside the display preferences. Like the retail counters it carries no authority: a missing
/// or damaged file reads as unused and is replaced on the next start; a newer schema is read as unused and never
/// overwritten.
/// </summary>
internal sealed class GameModeStore
{
    internal const int MaximumLength = 1024;
    private const string FileName = "play.json";

    private readonly string? _directory;
    private readonly object _gate = new();

    public GameModeStore(string? directory)
    {
        if (directory is not null && !Path.IsPathFullyQualified(directory))
        {
            throw new ArgumentException("Settings directory must be absolute.", nameof(directory));
        }

        _directory = directory;
    }

    public static GameModeStore ForCurrentUser() => new(LauncherConfigurationDirectory.ForCurrentUser());

    public Task<GameModeRecord> LoadAsync(CancellationToken cancellationToken) =>
        Task.Run(() => { lock (_gate) { return Read(cancellationToken).Record; } }, cancellationToken);

    /// <summary>Counts one start in <paramref name="mode"/>; returns false when the file could not be written.</summary>
    public Task<bool> RecordStartAsync(GameMode mode, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown start mode.");
        }

        return Task.Run(() =>
        {
            lock (_gate)
            {
                (GameModeRecord current, bool writable, bool exists) = Read(cancellationToken);
                if (!writable || _directory is null)
                {
                    return false;
                }

                try
                {
                    GameModeRecord next = new(current.Usage.Record(mode), mode);
                    SettingsFile.Replace(_directory, FileName, Serialize(next), exists, cancellationToken);
                    return true;
                }
                catch (UnauthorizedAccessException) { return false; }
                catch (IOException) { return false; }
            }
        }, cancellationToken);
    }

    internal static byte[] Serialize(GameModeRecord record)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteNumber("highTimes", record.Usage.HighTimes);
            writer.WriteNumber("standardTimes", record.Usage.StandardTimes);
            writer.WriteNumber("lowTimes", record.Usage.LowTimes);
            writer.WriteString("lastMode", Name(record.LastMode));
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    /// <summary>Parses a document; null when it is damaged, and <paramref name="newer"/> when a later schema wrote it.</summary>
    internal static GameModeRecord? Parse(ReadOnlyMemory<byte> bytes, out bool newer)
    {
        newer = false;
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 4 });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !TryReadCount(root, "schemaVersion", out int schema) || schema < 1)
            {
                return null;
            }

            if (schema > 1)
            {
                newer = true;
                return null;
            }

            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    return null;
                }
            }

            if (names.Count != 5 || !TryReadCount(root, "highTimes", out int high) || !TryReadCount(root, "standardTimes", out int standard)
                || !TryReadCount(root, "lowTimes", out int low) || !root.TryGetProperty("lastMode", out JsonElement last)
                || last.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            GameMode? lastMode = last.GetString() switch
            {
                "standard" => GameMode.Standard,
                "high" => GameMode.High,
                "low" => GameMode.Low,
                _ => null,
            };
            return lastMode is { } mode ? new GameModeRecord(new GameModeUsage(high, standard, low), mode) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private (GameModeRecord Record, bool Writable, bool Exists) Read(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        GameModeRecord unused = new(default, GameMode.Standard);
        if (_directory is null)
        {
            return (unused, false, false);
        }

        try
        {
            using FileStream stream = SettingsFile.OpenRead(Path.Combine(_directory, FileName));
            if (!stream.CanSeek || stream.Length > MaximumLength)
            {
                return (unused, false, true);
            }

            byte[] bytes = new byte[checked((int)stream.Length)];
            stream.ReadExactly(bytes);
            GameModeRecord? record = Parse(bytes, out bool newer);
            return (record ?? unused, !newer, true);
        }
        catch (FileNotFoundException) { return (unused, true, false); }
        catch (DirectoryNotFoundException) { return (unused, true, false); }
        catch (UnauthorizedAccessException) { return (unused, false, true); }
        catch (IOException) { return (unused, false, true); }
    }

    private static string Name(GameMode mode) => mode switch
    {
        GameMode.Standard => "standard",
        GameMode.High => "high",
        GameMode.Low => "low",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown start mode."),
    };

    private static bool TryReadCount(JsonElement root, string name, out int value)
    {
        value = 0;
        return root.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out value) && value >= 0;
    }
}
