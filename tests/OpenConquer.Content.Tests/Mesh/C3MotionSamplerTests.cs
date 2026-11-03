using System.Numerics;
using OpenConquer.Content.Mesh;

namespace OpenConquer.Content.Tests.Mesh;

public sealed class C3MotionSamplerTests
{
    [Fact]
    public void SamplesKeyframesAndInterpolatesAllMatrixComponents()
    {
        C3Motion motion = new(1, 16, 0, [5, 15],
        [
            Matrix4x4.Identity,
            new Matrix4x4(
                3, 2, 0, 0, 0, 3, 0, 0,
                0, 0, 3, 0, 10, 20, 30, 1)
        ]);

        Assert.True(C3MotionSampler.TrySampleBone(motion, 0, -5, out Matrix4x4 before));
        Assert.Equal(Matrix4x4.Identity, before);

        Assert.True(C3MotionSampler.TrySampleBone(motion, 0, 10, out Matrix4x4 middle));
        Assert.Equal(2f, middle.M11);
        Assert.Equal(1f, middle.M12);
        Assert.Equal(new Vector3(5, 10, 15), middle.Translation);
        Assert.Equal(1f, middle.M44);

        Assert.True(C3MotionSampler.TrySampleBone(motion, 0, 15, out Matrix4x4 atEnd));
        Assert.True(C3MotionSampler.TrySampleBone(motion, 0, 30, out Matrix4x4 after));
        Assert.Equal(atEnd, after);
        Assert.Equal(10f, atEnd.M41);
    }

    [Fact]
    public void BlendUsesSecondaryWeightWithoutClamping()
    {
        C3Motion first = Single(Matrix4x4.CreateTranslation(10, 0, 0));
        C3Motion second = Single(Matrix4x4.CreateTranslation(30, 0, 0));
        Matrix4x4 result = Matrix4x4.Identity;

        Assert.True(C3MotionSampler.TrySampleBlended(
            first, 0, second, 0, 1.5f, 0, ref result));
        Assert.Equal(40f, result.M41);
        Assert.Equal(1f, result.M44);

        Assert.True(C3MotionSampler.TrySampleBlended(
            first, 0, null, 0, 0.5f, 0, ref result));
        Assert.Equal(10f, result.M41);
    }

    [Fact]
    public void MissingPrimaryLeavesOutputUntouchedAndMissingBoneWritesIdentity()
    {
        Matrix4x4 result = Matrix4x4.CreateTranslation(99, 0, 0);
        Assert.False(C3MotionSampler.TrySampleBlended(
            null, 0, null, 0, 0, 0, ref result));
        Assert.Equal(99f, result.M41);

        Assert.True(C3MotionSampler.TrySampleBone(Single(Matrix4x4.Identity),
            12, 0, out Matrix4x4 missingBone));
        Assert.Equal(Matrix4x4.Identity, missingBone);
        Assert.True(C3MotionSampler.TrySampleBone(null, 0, 0, out Matrix4x4 missingMotion));
        Assert.Equal(Matrix4x4.Identity, missingMotion);
    }

    [Fact]
    public void EmptyKeyframesCannotBeSampled()
    {
        C3Motion motion = new(1, 1, 0, [], []);
        Assert.False(C3MotionSampler.TrySampleBone(motion, 0, 0, out _));
    }

    private static C3Motion Single(Matrix4x4 matrix) => new(1, 1, 0, [0], [matrix]);
}
