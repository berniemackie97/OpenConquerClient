using System.Text.Json;

namespace OpenConquer.Product.Tool;

internal sealed record ManagedProductLayout(string? ActiveRelease, string? FallbackRelease);

/// <summary>Reads and writes the generation selector owned by product composition.</summary>
internal static class ManagedProductDescriptor
{
    public const string FileName = "openconquer.installation.json";
    public const string ReleasesRoot = "releases";
    public const string ClientRoot = "client";

    private const int SchemaVersion = 2;
    private const string ProductId = "OpenConquer";
    private const int MaximumLength = 32 * 1024;

    public static void Write(string productRootPath, string activeRelease, string? fallbackRelease = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productRootPath);

        if (!Path.IsPathFullyQualified(productRootPath))
        {
            throw new ArgumentException("The managed product root must be fully qualified.", nameof(productRootPath));
        }

        ValidateReleaseSelection(activeRelease, fallbackRelease);

        string descriptorPath = Path.Combine(productRootPath, FileName);

        using FileStream stream = new(descriptorPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", SchemaVersion);
        writer.WriteString("productId", ProductId);
        writer.WriteString("activeRelease", activeRelease);
        if (fallbackRelease is null)
        {
            writer.WriteNull("fallbackRelease");
        }
        else
        {
            writer.WriteString("fallbackRelease", fallbackRelease);
        }

        writer.WriteEndObject();
        writer.Flush();
        stream.Flush(flushToDisk: true);

        if (!OperatingSystem.IsWindows())
        {
            UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
            File.SetUnixFileMode(descriptorPath, mode);
        }
    }

    public static ManagedProductLayout Read(string productRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productRootPath);

        string descriptorPath = ProductStagingPathGuard.RequireRegularFile(Path.Combine(productRootPath, FileName), "managed-product installation descriptor");
        byte[] bytes = ProductReleaseManifest.ReadRegularFile(descriptorPath, MaximumLength);

        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("The managed-product installation descriptor is invalid.");
            }

            HashSet<string> properties = new(StringComparer.Ordinal);
            int? schemaVersion = null;
            string? productId = null;
            string? activeRelease = null;
            string? fallbackRelease = null;
            string? clientRoot = null;
            bool fallbackSeen = false;

            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (!properties.Add(property.Name))
                {
                    throw new InvalidDataException("The managed-product installation descriptor is invalid.");
                }

                switch (property.Name)
                {
                    case "schemaVersion" when property.Value.TryGetInt32(out int value):
                        schemaVersion = value;
                        break;
                    case "productId" when property.Value.ValueKind == JsonValueKind.String:
                        productId = property.Value.GetString();
                        break;
                    case "activeRelease" when property.Value.ValueKind == JsonValueKind.String:
                        activeRelease = property.Value.GetString();
                        break;
                    case "fallbackRelease" when property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Null:
                        fallbackRelease = property.Value.GetString();
                        fallbackSeen = true;
                        break;
                    case "clientRoot" when property.Value.ValueKind == JsonValueKind.String:
                        clientRoot = property.Value.GetString();
                        break;
                    default:
                        throw new InvalidDataException("The managed-product installation descriptor is invalid.");
                }
            }

            if (schemaVersion == 1 && properties.SetEquals(["schemaVersion", "productId", "clientRoot"])
                                   && string.Equals(productId, ProductId, StringComparison.Ordinal)
                                   && string.Equals(clientRoot, ClientRoot, StringComparison.Ordinal))
            {
                return new ManagedProductLayout(ActiveRelease: null, FallbackRelease: null);
            }

            if (schemaVersion != SchemaVersion || !properties.SetEquals(["schemaVersion", "productId", "activeRelease", "fallbackRelease"])
                                               || !string.Equals(productId, ProductId, StringComparison.Ordinal) || !fallbackSeen || activeRelease is null)
            {
                throw new InvalidDataException("The managed-product installation descriptor is invalid.");
            }

            ValidateReleaseSelection(activeRelease, fallbackRelease);
            return new ManagedProductLayout(activeRelease, fallbackRelease);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The managed-product installation descriptor is invalid.", exception);
        }
    }

    public static string GetActiveReleaseRoot(string productRootPath)
    {
        string productRoot = ProductStagingPathGuard.RequireDirectory(productRootPath, "managed-product root");
        return GetActiveReleaseRootCore(productRoot, Read(productRoot));
    }

    public static string GetActiveReleaseRoot(string productRootPath, ManagedProductLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        string productRoot = ProductStagingPathGuard.RequireDirectory(productRootPath, "managed-product root");
        return GetActiveReleaseRootCore(productRoot, layout);
    }

    private static string GetActiveReleaseRootCore(string productRoot, ManagedProductLayout layout)
    {
        if (layout.ActiveRelease is null)
        {
            return productRoot;
        }

        string releasesRoot = ProductStagingPathGuard.RequireDirectory(Path.Combine(productRoot, ReleasesRoot), "managed-product releases root");
        return ProductStagingPathGuard.RequireDirectory(Path.Combine(releasesRoot, layout.ActiveRelease), "managed-product active release");
    }

    private static void ValidateReleaseSelection(string activeRelease, string? fallbackRelease)
    {
        if (!ProductReleaseIdentity.IsValid(activeRelease) || fallbackRelease is not null && !ProductReleaseIdentity.IsValid(fallbackRelease)
                                                           || string.Equals(activeRelease, fallbackRelease, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The managed-product release selection is invalid.");
        }
    }
}
