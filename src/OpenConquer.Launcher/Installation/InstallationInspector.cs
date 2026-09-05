using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;

namespace OpenConquer.Launcher.Installation;

/// <summary>Inspects the current unpacked .NET game layout without loading or executing its code.</summary>
internal sealed class InstallationInspector : IInstallationInspector
{
    private const string AssemblyName = "OpenConquer.Client";
    private const int MaximumAssemblyLength = 16 * 1024 * 1024;
    private const int MaximumJsonLength = 1024 * 1024;

    public Task<InstallationInspection> InspectAsync(InstallationRoot root, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        // Attribute probes and PE/JSON parsing are synchronous. Keep them off the UI thread too.
        return Task.Run(() => InspectCoreAsync(root, cancellationToken), cancellationToken);
    }

    private static async Task<InstallationInspection> InspectCoreAsync(InstallationRoot root, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CheckKind(root.Path, directory: true))
            {
                return Reject(InstallationIssue.InvalidLayout);
            }

            byte[] assembly = await ReadFileAsync(root, AssemblyName + ".dll", MaximumAssemblyLength, cancellationToken).ConfigureAwait(false);
            Version? version = ReadAssemblyVersion(assembly);
            if (version is null)
            {
                return Reject(InstallationIssue.InvalidLayout);
            }

            using (JsonDocument runtime = await ReadJsonAsync(root, AssemblyName + ".runtimeconfig.json", cancellationToken).ConfigureAwait(false))
            {
                if (!runtime.RootElement.TryGetProperty("runtimeOptions", out JsonElement options) || options.ValueKind != JsonValueKind.Object ||
                    !options.TryGetProperty("tfm", out JsonElement tfm) || tfm.ValueKind != JsonValueKind.String || tfm.GetString() != "net10.0")
                {
                    return Reject(InstallationIssue.UnsupportedLayout);
                }
            }

            using (JsonDocument dependencies = await ReadJsonAsync(root, AssemblyName + ".deps.json", cancellationToken).ConfigureAwait(false))
            {
                if (!HasGameRuntimeAsset(dependencies.RootElement))
                {
                    return Reject(InstallationIssue.InvalidLayout);
                }
            }

            string content = Path.Combine(root.Path, "content");
            string contentSet = Path.Combine(content, "retail-5517");
            if (!CheckKind(content, directory: true) || !CheckKind(contentSet, directory: true) || !CheckKind(Path.Combine(contentSet, "payload"), directory: true) || !CheckKind(Path.Combine(contentSet, "manifest.json"), directory: false))
            {
                return Reject(InstallationIssue.InvalidLayout);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new InstallationInspection.Located(version);
        }
        catch (LinkedInstallationPathException)
        {
            return Reject(InstallationIssue.LinkedPath);
        }
        catch (FileNotFoundException)
        {
            return Reject(InstallationIssue.MissingFiles);
        }
        catch (DirectoryNotFoundException)
        {
            return Reject(InstallationIssue.MissingFiles);
        }
        catch (UnauthorizedAccessException)
        {
            return Reject(InstallationIssue.AccessDenied);
        }
        catch (BadImageFormatException)
        {
            return Reject(InstallationIssue.InvalidLayout);
        }
        catch (JsonException)
        {
            return Reject(InstallationIssue.InvalidLayout);
        }
        catch (InvalidDataException)
        {
            return Reject(InstallationIssue.InvalidLayout);
        }
        catch (IOException)
        {
            return Reject(InstallationIssue.ReadFailure);
        }
    }

    private static Version? ReadAssemblyVersion(byte[] image)
    {
        using MemoryStream stream = new(image, writable: false);
        using PEReader reader = new(stream, PEStreamOptions.LeaveOpen);
        if (!reader.HasMetadata || reader.PEHeaders.CorHeader is not { EntryPointTokenOrRelativeVirtualAddress: not 0 })
        {
            return null;
        }

        MetadataReader metadata = reader.GetMetadataReader();
        if (!metadata.IsAssembly)
        {
            return null;
        }

        AssemblyDefinition definition = metadata.GetAssemblyDefinition();
        return metadata.GetString(definition.Name) == AssemblyName ? definition.Version : null;
    }

    private static async Task<JsonDocument> ReadJsonAsync(InstallationRoot root, string name, CancellationToken cancellationToken)
    {
        byte[] bytes = await ReadFileAsync(root, name, MaximumJsonLength, cancellationToken).ConfigureAwait(false);
        JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        try
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Installation metadata must be a JSON object.");
            }

            RejectDuplicateProperties(document.RootElement);
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException("Installation metadata has duplicate properties.");
                }

                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in element.EnumerateArray())
            {
                RejectDuplicateProperties(child);
            }
        }
    }

    private static bool HasGameRuntimeAsset(JsonElement root)
    {
        if (!root.TryGetProperty("runtimeTarget", out JsonElement runtimeTarget) || runtimeTarget.ValueKind != JsonValueKind.Object ||
            !runtimeTarget.TryGetProperty("name", out JsonElement targetName) || targetName.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("targets", out JsonElement targets) || targets.ValueKind != JsonValueKind.Object ||
            !targets.TryGetProperty(targetName.GetString()!, out JsonElement target) || target.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        int matches = 0;
        foreach (JsonProperty library in target.EnumerateObject())
        {
            if (library.Name.StartsWith(AssemblyName + "/", StringComparison.Ordinal) && library.Value.ValueKind == JsonValueKind.Object &&
                library.Value.TryGetProperty("runtime", out JsonElement runtime) && runtime.ValueKind == JsonValueKind.Object &&
                runtime.TryGetProperty(AssemblyName + ".dll", out JsonElement asset) && asset.ValueKind == JsonValueKind.Object)
            {
                matches++;
            }
        }

        return matches == 1;
    }

    private static async Task<byte[]> ReadFileAsync(InstallationRoot root, string name, int maximumLength, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = Path.Combine(root.Path, name);
        if (!CheckKind(path, directory: false))
        {
            throw new InvalidDataException("An installation file has an unexpected filesystem type.");
        }

        long length = new FileInfo(path).Length;
        if (length is <= 0 || length > maximumLength)
        {
            throw new InvalidDataException("An installation file exceeds its inspection limits.");
        }

        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (!stream.CanSeek || stream.Length != length)
        {
            throw new IOException("An installation file changed before it could be read.");
        }

        byte[] bytes = new byte[(int)length];
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        if (stream.Length != length)
        {
            throw new IOException("An installation file changed during inspection.");
        }

        return bytes;
    }

    private static bool CheckKind(string path, bool directory)
    {
        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new LinkedInstallationPathException();
        }

        return (attributes & FileAttributes.Device) == 0 && ((attributes & FileAttributes.Directory) != 0) == directory;
    }

    private static InstallationInspection.Rejected Reject(InstallationIssue issue) => new(issue);

    private sealed class LinkedInstallationPathException : IOException;
}
