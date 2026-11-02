using System.Collections.Immutable;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio.Authoring;

/// <summary>A single-owner editing session. Save publishes immutable pixels before replacing the project manifest.</summary>
public sealed class StudioProject : IDisposable
{
    private readonly FileStream _lease;
    private readonly Dictionary<Guid, TextureData> _textures;
    private string? _savedDigest;
    private bool _disposed;

    private StudioProject(string root, FileStream lease, ProjectDocument document, Dictionary<Guid, TextureData> textures, string? savedDigest)
    {
        Root = root;
        _lease = lease;
        Document = document;
        _textures = textures;
        _savedDigest = savedDigest;
        IsModified = savedDigest is null;
    }

    public string Root
    {
        get;
    }
    public ProjectDocument Document
    {
        get; private set;
    }
    public bool IsModified
    {
        get; private set;
    }

    public static StudioProject Create(string root, string name)
    {
        ProjectDocument document = new(ProjectDocument.CurrentVersion, Guid.NewGuid(), name, []);
        document.Validate();
        root = ProjectFiles.RequireSafePath(root);
        if (!Directory.Exists(root) || Directory.EnumerateFileSystemEntries(root).Any())
        {
            throw new IOException("Choose an existing empty directory for the new project.");
        }

        FileStream lease = AcquireLease(root);
        StudioProject project = new(root, lease, document, [], null);
        try
        {
            project.Save();
            return project;
        }
        catch
        {
            project.Dispose();
            throw;
        }
    }

    public static StudioProject Open(string root, CancellationToken cancellationToken = default)
    {
        root = ProjectFiles.RequireSafePath(root);
        FileStream lease = AcquireLease(root);
        try
        {
            byte[] bytes = ProjectFiles.Read(Path.Combine(root, ProjectFiles.ManifestName), ProjectFiles.MaximumManifestBytes);
            ProjectDocument document = ProjectFiles.Deserialize<ProjectDocument>(bytes);
            document.Validate();
            Dictionary<Guid, TextureData> textures = [];
            foreach (TextureAsset asset in document.Assets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] pixels = ProjectFiles.Read(PixelPath(root, asset.PixelDigest), TextureData.MaximumPixelBytes);
                if (ProjectFiles.Hash(pixels) != asset.PixelDigest)
                {
                    throw new InvalidDataException($"Pixel data for '{asset.Name}' failed its SHA-256 check.");
                }

                textures.Add(asset.Id, new TextureData(asset.Width, asset.Height, pixels));
            }

            return new StudioProject(root, lease, document, textures, ProjectFiles.Hash(bytes));
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private static FileStream AcquireLease(string root)
    {
        string path = ProjectFiles.RequireSafePath(Path.Combine(root, ".studio.lock"));
        return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public Guid ImportTexture(string path, string name, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        (TextureData texture, string format, string digest) = TextureImporter.Read(path, cancellationToken);
        return AddTexture(name, texture, format, digest);
    }

    public Guid ImportImage(string name, TextureData texture, string sourceKey)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(texture);
        return AddTexture(name, texture, "rgba", ProjectFiles.Hash(texture.Pixels.Span), sourceKey);
    }

    public Guid CreateTexture(string name, int width, int height, byte red, byte green, byte blue, byte alpha = 255)
    {
        ThrowIfDisposed();
        byte[] pixels = new byte[TextureData.GetByteLength(width, height)];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = red;
            pixels[i + 1] = green;
            pixels[i + 2] = blue;
            pixels[i + 3] = alpha;
        }

        return AddTexture(name, new TextureData(width, height, pixels), "created", ProjectFiles.Hash(pixels));
    }

    private Guid AddTexture(string name, TextureData texture, string format, string sourceDigest, string? sourceKey = null)
    {
        TextureAsset asset = new(Guid.NewGuid(), name, texture.Width, texture.Height, ProjectFiles.Hash(texture.Pixels.Span), format, sourceDigest, false, 0, sourceKey);
        ProjectDocument updated = Document with
        {
            Assets = [.. Document.Assets, asset]
        };
        updated.Validate();
        _textures.Add(asset.Id, texture);
        Document = updated;
        IsModified = true;
        return asset.Id;
    }

