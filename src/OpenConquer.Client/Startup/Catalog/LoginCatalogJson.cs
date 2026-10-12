using System.Text.Json;

namespace OpenConquer.Client.Startup.Catalog;

/// <summary>Strict, duplicate- and unknown-property-rejecting catalog JSON primitives.</summary>
internal static class LoginCatalogJson
{
    public static JsonDocument Parse(ReadOnlySpan<byte> bytes)
    {
        try
        {
            return JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions
            {
                MaxDepth = 12,
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The catalog JSON is malformed.", exception);
        }
    }

    public static void RequireObject(JsonElement element, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A catalog object was expected.");
        }

        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name) || Array.IndexOf(expected, property.Name) < 0)
            {
                throw new InvalidDataException("The catalog contains duplicate or unsupported properties.");
            }
        }

        if (seen.Count != expected.Length)
        {
            throw new InvalidDataException("The catalog is missing required properties.");
        }
    }

    public static string RequiredString(JsonElement parent, string property, int maxLength)
    {
        JsonElement value = parent.GetProperty(property);

        if (value.ValueKind != JsonValueKind.String || value.GetString() is not { } text ||
            text.Length is 0 || > maxLength)
        {
            throw new InvalidDataException($"Catalog property '{property}' is invalid.");
        }

        return text;
    }

    public static int RequiredInteger(JsonElement parent, string property)
    {
        JsonElement value = parent.GetProperty(property);

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int number))
        {
            throw new InvalidDataException($"Catalog property '{property}' must be an integer.");
        }

        return number;
    }

    public static long RequiredPositiveLong(JsonElement parent, string property)
    {
        JsonElement value = parent.GetProperty(property);

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long number) || number <= 0)
        {
            throw new InvalidDataException($"Catalog property '{property}' must be a positive integer.");
        }

        return number;
    }

    public static JsonElement RequiredArray(JsonElement parent, string property)
    {
        JsonElement value = parent.GetProperty(property);

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Catalog property '{property}' must be an array.");
        }

        return value;
    }
}
