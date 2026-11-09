using System.Diagnostics.CodeAnalysis;
using OpenConquer.Content.Mesh;

namespace OpenConquer.Client.World;

internal sealed class WorldRoleActionTracks(
    uint idleFrameCount, int[] drawableTrackIndices, Dictionary<string, int> attachmentTrackIndices,
    Dictionary<ushort, C3Motion[]> actionMotions)
{
    internal uint IdleFrameCount { get; } = idleFrameCount;
    internal IReadOnlyList<int> DrawableTrackIndices { get; } = drawableTrackIndices;

    internal bool TryGetAttachment(string name, ushort action, [NotNullWhen(true)] out C3Motion? motion)
    {
        if (attachmentTrackIndices.TryGetValue(name, out int index))
        {
            return TryGetMotion(action, index, out motion);
        }

        motion = null;
        return false;
    }

    internal bool TryGetMotion(ushort action, int index, [NotNullWhen(true)] out C3Motion? motion)
    {
        if (actionMotions.TryGetValue(action, out C3Motion[]? tracks))
        {
            motion = tracks[index];
            return true;
        }

        motion = null;
        return false;
    }

    internal uint GetFrameCount(ushort action) =>
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
}
