using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace OpenConquer.Product.Tool;

/// <summary>Publisher-only realm-directory command; independent of release signing.</summary>
internal static class RealmCatalogTool
{
    private const string Command = "create-realm-catalog";
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private static readonly string[] s_options = ["--definition", "--private-key", "--sequence", "--issued-utc", "--expires-utc", "--catalog-uri", "--signature-uri", "--output"];

    public static bool IsCommand(string command) => command == Command;

    public static int Run(IReadOnlyList<string> args)
    {
        if (!TryParse(args, Environment.CurrentDirectory, out RealmCatalogPublishOptions? options, out string? error))
        {
            Console.Error.WriteLine($"OpenConquer.Product.Tool: {error}");
            Console.Error.WriteLine("Usage: create-realm-catalog --definition <path> --private-key <PKCS8 DER path> --sequence <positive integer> --issued-utc <yyyy-MM-ddTHH:mm:ssZ> --expires-utc <yyyy-MM-ddTHH:mm:ssZ> --catalog-uri <https URI> --signature-uri <https URI> --output <new directory>");
            return 2;
        }

        try
        {
            RealmCatalogPublisher.Create(options, DateTimeOffset.UtcNow);
            Console.WriteLine($"Created signed realm catalog and public client authority in '{options.OutputPath}'.");
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidDataException or JsonException or UnauthorizedAccessException or InvalidOperationException or System.Security.Cryptography.CryptographicException)
        {
            Console.Error.WriteLine($"OpenConquer.Product.Tool: {exception.Message}");
            return 1;
        }
    }

    internal static bool TryParse(IReadOnlyList<string> args, string workingDirectory, [NotNullWhen(true)] out RealmCatalogPublishOptions? options, [NotNullWhen(false)] out string? error)
    {
        options = null;
        error = null;
        if (args.Count != 1 + s_options.Length * 2 || args[0] != Command)
        {
            error = "Realm catalog command requires exactly eight named options.";
            return false;
        }

        Dictionary<string, string> values = new(StringComparer.Ordinal);
        for (int i = 1; i < args.Count; i += 2)
        {
            string name = args[i];
            string value = args[i + 1];
            if (!s_options.Contains(name, StringComparer.Ordinal) || !values.TryAdd(name, value) || string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Invalid, repeated or missing value for '{name}'.";
                return false;
            }
        }

        if (!long.TryParse(values["--sequence"], NumberStyles.None, CultureInfo.InvariantCulture, out long sequence) || sequence <= 0 ||
            !DateTimeOffset.TryParseExact(values["--issued-utc"], TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset issued) ||
            !DateTimeOffset.TryParseExact(values["--expires-utc"], TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset expires) ||
            !Uri.TryCreate(values["--catalog-uri"], UriKind.Absolute, out Uri? catalogUri) ||
            !Uri.TryCreate(values["--signature-uri"], UriKind.Absolute, out Uri? signatureUri))
        {
            error = "Realm sequence, UTC timestamps, or publication URIs are invalid.";
            return false;
        }

        try
        {
            options = new RealmCatalogPublishOptions(
                FullPath(workingDirectory, values["--definition"]), FullPath(workingDirectory, values["--private-key"]),
                sequence, issued, expires, catalogUri, signatureUri, FullPath(workingDirectory, values["--output"]));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException)
        {
            error = $"A realm catalog path is invalid: {exception.Message}";
            return false;
        }
    }

    private static string FullPath(string workingDirectory, string value) =>
        ProductStagingPathGuard.NormalizePath(Path.GetFullPath(value, workingDirectory), nameof(value));
}

internal sealed record RealmCatalogPublishOptions(string DefinitionPath, string PrivateKeyPath, long Sequence, DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt, Uri CatalogUri, Uri SignatureUri, string OutputPath);
