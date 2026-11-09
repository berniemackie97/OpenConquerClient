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
    internal WorldRoleActionTracks Actions
    {
        get;
    }

    private WorldRolePart(OpenGLTexture2D texture, List<WorldRoleMesh> meshes, WorldRoleActionTracks actions)
    {
        _texture = texture;
        _meshes = meshes;
        Actions = actions;
    }

    internal uint FrameCount => Actions.IdleFrameCount;

    internal static WorldRolePart Create(OpenGLGraphicsDevice device, WorldRolePartAssets assets)
    {
        List<WorldRoleMesh> meshes = new(assets.Meshes.Count);
        OpenGLTexture2D texture = device.CreateTexture2D(assets.Image.Width, assets.Image.Height, assets.Image.Pixels.Span);
        try
        {
            for (int i = 0; i < assets.Meshes.Count; i++)
            {
                (C3PhyMesh mesh, C3Motion motion) = assets.Meshes[i];
                int maximumBones = assets.Actions.MaximumBoneCount(assets.Actions.DrawableTrackIndices[i], motion);
                meshes.Add(new WorldRoleMesh(device, mesh, motion, maximumBones));
            }

            return new WorldRolePart(texture, meshes, assets.Actions);
        }
        catch (Exception failure)
        {
            ClientShutdown cleanup = new(failure);
            Release(meshes, texture, cleanup);
            cleanup.ThrowIfFailed();
            throw;
        }
    }

    internal void Draw(OpenGLRenderer renderer, Matrix4x4 matrix, int frame, ushort action = 0)
    {
        for (int i = 0; i < _meshes.Count; i++)
        {
            C3Motion? motion = Actions.TryGetMotion(action, Actions.DrawableTrackIndices[i],
                out C3Motion? selected) ? selected : null;
            _meshes[i].Draw(renderer, _texture, matrix, frame, motion);
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
