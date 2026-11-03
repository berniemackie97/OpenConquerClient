using System.Numerics;

namespace OpenConquer.Content.Mesh;

internal static class C3PhyNormals
{
    private const float SharedPositionTolerance = 0.01f;

    internal static void Generate(C3PhyVertex[] vertices, ushort[] indices)
    {
        Vector3[] normals = new Vector3[vertices.Length];

        for (int i = 0; i < indices.Length; i += 3)
        {
            int a = indices[i];
            int b = indices[i + 1];
            int c = indices[i + 2];
            Vector3 position = vertices[a].Position;
            Vector3 face = Vector3.Cross(vertices[c].Position - position, vertices[b].Position - position);
            if (face.LengthSquared() == 0)
            {
                continue;
            }

            face = Vector3.Normalize(face);
            normals[a] += face;
            normals[b] += face;
            normals[c] += face;
        }

        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = Normalize(normals[i]);
        }

        Dictionary<(long X, long Y, long Z), List<int>> cells = [];
        List<int> matches = [];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 current = vertices[i].Position;
            (long x, long y, long z) = Cell(current);
            matches.Clear();

            for (long dz = -1; dz <= 1; dz++)
            {
                for (long dy = -1; dy <= 1; dy++)
                {
                    for (long dx = -1; dx <= 1; dx++)
                    {
                        if (cells.TryGetValue((x + dx, y + dy, z + dz), out List<int>? previous))
                        {
                            matches.AddRange(previous);
                        }
                    }
                }
            }

            matches.Sort();
            foreach (int previous in matches)
            {
                Vector3 other = vertices[previous].Position;
                if (MathF.Abs(current.X - other.X) > SharedPositionTolerance ||
                    MathF.Abs(current.Y - other.Y) > SharedPositionTolerance ||
                    MathF.Abs(current.Z - other.Z) > SharedPositionTolerance)
                {
                    continue;
                }

                normals[i] = Normalize(normals[i] + normals[previous]);
                normals[previous] = normals[i];
            }

            if (!cells.TryGetValue((x, y, z), out List<int>? entries))
            {
                entries = [];
                cells.Add((x, y, z), entries);
            }

            entries.Add(i);
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = vertices[i] with { Normal = normals[i] };
        }
    }

    private static Vector3 Normalize(Vector3 normal) =>
        normal.LengthSquared() > 0 ? Vector3.Normalize(normal) : Vector3.Zero;

    private static (long X, long Y, long Z) Cell(Vector3 position) =>
        ((long)Math.Floor(position.X / SharedPositionTolerance),
         (long)Math.Floor(position.Y / SharedPositionTolerance),
         (long)Math.Floor(position.Z / SharedPositionTolerance));
}