    public void EditTexture(Guid id, string name, bool flipHorizontal, int clockwiseQuarterTurns)
    {
        ThrowIfDisposed();
        TextureAsset original = FindAsset(id);
        TextureAsset updated = original with
        {
            Name = name,
            FlipHorizontal = flipHorizontal,
            ClockwiseQuarterTurns = clockwiseQuarterTurns
        };
        ProjectDocument document = Document with
        {
            Assets = Document.Assets.Select(a => a.Id == id ? updated : a).ToImmutableArray()
        };
        document.Validate();
        if (original != updated)
        {
            Document = document;
            IsModified = true;
        }
    }

    public void ReplaceTexture(Guid id, string path, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        TextureAsset original = FindAsset(id);
        (TextureData texture, string format, string digest) = TextureImporter.Read(path, cancellationToken);
        TextureAsset updated = original with
        {
            Width = texture.Width,
            Height = texture.Height,
            PixelDigest = ProjectFiles.Hash(texture.Pixels.Span),
            SourceFormat = format,
            SourceDigest = digest,
        };
        ProjectDocument document = Document with
        {
            Assets = Document.Assets.Select(a => a.Id == id ? updated : a).ToImmutableArray()
        };
        document.Validate();
        _textures[id] = texture;
        Document = document;
        IsModified = true;
    }

    public TextureData Preview(Guid id, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        TextureAsset asset = FindAsset(id);
        return _textures[id].Transform(asset.FlipHorizontal, asset.ClockwiseQuarterTurns, cancellationToken);
    }

    public void Rename(string name)
    {
        ThrowIfDisposed();
        ProjectDocument updated = Document with
        {
            Name = name
        };
        updated.Validate();
        IsModified |= name != Document.Name;
        Document = updated;
    }

    public void Save(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Document.Validate();
        string manifest = Path.Combine(Root, ProjectFiles.ManifestName);
        if (_savedDigest is not null && ProjectFiles.Hash(ProjectFiles.Read(manifest, ProjectFiles.MaximumManifestBytes)) != _savedDigest)
        {
            throw new IOException("The project manifest changed outside this session. Reopen it before saving.");
        }

        string inputs = ProjectFiles.RequireSafePath(Path.Combine(Root, "Inputs"));
        Directory.CreateDirectory(inputs);
        foreach (TextureAsset asset in Document.Assets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = PixelPath(Root, asset.PixelDigest);
            if (File.Exists(path))
            {
                if (ProjectFiles.Hash(ProjectFiles.Read(path, TextureData.MaximumPixelBytes)) != asset.PixelDigest)
                {
                    throw new InvalidDataException($"Existing pixels for '{asset.Name}' are corrupted; save was stopped.");
                }
            }
            else
            {
                ProjectFiles.WriteAtomic(path, _textures[asset.Id].Pixels.Span, overwrite: false);
            }
        }

        byte[] bytes = ProjectFiles.Serialize(Document with
        {
            Assets = Document.Assets.OrderBy(a => a.Id).ToImmutableArray()
        });
        cancellationToken.ThrowIfCancellationRequested();
        ProjectFiles.WriteAtomic(manifest, bytes, overwrite: _savedDigest is not null);
        _savedDigest = ProjectFiles.Hash(bytes);
        IsModified = false;
    }

    internal TextureAsset FindAsset(Guid id) => Document.Assets.FirstOrDefault(a => a.Id == id) ?? throw new InvalidDataException($"Asset '{id}' does not exist.");

    private static string PixelPath(string root, string digest) => Path.Combine(root, "Inputs", digest + ".rgba");

    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (!_disposed)
        {
            _lease.Dispose();
            _textures.Clear();
            _disposed = true;
        }
    }
}
