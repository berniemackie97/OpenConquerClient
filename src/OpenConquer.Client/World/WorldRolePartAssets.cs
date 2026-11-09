using System.Diagnostics.CodeAnalysis;
using OpenConquer.Content;
using OpenConquer.Content.Images;
using OpenConquer.Content.Mesh;

namespace OpenConquer.Client.World;

internal sealed class WorldRolePartAssets(
    RgbaImage image, IReadOnlyList<(C3PhyMesh Mesh, C3Motion Motion)> meshes,
    Dictionary<string, C3Motion> attachments, uint frameCount, int[] drawableTrackIndices,
    Dictionary<string, int> attachmentTrackIndices, Dictionary<ushort, C3Motion[]> actionMotions)
{
    internal RgbaImage Image { get; } = image;
    internal IReadOnlyList<(C3PhyMesh Mesh, C3Motion Motion)> Meshes { get; } = meshes;
    internal uint FrameCount { get; } = frameCount;
    internal IReadOnlyList<int> DrawableTrackIndices { get; } = drawableTrackIndices;
    internal IEnumerable<ushort> ActionIds => actionMotions.Keys;

    internal bool TryGetAttachment(string name, [NotNullWhen(true)] out C3Motion? motion) =>
        attachments.TryGetValue(name, out motion);

    internal bool TryGetActionAttachment(string name, ushort action, [NotNullWhen(true)] out C3Motion? motion)
    {
        if (attachmentTrackIndices.TryGetValue(name, out int index))
        {
            return TryGetActionMotion(action, index, out motion);
        }

        motion = null;
        return false;
    }

    internal bool TryGetActionMotion(ushort action, int trackIndex, [NotNullWhen(true)] out C3Motion? motion)
    {
        if (actionMotions.TryGetValue(action, out C3Motion[]? tracks))
        {
            motion = tracks[trackIndex];
            return true;
        }

        motion = null;
        return false;
    }

    internal uint GetActionFrameCount(ushort action) =>
        actionMotions.TryGetValue(action, out C3Motion[]? tracks) ? tracks[0].FrameCount : 0;

    internal int MaximumBoneCount(int trackIndex, C3Motion defaultMotion)
    {
        int maximum = checked((int)defaultMotion.BoneCount);
        foreach (C3Motion[] tracks in actionMotions.Values)
        {
            maximum = Math.Max(maximum, checked((int)tracks[trackIndex].BoneCount));
        }

        return maximum;
    }

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
            foreach ((ushort action, string path) in additionalActions)
            {
                token.ThrowIfCancellationRequested();
                List<C3Motion> tracks = [];
                ReadSections(C3MeshFile.Load(source, path), [], tracks);
                if (tracks.Count < geometry.Count)
                {
                    throw new InvalidDataException("The character action has fewer tracks than its geometry.");
                }

                alternatives.Add(action, [.. tracks]);
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

        return new WorldRolePartAssets(image, meshes, attachments,
            actionMotionPath is null ? 0 : motions[0].FrameCount,
            [.. drawableTrackIndices], attachmentTrackIndices, alternatives);
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
