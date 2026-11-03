using System.Numerics;
using System.Runtime.InteropServices;
using OpenConquer.Content.Mesh;
using OpenConquer.Platform.Geometry;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Meshes;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class IndexedMeshConformance
{
    public static void Run(OpenGLGraphicsDevice graphicsDevice, PixelSize framebufferSize)
    {
        const int size = 9;
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(
            new LogicalRenderSize(size, size), framebufferSize.Width, framebufferSize.Height,
            colorFormat: RenderColorFormat.Rgba8);
        using OpenGLTexture2D texture = graphicsDevice.CreateTexture2D(1, 1, [255, 255, 255, 255]);
        using OpenGLIndexedMesh mesh = graphicsDevice.CreateIndexedMesh(3, [0, 1, 2]);
        using OpenGLIndexedMesh reversed = graphicsDevice.CreateIndexedMesh(3, [2, 1, 0]);

        C3PhyOutputVertex[] vertices =
        [
            Vertex(-0.9f, -0.9f, 0.25f, 0xFFFF0000),
            Vertex(0.9f, -0.9f, 0.25f, 0xFFFF0000),
            Vertex(0, 0.9f, 0.25f, 0xFFFF0000)
        ];

        renderer.BeginFrame();
        Draw(mesh, vertices, renderer, texture, MeshCullMode.Clockwise);
        SetVertexState(vertices, depth: 0.75f, color: 0xFF0000FF);
        Draw(mesh, vertices, renderer, texture, MeshCullMode.Clockwise);
        SetVertexState(vertices, depth: 0.05f, color: 0xFF00FF00);
        Draw(reversed, vertices, renderer, texture, MeshCullMode.Clockwise);
        SetVertexState(vertices, depth: 0.05f, color: 0x07FFFFFF);
        Draw(mesh, vertices, renderer, texture, MeshCullMode.Clockwise);

        byte[] first = renderer.ReadFrameTopLeftRgba();
        VerifyPixel(first, size, 4, 4, 255, 0, 0);
        VerifyPixel(first, size, 0, 0, 0, 0, 0);

        SetVertexState(vertices, depth: 0.05f, color: 0xFF00FF00);
        Draw(reversed, vertices, renderer, texture, MeshCullMode.None);
        byte[] second = renderer.ReadFrameTopLeftRgba();
        VerifyPixel(second, size, 4, 4, 0, 255, 0);

        renderer.DrawSprite(texture, 0, 0);
        byte[] third = renderer.ReadFrameTopLeftRgba();
        VerifyPixel(third, size, 0, 0, 255, 255, 255);

        SetVertexState(vertices, depth: 0.04f, color: 0xFFFFFFFF);
        reversed.Upload(MemoryMarshal.AsBytes(vertices.AsSpan()));
        renderer.DrawIndexedMeshShadow(reversed, Matrix4x4.Identity, MeshCullMode.None, 0.3f);
        byte[] shadow = renderer.ReadFrameTopLeftRgba();
        int center = (4 * size + 4) * 4;
        if (shadow[center] != 0 || shadow[center + 1] is < 176 or > 182 ||
            shadow[center + 2] != 0 || shadow[center + 3] != 255)
        {
            throw new InvalidDataException(
                $"Indexed mesh shadow center ({shadow[center]},{shadow[center + 1]}," +
                $"{shadow[center + 2]},{shadow[center + 3]}) did not blend over the main geometry.");
        }

        renderer.EndFrame();

        Console.WriteLine("Indexed mesh: 16-bit triangles, ARGB color, D3D depth conversion, culling, alpha test, geometric shadow blend, and sprite state passed.");
    }

    private static void Draw(OpenGLIndexedMesh mesh, C3PhyOutputVertex[] vertices,
        OpenGLRenderer renderer, OpenGLTexture2D texture, MeshCullMode cullMode)
    {
        mesh.Upload(MemoryMarshal.AsBytes(vertices.AsSpan()));
        renderer.DrawIndexedMesh(mesh, texture, Matrix4x4.Identity, cullMode);
    }

    private static C3PhyOutputVertex Vertex(float x, float y, float depth, uint diffuse) =>
        new(new Vector3(x, y, depth), Vector3.UnitZ, diffuse, new Vector2(0.5f));

    private static void SetVertexState(C3PhyOutputVertex[] vertices, float depth, uint color)
    {
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = vertices[i] with
            {
                Position = new Vector3(vertices[i].Position.X, vertices[i].Position.Y, depth),
                DiffuseArgb = color
            };
        }
    }

    private static void VerifyPixel(byte[] pixels, int width, int x, int y, byte red, byte green, byte blue)
    {
        int offset = (y * width + x) * 4;
        if (pixels[offset] != red || pixels[offset + 1] != green ||
            pixels[offset + 2] != blue || pixels[offset + 3] != 255)
        {
            throw new InvalidDataException(
                $"Indexed mesh pixel ({x},{y}) is ({pixels[offset]},{pixels[offset + 1]},{pixels[offset + 2]},{pixels[offset + 3]}), expected ({red},{green},{blue},255).");
        }
    }
}
