using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace OpenConquer.Product.Tool;

internal static class ProductToolCommandLine
{
    public const string Usage =
        "Commands:\n"
        + "  create-release-manifest --client-publish <path> --target-runtime <rid> --release-version <value> --release-sequence <positive integer> --minimum-launcher-version <positive integer> --output <path>\n"
        + "  create-release-signature --release-manifest <path> --public-key <path> --signature <path> --output <path>\n"
        + "  create-release-package --client-publish <path> --release-manifest <path> --release-signature <path> --public-key <path> --output <path>\n"
        + "  create-release-catalog --release-package <path> [--release-package <path> ...] --package-base-uri <https URI ending in /> --public-key <path> [--public-key <path> ...] --expires-utc <yyyy-MM-ddTHH:mm:ssZ> --output <path>\n"
        + "  create-release-catalog-signature --release-catalog <path> --public-key <path> --signature <path> --output <path>\n"
        + "  create-release-trust --public-key <path> [--public-key <path> ...] --output <path>\n"
        + "  create-local-product\n"
        + "  stage-managed-product --launcher-publish <path> --client-publish <path> --release-manifest <path> --release-signature <path> --output <path>";

    public static bool TryParse(IReadOnlyList<string> args, string workingDirectoryPath, [NotNullWhen(true)] out ProductToolOptions? options, [NotNullWhen(false)] out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectoryPath);
        options = null;

        if (args.Count == 0)
        {
            errorMessage = "A command is required.";
            return false;
        }

        string command = args[0];

        if (command is not ("create-release-manifest" or "create-release-signature" or
                "create-release-package" or "create-release-catalog" or
                "create-release-catalog-signature" or "create-release-trust" or
                "create-local-product" or "stage-managed-product"))
        {
            errorMessage = $"Unknown command '{command}'.";
            return false;
        }

        if (command == "create-local-product")
        {
            if (args.Count != 1)
            {
                errorMessage = "Command 'create-local-product' does not accept options.";
                return false;
            }

            options = new LocalProductOptions(Path.GetFullPath(workingDirectoryPath));
            errorMessage = null;
            return true;
        }

        if (command == "create-release-trust")
        {
            return TryParseTrust(args, workingDirectoryPath, out options, out errorMessage);
        }

        if (command == "create-release-catalog")
        {
            return TryParseCatalog(args, workingDirectoryPath, out options,
                out errorMessage);
        }

        Dictionary<string, string> values = new(StringComparer.Ordinal);

