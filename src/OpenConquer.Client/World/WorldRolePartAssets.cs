using System.Diagnostics.CodeAnalysis;
using OpenConquer.Content;
using OpenConquer.Content.Images;
using OpenConquer.Content.Mesh;

namespace OpenConquer.Client.World;

internal sealed class WorldRolePartAssets(
    RgbaImage image, IReadOnlyList<(C3PhyMesh Mesh, C3Motion Motion)> meshes,
    Dictionary<string, C3Motion> attachments, WorldRoleActionTracks actions)
{
    internal RgbaImage Image { get; } = image;
    internal IReadOnlyList<(C3PhyMesh Mesh, C3Motion Motion)> Meshes { get; } = meshes;
    internal WorldRoleActionTracks Actions { get; } = actions;

    internal bool TryGetAttachment(string name, [NotNullWhen(true)] out C3Motion? motion) =>
        attachments.TryGetValue(name, out motion);

    internal static WorldRolePartAssets Load(IClientContentSource source, C3RoleCatalog catalog,
        C3ResolvedPart part, CancellationToken token, string? actionMotionPath = null,
        IReadOnlyDictionary<ushort, string>? additionalActions = null)
    {
        token.ThrowIfCancellationRequested();
        List<C3MeshSection> geometry = [];
        List<C3Motion> motions = [];
        ReadSections(C3MeshFile.Load(source, part.MeshPath), geometry, motions);
        if (geometry.Count == 0 || geometry.Count != motions.Count)
        {
            throw new InvalidDataException("The character part contains mismatched geometry and motion sections.");
        }

        if (actionMotionPath is not null)
        {
            token.ThrowIfCancellationRequested();
            List<C3Motion> actionMotions = [];
            ReadSections(C3MeshFile.Load(source, actionMotionPath), [], actionMotions);
            if (actionMotions.Count < geometry.Count)
            {
                throw new InvalidDataException("The character action has fewer tracks than its geometry.");
            }

            motions = actionMotions;
        }

        Dictionary<ushort, C3Motion[]> alternatives = [];
        if (additionalActions is not null)
        {
            Dictionary<string, C3Motion[]> decodedPaths = new(StringComparer.OrdinalIgnoreCase);
            foreach ((ushort action, string path) in additionalActions)
            {
                token.ThrowIfCancellationRequested();
                if (!decodedPaths.TryGetValue(path, out C3Motion[]? tracks))
                {
                    List<C3Motion> decoded = [];
                    ReadSections(C3MeshFile.Load(source, path), [], decoded);
                    if (decoded.Count < geometry.Count)
                    {
                        throw new InvalidDataException("The character action has fewer tracks than its geometry.");
                    }

                    tracks = [.. decoded];
                    decodedPaths.Add(path, tracks);
                }

                alternatives.Add(action, tracks);
            }
        }

        token.ThrowIfCancellationRequested();
        RgbaImage image = DdsImageLoader.Load(source, part.TexturePath, ContentLookupMode.PackageOnly);
        List<(C3PhyMesh Mesh, C3Motion Motion)> meshes = [];
        List<int> drawableTrackIndices = [];
        Dictionary<string, C3Motion> attachments = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> attachmentTrackIndices = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < geometry.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            C3PhyMesh mesh = C3PhyMeshReader.Read(geometry[i]);
            C3Motion motion = motions[i];
            if (catalog.IsAttachmentMesh(mesh.Name))
            {
                if (!attachments.TryAdd(mesh.Name, motion))
                {
                    throw new InvalidDataException("The character part contains duplicate attachment submeshes.");
                }

                attachmentTrackIndices.Add(mesh.Name, i);
            }
            else
            {
                meshes.Add((mesh, motion));
                drawableTrackIndices.Add(i);
            }
        }

        if (meshes.Count == 0)
        {
            throw new InvalidDataException("The character part contains no drawable geometry.");
        }

        WorldRoleActionTracks actions = new(
            actionMotionPath is null ? 0 : motions[0].FrameCount,
            [.. drawableTrackIndices], attachmentTrackIndices, alternatives);
        return new WorldRolePartAssets(image, meshes, attachments, actions);
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
}
