using System.Text.Json;

namespace OpenConquer.Product.Tool;

/// <summary>Validates publisher public keys and writes the launcher release-trust document.</summary>
internal static class ProductReleaseTrust
{
    public const string FileName = "release-trust.json";
    public const int MaximumKeyCount = 8;

    private const int CurrentSchemaVersion = 1;

    public static void Create(ReleaseTrustOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.PublicKeyPaths);

        if (options.PublicKeyPaths.Count is 0 or > MaximumKeyCount)
        {
            throw new ArgumentException($"Release trust requires between 1 and {MaximumKeyCount} public keys.", nameof(options));
        }

        string outputPath = ProductStagingPathGuard.NormalizePath(options.OutputPath, nameof(options.OutputPath));
        StringComparison pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        Dictionary<string, byte[]> publicKeys = new(StringComparer.Ordinal);

        foreach (string publicKeyPath in options.PublicKeyPaths)
        {
            string normalizedPublicKeyPath = ProductStagingPathGuard.NormalizePath(publicKeyPath, nameof(options.PublicKeyPaths));

            if (string.Equals(normalizedPublicKeyPath, outputPath, pathComparison))
            {
                throw new InvalidOperationException("The release-trust output must not replace a public-key input.");
            }

            byte[] publicKey = ProductReleaseSignature.ExportSubjectPublicKeyInfo(normalizedPublicKeyPath);
            string keyId = ProductReleaseSignature.GetKeyId(publicKey);

            if (!publicKeys.TryAdd(keyId, publicKey))
            {
                throw new InvalidDataException("Release trust must not contain duplicate publisher keys.");
            }
        }

        ProductStagingPathGuard.PrepareFileOutput(outputPath);

        string temporaryPath = outputPath + $".tmp-{Guid.NewGuid():N}";
        bool completed = false;

        try
        {
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteNumber("schemaVersion", CurrentSchemaVersion);
                writer.WriteStartArray("keys");

                foreach (KeyValuePair<string, byte[]> entry in publicKeys.OrderBy(entry => entry.Key, StringComparer.Ordinal))
                {
                    writer.WriteStartObject();
                    writer.WriteString("algorithm", ProductReleaseSignature.Algorithm);
                    writer.WriteBase64String("publicKey", entry.Value);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.Flush();

                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, outputPath);
            completed = true;
        }
        finally
        {
            if (!completed)
            {
                TryDelete(temporaryPath);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
