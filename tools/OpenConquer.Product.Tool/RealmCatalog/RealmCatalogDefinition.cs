using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OpenConquer.Product.Tool;

/// <summary>Strict publisher-side validation for the client's closed schema-v2 realm view.</summary>
internal static class RealmCatalogDefinition
{
    public const int MaximumBytes = 128 * 1024;
    private static readonly int[] s_iconsPerGroup = [12, 13, 12, 12, 12, 12, 12, 12, 5];

    public static byte[] Compose(ReadOnlySpan<byte> definition, long sequence, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        if (definition.Length is 0 or > MaximumBytes || sequence <= 0)
        {
            throw new InvalidDataException("The realm definition is empty, oversized or has an invalid sequence.");
        }

        // Input definitions may be saved by editors with a UTF-8 BOM; signed output never contains one.
        if (definition.Length >= 3 && definition[0] == 0xef && definition[1] == 0xbb && definition[2] == 0xbf)
        {
            definition = definition[3..];
        }

        try
        {
            using JsonDocument source = JsonDocument.Parse(definition.ToArray(), new JsonDocumentOptions { MaxDepth = 12, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            JsonElement root = source.RootElement;
            RequireObject(root, "regions");
            using MemoryStream buffer = new();
            using (Utf8JsonWriter writer = new(buffer))
            {
                writer.WriteStartObject();
                writer.WriteNumber("schemaVersion", 2);
                writer.WriteString("productId", "OpenConquer");
                writer.WriteNumber("sequence", sequence);
                writer.WriteString("issuedAt", Timestamp(issuedAt));
                writer.WriteString("expiresAt", Timestamp(expiresAt));
                writer.WritePropertyName("regions");
                writer.WriteStartArray();

                HashSet<string> regionIds = new(StringComparer.Ordinal);
                HashSet<string> protocolNames = new(StringComparer.Ordinal);
                int regionCount = 0;
                int totalServers = 0;
                foreach (JsonElement region in RequiredArray(root, "regions").EnumerateArray())
                {
                    if (++regionCount > 64)
                    {
                        throw new InvalidDataException("Too many realm regions.");
                    }

                    RequireObject(region, "id", "name", "servers");
                    string regionId = Id(region, "id");
                    string regionName = Name(region, "name", 48);
                    if (!regionIds.Add(regionId))
                    {
                        throw new InvalidDataException("Duplicate realm region ID.");
                    }

                    writer.WriteStartObject();
                    writer.WriteString("id", regionId);
                    writer.WriteString("name", regionName);
                    writer.WritePropertyName("servers");
                    writer.WriteStartArray();
                    HashSet<string> serverIds = new(StringComparer.Ordinal);
                    int regionServers = 0;
                    foreach (JsonElement server in RequiredArray(region, "servers").EnumerateArray())
                    {
                        if (++regionServers > 128 || ++totalServers > 1024)
                        {
                            throw new InvalidDataException("Too many realm servers.");
                        }

                        RequireObject(server, "id", "name", "protocolServerName", "accountHost", "accountPort", "iconId");
                        string serverId = Id(server, "id");
                        string name = Name(server, "name", 48);
                        string protocol = Name(server, "protocolServerName", 15);
                        string host = Text(server, "accountHost", 253);
                        int port = Integer(server, "accountPort");
                        string icon = Text(server, "iconId", 8);
                        if (!serverIds.Add(serverId) || !protocolNames.Add(protocol))
                        {
                            throw new InvalidDataException("Duplicate server ID or protocol name.");
                        }
                        if (protocol.Any(static c => c is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '(' or ')' or '-')))
                        {
                            throw new InvalidDataException("Native protocol server names allow only ASCII letters, digits, underscore, parentheses and hyphen.");
                        }
                        if (host != host.Trim() || host.Any(static c => c > 127 || char.IsWhiteSpace(c)) ||
                            Uri.CheckHostName(host) is not (UriHostNameType.Dns or UriHostNameType.IPv4) || port is < 1 or > 65535 || !KnownIcon(icon))
                        {
                            throw new InvalidDataException("Invalid account ingress or unsupported bundled realm icon ID.");
                        }

                        writer.WriteStartObject();
                        writer.WriteString("id", serverId);
                        writer.WriteString("name", name);
                        writer.WriteString("protocolServerName", protocol);
                        writer.WriteString("accountHost", host);
                        writer.WriteNumber("accountPort", port);
                        writer.WriteString("iconId", icon);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.Flush();
            }

            if (buffer.Length is 0 or > MaximumBytes)
            {
                throw new InvalidDataException("The serialized signed realm catalog exceeds its size limit.");
            }
            return buffer.ToArray();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Realm definition JSON is malformed.", exception);
        }
    }

    private static void RequireObject(JsonElement element, params string[] fields)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Expected a realm definition JSON object.");
        }
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name) || !fields.Contains(property.Name, StringComparer.Ordinal))
            {
                throw new InvalidDataException("Unknown or duplicate realm definition property.");
            }
        }
        if (seen.Count != fields.Length)
        {
            throw new InvalidDataException("Missing required realm definition property.");
        }
    }

    private static JsonElement RequiredArray(JsonElement parent, string name)
    {
        JsonElement value = parent.GetProperty(name);
        return value.ValueKind == JsonValueKind.Array ? value : throw new InvalidDataException("Expected a realm definition array.");
    }

    private static string Id(JsonElement parent, string name)
    {
        string text = Text(parent, name, 64);
        foreach (char c in text)
        {
            if (c is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-' or '.'))
            {
                throw new InvalidDataException("Realm IDs require printable ASCII letters, digits, '.', '-' or '_'.");
            }
        }
        return text;
    }

    private static string Name(JsonElement parent, string property, int maximum)
    {
        string text = Text(parent, property, maximum);
        if (text != text.Trim())
        {
            throw new InvalidDataException("Realm names cannot have surrounding whitespace.");
        }
        for (ReadOnlySpan<char> remaining = text.AsSpan(); !remaining.IsEmpty;)
        {
            if (Rune.DecodeFromUtf16(remaining, out Rune scalar, out int consumed) != OperationStatus.Done ||
                Rune.GetUnicodeCategory(scalar) is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
            {
                throw new InvalidDataException("Realm names contain an unsupported Unicode scalar.");
            }
            remaining = remaining[consumed..];
        }
        return text;
    }

    private static string Text(JsonElement parent, string property, int maximum)
    {
        JsonElement value = parent.GetProperty(property);
        if (value.ValueKind != JsonValueKind.String || value.GetString() is not { } text || text.Length is 0 || text.Length > maximum)
        {
            throw new InvalidDataException($"Invalid realm field '{property}'.");
        }
        return text;
    }

    private static int Integer(JsonElement parent, string property)
    {
        JsonElement value = parent.GetProperty(property);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) ? number : throw new InvalidDataException($"Invalid realm integer field '{property}'.");
    }

    private static bool KnownIcon(string id)
    {
        if (id == "none")
        {
            return true;
        }
        if (id.Length != 6 || id[0] != 'g' || id[3] != 's' || id[1] is < '0' or > '9' ||
            id[2] is < '0' or > '9' || id[4] is < '0' or > '9' || id[5] is < '0' or > '9')
        {
            return false;
        }
        int group = (id[1] - '0') * 10 + id[2] - '0';
        int server = (id[4] - '0') * 10 + id[5] - '0';
        return group >= 1 && group <= s_iconsPerGroup.Length && server >= 1 && server <= s_iconsPerGroup[group - 1];
    }

    private static string Timestamp(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
