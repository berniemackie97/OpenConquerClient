using System.Text.Json;

namespace OpenConquer.Product.Tool;

/// <summary>Writes the managed-product layout descriptor owned by product composition.</summary>
internal static class ManagedProductDescriptor
{
    public const string FileName = "openconquer.installation.json";
    public const string ClientRoot = "client";

    private const int SchemaVersion = 1;
    private const string ProductId = "OpenConquer";

    public static void Write(string productRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productRootPath);

        if (!Path.IsPathFullyQualified(productRootPath))
        {
            throw new ArgumentException("The managed product root must be fully qualified.", nameof(productRootPath));
        }

        string descriptorPath = Path.Combine(productRootPath, FileName);

        using FileStream stream = new(descriptorPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        using Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", SchemaVersion);
        writer.WriteString("productId", ProductId);
        writer.WriteString("clientRoot", ClientRoot);
        writer.WriteEndObject();
        writer.Flush();
    }
}
