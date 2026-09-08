using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace OpenConquer.Product.Tool;

internal static class ProductToolCommandLine
{
    public const string Usage =
        "Commands:\n" +
        "  create-release-manifest --client-publish <path> --target-runtime <rid> --release-version <value> --release-sequence <positive integer> --minimum-launcher-version <positive integer> --output <path>\n" +
        "  create-release-signature --release-manifest <path> --public-key <path> --signature <path> --output <path>\n" +
        "  create-release-trust --public-key <path> [--public-key <path> ...] --output <path>\n" +
        "  stage-managed-product --launcher-publish <path> --client-publish <path> --release-manifest <path> --release-signature <path> --output <path>";

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
        if (command is not ("create-release-manifest" or "create-release-signature" or "create-release-trust" or "stage-managed-product"))
        {
            errorMessage = $"Unknown command '{command}'.";
            return false;
        }
        if (command == "create-release-trust")
        {
            return TryParseTrust(args, workingDirectoryPath, out options, out errorMessage);
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
            "stage-managed-product" => TryParseStage(values, workingDirectoryPath, out options, out errorMessage),
            _ => throw new InvalidOperationException("Unsupported product-tool command."),
        };
    }

    private static bool TryParseManifest(Dictionary<string, string> values, string workingDirectory,
        out ProductToolOptions? options, out string? error)
    {
        string[] required = ["--client-publish", "--target-runtime", "--release-version", "--release-sequence", "--minimum-launcher-version", "--output"];
        if (!HasExactly(values, required, out error) || !TryPath(values["--client-publish"], workingDirectory, out string? client)
                                                     || !TryPath(values["--output"], workingDirectory, out string? output)
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
        if (!HasExactly(values, required, out error) || !TryPath(values["--release-manifest"], workingDirectory, out string? manifest)
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
        string[] required = ["--launcher-publish", "--client-publish", "--release-manifest", "--release-signature", "--output"];
        if (!HasExactly(values, required, out error) || !TryPath(values["--launcher-publish"], workingDirectory, out string? launcher)
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
            error = unexpected is not null ? $"Option '{unexpected}' is not valid for this command." : $"Option '{missing}' is required.";
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
            string candidate = Path.IsPathFullyQualified(path) ? path : Path.Combine(Path.GetFullPath(workingDirectory), path);
            normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
            return !string.IsNullOrWhiteSpace(normalized);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
