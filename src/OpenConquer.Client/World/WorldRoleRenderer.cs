using System.Numerics;
using OpenConquer.Content;
using OpenConquer.Content.Images;
using OpenConquer.Content.Mesh;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;

namespace OpenConquer.Client.World;

internal sealed class WorldRoleRenderer : IDisposable
{
    private readonly OpenGLTexture2D _texture;
    private readonly List<WorldRoleMesh> _meshes;

    private WorldRoleRenderer(OpenGLTexture2D texture, List<WorldRoleMesh> meshes)
    {
        _texture = texture;
        _meshes = meshes;
    }

    internal static WorldRoleRenderer? Load(IClientContentSource source,
        OpenGLGraphicsDevice device, uint packedAppearance)
    {
        if (!source.TryOpenRead("ini/c3.wdb", ContentLookupMode.LooseOnly, out Stream? probe))
        {
            return null;
        }

        probe.Dispose();
        C3RoleCatalog catalog = C3RoleCatalog.Load(source);
        uint bodyAppearance = packedAppearance % 1000;
        uint modelId = checked(bodyAppearance * 1_000_000);
        if (!catalog.TryResolve("body", modelId, out IReadOnlyList<C3ResolvedPart> parts))
        {
            throw new InvalidDataException("The character's body model is absent from the 3D resource catalog.");
        }

        if (parts.Count != 1)
        {
            throw new InvalidDataException("The initial character body requires one resolved mesh resource.");
        }

        C3ResolvedPart part = parts[0];
        C3MeshFile file = C3MeshFile.Load(source, part.MeshPath);
        List<C3MeshSection> geometry = [];
        List<C3MeshSection> motions = [];
        foreach (C3MeshSection section in file.Sections)
        {
            if ((section.Identifier & 0x00FFFFFF) == 0x00594850)
            {
                geometry.Add(section);
            }
            else if (section.Identifier == 0x49544F4D)
            {
                motions.Add(section);
            }
        }

        if (geometry.Count == 0 || motions.Count != geometry.Count)
        {
            throw new InvalidDataException("The character's model contains mismatched geometry and motion sections.");
        }

        RgbaImage image = DdsImageLoader.Load(source, part.TexturePath, ContentLookupMode.PackageOnly);
        OpenGLTexture2D? texture = null;
        List<WorldRoleMesh> meshes = [];
        try
        {
            texture = device.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
            for (int i = 0; i < geometry.Count; i++)
            {
                C3PhyMesh phy = C3PhyMeshReader.Read(geometry[i]);
                C3Motion motion = C3MotionReader.Read(motions[i]);
                if (!catalog.IsAttachmentMesh(phy.Name))
                {
                    meshes.Add(new WorldRoleMesh(device, phy, motion));
                }
            }

            if (meshes.Count == 0)
            {
                throw new InvalidDataException("The character's body model contains no drawable geometry.");
            }

            return new WorldRoleRenderer(texture, meshes);
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

    internal void Draw(OpenGLRenderer renderer, Matrix4x4 main, Matrix4x4 shadow)
    {
        foreach (WorldRoleMesh mesh in _meshes)
        {
            mesh.Draw(renderer, _texture, main, frame: 0);
        }

        foreach (WorldRoleMesh mesh in _meshes)
        {
            mesh.DrawShadow(renderer, shadow);
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
