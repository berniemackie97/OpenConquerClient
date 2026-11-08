using System.Numerics;
using OpenConquer.Content.Mesh;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.World;

namespace OpenConquer.Client.World;

internal sealed class WorldRolePart : IDisposable
{
    private readonly OpenGLTexture2D _texture;
    private readonly List<WorldRoleMesh> _meshes;

    private WorldRolePart(OpenGLTexture2D texture, List<WorldRoleMesh> meshes,
        uint frameCount)
    {
        _texture = texture;
        _meshes = meshes;
        FrameCount = frameCount;
    }

    internal uint FrameCount
    {
        get;
    }

    internal static WorldRolePart Create(OpenGLGraphicsDevice device, WorldRolePartAssets assets)
    {
        List<WorldRoleMesh> meshes = new(assets.Meshes.Count);
        OpenGLTexture2D texture = device.CreateTexture2D(assets.Image.Width, assets.Image.Height, assets.Image.Pixels.Span);
        try
        {
            foreach ((C3PhyMesh mesh, C3Motion motion) in assets.Meshes)
            {
                meshes.Add(new WorldRoleMesh(device, mesh, motion));
            }

            return new WorldRolePart(texture, meshes, assets.FrameCount);
        }
        catch (Exception failure)
        {
            ClientShutdown cleanup = new(failure);
            Release(meshes, texture, cleanup);
            cleanup.ThrowIfFailed();
            throw;
        }
    }

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
        ClientShutdown cleanup = new();
        Release(_meshes, _texture, cleanup);
        cleanup.ThrowIfFailed();
    }

    private static void Release(List<WorldRoleMesh> meshes, OpenGLTexture2D texture, ClientShutdown cleanup)
    {
        foreach (WorldRoleMesh mesh in meshes)
        {
            cleanup.Run(mesh.Dispose);
        }

        cleanup.Run(texture.Dispose);
    }
}
