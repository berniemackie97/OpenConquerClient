using System.Numerics;
using OpenConquer.Content;
using OpenConquer.Content.Images;
using OpenConquer.Content.Mesh;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;

namespace OpenConquer.Client.World;

internal sealed class WorldRolePart : IDisposable
{
    private readonly OpenGLTexture2D _texture;
    private readonly List<WorldRoleMesh> _meshes;
    private readonly Dictionary<string, C3Motion> _attachments;

    private WorldRolePart(OpenGLTexture2D texture, List<WorldRoleMesh> meshes,
        Dictionary<string, C3Motion> attachments, uint frameCount)
    {
        _texture = texture;
        _meshes = meshes;
        _attachments = attachments;
        FrameCount = frameCount;
    }

    internal uint FrameCount { get; }

    internal static WorldRolePart Load(IClientContentSource source, OpenGLGraphicsDevice device,
        C3RoleCatalog catalog, C3ResolvedPart part, string? actionMotionPath = null)
    {
        List<C3MeshSection> geometry = [];
        List<C3Motion> motions = [];
        ReadSections(C3MeshFile.Load(source, part.MeshPath), geometry, motions);
        if (geometry.Count == 0 || geometry.Count != motions.Count)
        {
            throw new InvalidDataException("The character part contains mismatched geometry and motion sections.");
        }

        if (actionMotionPath is not null)
        {
            List<C3Motion> actionMotions = [];
            ReadSections(C3MeshFile.Load(source, actionMotionPath), [], actionMotions);
            if (actionMotions.Count < geometry.Count)
            {
                throw new InvalidDataException("The character action has fewer tracks than its geometry.");
            }

            motions = actionMotions;
        }

        RgbaImage image = DdsImageLoader.Load(source, part.TexturePath, ContentLookupMode.PackageOnly);
        OpenGLTexture2D? texture = null;
        List<WorldRoleMesh> meshes = [];
        Dictionary<string, C3Motion> attachments = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            texture = device.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
            for (int i = 0; i < geometry.Count; i++)
            {
                C3PhyMesh phy = C3PhyMeshReader.Read(geometry[i]);
                C3Motion motion = motions[i];
                if (catalog.IsAttachmentMesh(phy.Name))
                {
                    if (!attachments.TryAdd(phy.Name, motion))
                    {
                        throw new InvalidDataException("The character part contains duplicate attachment submeshes.");
                    }
                }
                else
                {
                    meshes.Add(new WorldRoleMesh(device, phy, motion));
                }
            }

            if (meshes.Count == 0)
            {
                throw new InvalidDataException("The character part contains no drawable geometry.");
            }

            return new WorldRolePart(texture, meshes, attachments,
                actionMotionPath is null ? 0 : motions[0].FrameCount);
        }
        catch
        {
            foreach (WorldRoleMesh mesh in meshes)
            {
                mesh.Dispose();
            }

            texture?.Dispose();
            throw;
        }
    }

    private static void ReadSections(C3MeshFile file, List<C3MeshSection> geometry, List<C3Motion> motions)
    {
        foreach (C3MeshSection section in file.Sections)
        {
            if ((section.Identifier & 0x00FFFFFF) == 0x00594850)
            {
                geometry.Add(section);
            }
            else if (section.Identifier == 0x49544F4D)
            {
                if (motions.Count == 16)
                {
                    throw new InvalidDataException("The character part exceeds its motion-track limit.");
                }

                motions.Add(C3MotionReader.Read(section));
            }
        }
    }

    internal bool TryGetAttachment(string name, out C3Motion motion) =>
        _attachments.TryGetValue(name, out motion!);

    internal void Draw(OpenGLRenderer renderer, Matrix4x4 matrix, int frame)
    {
        foreach (WorldRoleMesh mesh in _meshes)
        {
            mesh.Draw(renderer, _texture, matrix, frame);
        }
    }

    internal void DrawShadow(OpenGLRenderer renderer, Matrix4x4 matrix)
    {
        foreach (WorldRoleMesh mesh in _meshes)
        {
            mesh.DrawShadow(renderer, matrix);
        }
    }

    public void Dispose()
    {
        foreach (WorldRoleMesh mesh in _meshes)
        {
            mesh.Dispose();
        }

        _texture.Dispose();
    }
}
