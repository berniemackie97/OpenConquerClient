using System.Numerics;

namespace OpenConquer.Content.Mesh;

public static class C3MotionSampler
{
    public static bool TrySampleBone(C3Motion? motion, uint boneIndex, int framePosition, out Matrix4x4 matrix)
    {
        if (motion is null || boneIndex >= motion.BoneCount)
        {
            matrix = Matrix4x4.Identity;
            return true;
        }

        int count = motion.KeyframeCount;
        if (count == 0)
        {
            matrix = default;
            return false;
        }

        int bone = (int)boneIndex;
        int firstFrame = unchecked((int)motion.GetKeyframeIndex(0));
        if (framePosition <= firstFrame)
        {
            matrix = motion.GetBoneMatrix(0, bone);
            return true;
        }

        int lastFrame = unchecked((int)motion.GetKeyframeIndex(count - 1));
        if (framePosition >= lastFrame)
        {
            matrix = motion.GetBoneMatrix(count - 1, bone);
            return true;
        }

        int previous = 0;
        int next = count - 1;
        while (next - previous > 1)
        {
            int middle = previous + (next - previous) / 2;
            if (framePosition < unchecked((int)motion.GetKeyframeIndex(middle)))
            {
                next = middle;
            }
            else
            {
                previous = middle;
            }
        }

        int previousFrame = unchecked((int)motion.GetKeyframeIndex(previous));
        int nextFrame = unchecked((int)motion.GetKeyframeIndex(next));
        float weight = (float)((long)framePosition - previousFrame) / ((long)nextFrame - previousFrame);
        matrix = Interpolate(motion.GetBoneMatrix(previous, bone), motion.GetBoneMatrix(next, bone), weight);
        return true;
    }

    public static bool TrySampleBlended(C3Motion? primary, int primaryFrame, C3Motion? secondary,
        int secondaryFrame, float secondaryAlpha, uint boneIndex, ref Matrix4x4 matrix)
    {
        if (primary is null || !TrySampleBone(primary, boneIndex, primaryFrame, out Matrix4x4 primaryMatrix))
        {
            return false;
        }

        if (secondary is null)
        {
            matrix = primaryMatrix;
            return true;
        }

        if (!TrySampleBone(secondary, boneIndex, secondaryFrame, out Matrix4x4 secondaryMatrix))
        {
            return false;
        }

        matrix = Interpolate(primaryMatrix, secondaryMatrix, secondaryAlpha);
        return true;
    }

    private static Matrix4x4 Interpolate(Matrix4x4 left, Matrix4x4 right, float weight)
    {
        float complement = 1f - weight;
        return new Matrix4x4(
            complement * left.M11 + weight * right.M11,
            complement * left.M12 + weight * right.M12,
            complement * left.M13 + weight * right.M13,
            complement * left.M14 + weight * right.M14,
            complement * left.M21 + weight * right.M21,
            complement * left.M22 + weight * right.M22,
            complement * left.M23 + weight * right.M23,
            complement * left.M24 + weight * right.M24,
            complement * left.M31 + weight * right.M31,
            complement * left.M32 + weight * right.M32,
            complement * left.M33 + weight * right.M33,
            complement * left.M34 + weight * right.M34,
            complement * left.M41 + weight * right.M41,
            complement * left.M42 + weight * right.M42,
            complement * left.M43 + weight * right.M43,
            complement * left.M44 + weight * right.M44);
    }
}
