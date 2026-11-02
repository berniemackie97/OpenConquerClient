using System.Collections.Immutable;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio.Authoring;

public sealed record CompiledTexture(Guid Id, string Name, int Width, int Height, long Length, string Sha256);
public sealed record ContentBuild(int SchemaVersion, Guid ProjectId, ImmutableArray<CompiledTexture> Textures);

public static class StudioCompiler
{
    public const string ManifestName = "content.json";

    public static ContentBuild Compile(StudioProject project, string destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        project.ThrowIfDisposed();
        if (project.IsModified)
        {
            throw new InvalidOperationException("Save the project before compiling its content.");
        }

        destination = ProjectFiles.RequireSafePath(destination);
        string relative = Path.GetRelativePath(project.Root, destination).Replace('\\', '/');
        if (relative != ".." && !relative.StartsWith("../", StringComparison.Ordinal) &&
            !Path.IsPathRooted(relative) && !relative.StartsWith("Builds/", StringComparison.Ordinal))
        {
            throw new IOException("Builds inside a project must use the Builds directory.");
        }

        if (Directory.Exists(destination) || File.Exists(destination))
        {
            throw new IOException("The build destination already exists. Choose a new directory.");
        }

        string parent = Path.GetDirectoryName(destination) ?? throw new IOException("A build destination requires a parent directory.");
        if (!Directory.Exists(parent))
        {
            throw new DirectoryNotFoundException("Create the build destination's parent directory first.");
        }

        string stage = Path.Combine(parent, $".studio-build-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stage);
        try
        {
            List<CompiledTexture> entries = [];
            foreach (TextureAsset asset in project.Document.Assets.OrderBy(a => a.Id))
            {
                cancellationToken.ThrowIfCancellationRequested();
                TextureData texture = project.Preview(asset.Id, cancellationToken);
                string path = Path.Combine(stage, FileName(asset.Id));
                using (FileStream output = new(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    TextureCodec.Write(output, asset.Id, texture);
                    output.Flush(flushToDisk: true);
                }

                byte[] bytes = ProjectFiles.Read(path, TextureData.MaximumPixelBytes + 68);
                entries.Add(new CompiledTexture(asset.Id, asset.Name, texture.Width, texture.Height, bytes.Length, ProjectFiles.Hash(bytes)));
            }

            ContentBuild manifest = new(1, project.Document.Id, [.. entries]);
            ProjectFiles.WriteAtomic(Path.Combine(stage, ManifestName), ProjectFiles.Serialize(manifest), overwrite: false);
            Validate(stage, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ProjectFiles.RequireSafePath(destination);
            Directory.Move(stage, destination);
            return manifest;
        }
        finally
        {
            if (Directory.Exists(stage))
            {
                Directory.Delete(stage, recursive: true);
            }
        }
    }

    public static ContentBuild Validate(string directory, CancellationToken cancellationToken = default)
    {
        directory = ProjectFiles.RequireSafePath(directory);
        ContentBuild manifest = ProjectFiles.Deserialize<ContentBuild>(ProjectFiles.Read(Path.Combine(directory, ManifestName), ProjectFiles.MaximumManifestBytes));
        if (manifest.SchemaVersion != 1 || manifest.ProjectId == Guid.Empty || manifest.Textures.IsDefault || manifest.Textures.Length > ProjectDocument.MaximumAssets)
        {
            throw new InvalidDataException("The compiled content manifest has an unsupported version or invalid identity/count.");
        }

        long total = 0;
        Guid previous = Guid.Empty;
        HashSet<string> expected = new(StringComparer.Ordinal) { ManifestName };
        foreach (CompiledTexture entry in manifest.Textures)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry is null || entry.Id == Guid.Empty || entry.Id.CompareTo(previous) <= 0)
            {
                throw new InvalidDataException("Compiled texture IDs must be unique and sorted.");
            }

            previous = entry.Id;
            ProjectDocument.ValidateName(entry.Name);
            ProjectDocument.ValidateDigest(entry.Sha256);
            total += TextureData.GetByteLength(entry.Width, entry.Height);
            if (total > ProjectDocument.MaximumTotalPixelBytes)
            {
                throw new InvalidDataException("Compiled pixels exceed the project budget.");
            }

            string fileName = FileName(entry.Id);
            expected.Add(fileName);
            byte[] bytes = ProjectFiles.Read(Path.Combine(directory, fileName), TextureData.MaximumPixelBytes + 68);
            if (bytes.Length != entry.Length || ProjectFiles.Hash(bytes) != entry.Sha256)
            {
                throw new InvalidDataException($"Compiled texture '{entry.Name}' does not match its content manifest.");
            }

            using MemoryStream input = new(bytes, writable: false);
            (Guid id, TextureData texture) = TextureCodec.Read(input);
            if (id != entry.Id || texture.Width != entry.Width || texture.Height != entry.Height)
            {
                throw new InvalidDataException($"Compiled texture '{entry.Name}' has inconsistent metadata.");
            }
        }

        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            ProjectFiles.RequireSafePath(path);
            if (!expected.Remove(Path.GetFileName(path)) || Directory.Exists(path))
            {
                throw new InvalidDataException("The build contains files or directories outside its manifest.");
            }
        }

        if (expected.Count != 0)
        {
            throw new InvalidDataException("The build is missing a declared file.");
        }

        return manifest;
    }

    private static string FileName(Guid id) => id.ToString("N") + ".octex";
}