        for (int index = 1; index < args.Count; index++)
        {
            string name = args[index];

            if (!name.StartsWith("--", StringComparison.Ordinal) || !values.TryAdd(name, string.Empty))
            {
                errorMessage = $"Unknown or duplicate option '{name}'.";
                return false;
            }

            if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]) || args[index].StartsWith("--", StringComparison.Ordinal))
            {
                errorMessage = $"Option '{name}' requires a value.";
                return false;
            }

            values[name] = args[index];
        }

        return command switch
        {
            "create-release-manifest" => TryParseManifest(values, workingDirectoryPath, out options, out errorMessage),
            "create-release-signature" => TryParseSignature(values, workingDirectoryPath, out options, out errorMessage),
            "create-release-package" => TryParsePackage(values, workingDirectoryPath,
                out options, out errorMessage),
            "create-release-catalog-signature" => TryParseCatalogSignature(values,
                workingDirectoryPath, out options, out errorMessage),
            "stage-managed-product" => TryParseStage(values, workingDirectoryPath, out options, out errorMessage),
            _ => throw new InvalidOperationException("Unsupported product-tool command."),
        };
    }

    private static bool TryParseManifest(Dictionary<string, string> values, string workingDirectory, out ProductToolOptions? options, out string? error)
    {
        string[] required =
        [
            "--client-publish",
            "--target-runtime",
            "--release-version",
            "--release-sequence",
            "--minimum-launcher-version",
            "--output",
        ];

        if (!HasExactly(values, required, out error) || !TryPath(values["--client-publish"], workingDirectory, out string? client) || !TryPath(values["--output"], workingDirectory, out string? output)
            || !ulong.TryParse(values["--release-sequence"], NumberStyles.None, CultureInfo.InvariantCulture, out ulong sequence)
            || sequence == 0 || !int.TryParse(values["--minimum-launcher-version"], NumberStyles.None, CultureInfo.InvariantCulture, out int minimumLauncherVersion)
            || minimumLauncherVersion <= 0)
        {
            options = null;
            error ??= "Release paths, sequence, or launcher compatibility are invalid.";
            return false;
        }

        options = new ReleaseManifestOptions(client, values["--target-runtime"], values["--release-version"], sequence, minimumLauncherVersion, output);
        error = null;
        return true;
    }

    private static bool TryParseSignature(Dictionary<string, string> values, string workingDirectory, out ProductToolOptions? options, out string? error)
    {
        string[] required = ["--release-manifest", "--public-key", "--signature", "--output"];

        if (!HasExactly(values, required, out error)
            || !TryPath(values["--release-manifest"], workingDirectory, out string? manifest)
            || !TryPath(values["--public-key"], workingDirectory, out string? publicKey)
            || !TryPath(values["--signature"], workingDirectory, out string? signature)
            || !TryPath(values["--output"], workingDirectory, out string? output))
        {
            options = null;
            error ??= "Release signature paths are invalid.";
            return false;
        }

        options = new ReleaseSignatureOptions(manifest, publicKey, signature, output);
        error = null;
        return true;
    }

    private static bool TryParsePackage(
        Dictionary<string, string> values,
        string workingDirectory,
        out ProductToolOptions? options,
        out string? error)
    {
        string[] required =
        [
            "--client-publish",
            "--release-manifest",
            "--release-signature",
            "--public-key",
            "--output",
        ];

        if (!HasExactly(values, required, out error) ||
            !TryPath(values["--client-publish"], workingDirectory, out string? client) ||
            !TryPath(values["--release-manifest"], workingDirectory, out string? manifest) ||
            !TryPath(values["--release-signature"], workingDirectory,
                out string? signature) ||
            !TryPath(values["--public-key"], workingDirectory, out string? publicKey) ||
            !TryPath(values["--output"], workingDirectory, out string? output))
        {
            options = null;
            error ??= "Release package paths are invalid.";
            return false;
        }

        options = new ReleasePackageOptions(client, manifest, signature, publicKey, output);
        error = null;
        return true;
    }

    private static bool TryParseCatalogSignature(
        Dictionary<string, string> values,
        string workingDirectory,
        out ProductToolOptions? options,
        out string? error)
    {
        string[] required =
            ["--release-catalog", "--public-key", "--signature", "--output"];
        if (!HasExactly(values, required, out error) ||
            !TryPath(values["--release-catalog"], workingDirectory,
                out string? catalog) ||
            !TryPath(values["--public-key"], workingDirectory, out string? publicKey) ||
            !TryPath(values["--signature"], workingDirectory, out string? signature) ||
            !TryPath(values["--output"], workingDirectory, out string? output))
        {
            options = null;
            error ??= "Release catalog signature paths are invalid.";
            return false;
        }

        options = new CatalogSignatureOptions(catalog, publicKey, signature, output);
        error = null;
        return true;
    }

    private static bool TryParseCatalog(
        IReadOnlyList<string> args,
        string workingDirectory,
        out ProductToolOptions? options,
        out string? error)
    {
        List<string> packages = [];
        List<string> publicKeys = [];
        Uri? packageBaseUri = null;
        DateTimeOffset? expiresAt = null;
        string? output = null;
        for (int index = 1; index < args.Count; index++)
        {
            string name = args[index];
            if (name is not ("--release-package" or "--package-base-uri" or "--public-key" or
                    "--expires-utc" or "--output"))
            {
                options = null;
                error = $"Option '{name}' is not valid for this command.";
                return false;
            }

            if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]) ||
                args[index].StartsWith("--", StringComparison.Ordinal))
            {
                options = null;
                error = $"Option '{name}' requires a value.";
                return false;
            }

            if (name == "--release-package")
            {
                if (packages.Count == ProductReleaseCatalog.MaximumReleaseCount ||
                    !TryPath(args[index], workingDirectory, out string? package))
                {
                    options = null;
                    error = "The release package list is invalid or excessive.";
                    return false;
                }

                packages.Add(package);
            }
            else if (name == "--public-key")
            {
                if (publicKeys.Count == ProductReleaseTrust.MaximumKeyCount ||
                    !TryPath(args[index], workingDirectory, out string? publicKey))
                {
                    options = null;
                    error = "The release public-key list is invalid or excessive.";
                    return false;
                }

                publicKeys.Add(publicKey);
            }
            else if (name == "--package-base-uri")
            {
                if (packageBaseUri is not null ||
                    !ProductReleaseUri.TryParseBase(args[index], out packageBaseUri))
                {
                    options = null;
                    error = "The package base URI is invalid or repeated.";
                    return false;
                }
            }
            else if (name == "--expires-utc")
            {
                if (expiresAt is not null || !DateTimeOffset.TryParseExact(args[index],
                        ProductReleaseCatalog.TimestampFormat,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out DateTimeOffset parsedExpiration))
                {
                    options = null;
                    error = "The catalog expiration is invalid or repeated.";
                    return false;
                }

                expiresAt = parsedExpiration;
            }
            else if (output is not null ||
                !TryPath(args[index], workingDirectory, out output))
            {
                options = null;
                error = "The release catalog output path is invalid or repeated.";
                return false;
            }
        }

        if (packages.Count == 0 || publicKeys.Count == 0 || packageBaseUri is null ||
            expiresAt is null || output is null)
        {
            options = null;
            error = "Release packages, publisher keys, package base URI, expiration, and output are required.";
            return false;
        }

        options = new ReleaseCatalogOptions(packages, packageBaseUri, publicKeys,
            expiresAt.Value, output);
        error = null;
        return true;
    }

    private static bool TryParseTrust(IReadOnlyList<string> args, string workingDirectory, out ProductToolOptions? options, out string? error)
    {
        List<string> publicKeys = [];
        string? output = null;

        for (int index = 1; index < args.Count; index++)
        {
            string name = args[index];

            if (name is not ("--public-key" or "--output"))
            {
                options = null;
                error = $"Option '{name}' is not valid for this command.";
                return false;
            }

            if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]) || args[index].StartsWith("--", StringComparison.Ordinal))
            {
                options = null;
                error = $"Option '{name}' requires a value.";
                return false;
            }

            if (name == "--public-key")
            {
                if (publicKeys.Count == ProductReleaseTrust.MaximumKeyCount)
                {
                    options = null;
                    error = $"Release trust supports at most {ProductReleaseTrust.MaximumKeyCount} public keys.";
                    return false;
                }

                if (!TryPath(args[index], workingDirectory, out string? publicKey))
                {
                    options = null;
                    error = "A release public-key path is invalid.";
                    return false;
                }

                publicKeys.Add(publicKey);
                continue;
            }

            if (output is not null)
            {
                options = null;
                error = "Option '--output' must not be repeated.";
                return false;
            }

            if (!TryPath(args[index], workingDirectory, out output))
            {
                options = null;
                error = "The release-trust output path is invalid.";
                return false;
            }
        }

        if (publicKeys.Count == 0)
        {
            options = null;
            error = "Option '--public-key' is required.";
            return false;
        }

        if (output is null)
        {
            options = null;
            error = "Option '--output' is required.";
            return false;
        }

        options = new ReleaseTrustOptions(publicKeys, output);
        error = null;
        return true;
    }

    private static bool TryParseStage(Dictionary<string, string> values, string workingDirectory, out ProductToolOptions? options, out string? error)
    {
        string[] required =
        [
            "--launcher-publish",
            "--client-publish",
            "--release-manifest",
            "--release-signature",
            "--output",
        ];

        if (!HasExactly(values, required, out error)
            || !TryPath(values["--launcher-publish"], workingDirectory, out string? launcher)
            || !TryPath(values["--client-publish"], workingDirectory, out string? client)
            || !TryPath(values["--release-manifest"], workingDirectory, out string? manifest)
            || !TryPath(values["--release-signature"], workingDirectory, out string? signature)
            || !TryPath(values["--output"], workingDirectory, out string? output))
        {
            options = null;
            error ??= "Product staging paths are invalid.";
            return false;
        }

        options = new ProductStageOptions(launcher, client, manifest, signature, output);
        error = null;
        return true;
    }

    private static bool HasExactly(Dictionary<string, string> values, string[] required, out string? error)
    {
        HashSet<string> expected = new(required, StringComparer.Ordinal);
        string? unexpected = values.Keys.FirstOrDefault(option => !expected.Contains(option));
        string? missing = required.FirstOrDefault(option => !values.ContainsKey(option));

        if (unexpected is not null || missing is not null || values.Count != required.Length)
        {
            error = unexpected is not null
                ? $"Option '{unexpected}' is not valid for this command."
                : $"Option '{missing}' is required.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryPath(string path, string workingDirectory, [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;

        try
        {
            string candidate = Path.IsPathFullyQualified(path)
                ? path
                : Path.Combine(Path.GetFullPath(workingDirectory), path);
            normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
            return !string.IsNullOrWhiteSpace(normalized);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
