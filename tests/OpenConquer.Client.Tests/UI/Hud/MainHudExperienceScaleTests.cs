using OpenConquer.Client.UI.Hud.SkillExperience;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudExperienceScaleTests
{
    [Fact]
    public void Create_UsesShiftZeroWhenHighDwordIsZero()
    {
        MainHudExperienceScale scale = MainHudExperienceScale.Create(0x0000_0000_FFFF_FFFFul, 0x0000_0000_89AB_CDEFul);

        Assert.Equal(0, scale.Shift);
        Assert.Equal(unchecked((int)0xFFFF_FFFFu), scale.Maximum);
        Assert.Equal(unchecked((int)0x89AB_CDEFu), scale.Value);
    }

    [Fact]
    public void Create_UsesShiftSixteenWhenHighDwordIsNonZeroAndBits48Through63AreZero()
    {
        MainHudExperienceScale scale = MainHudExperienceScale.Create(0x0000_0001_0000_0000ul, 0x0000_0000_FFFF_0000ul);

        Assert.Equal(16, scale.Shift);
        Assert.Equal(0x0001_0000, scale.Maximum);
        Assert.Equal(0x0000_FFFF, scale.Value);
    }

    [Fact]
    public void Create_KeepsShiftSixteenAtItsUpperBoundary()
    {
        MainHudExperienceScale scale = MainHudExperienceScale.Create(0x0000_FFFF_FFFF_FFFFul, 0);

        Assert.Equal(16, scale.Shift);
        Assert.Equal(unchecked((int)0xFFFF_FFFFu), scale.Maximum);
        Assert.Equal(0, scale.Value);
    }

    [Fact]
    public void Create_UsesShiftThirtyTwoWhenBits48Through63AreNonZero()
    {
        MainHudExperienceScale scale = MainHudExperienceScale.Create(0x0001_0000_0000_0000ul, 0x0000_FFFF_0000_0000ul);

        Assert.Equal(32, scale.Shift);
        Assert.Equal(0x0001_0000, scale.Maximum);
        Assert.Equal(0x0000_FFFF, scale.Value);
    }

    [Fact]
    public void Create_ShiftThirtyTwoReturnsTheOriginalHighDword()
    {
        MainHudExperienceScale scale = MainHudExperienceScale.Create(0xFFFF_FFFF_FFFF_FFFFul, 0x89AB_CDEF_0123_4567ul);

        Assert.Equal(32, scale.Shift);
        Assert.Equal(unchecked((int)0xFFFF_FFFFu), scale.Maximum);
        Assert.Equal(unchecked((int)0x89AB_CDEFu), scale.Value);
    }

    [Fact]
    public void Create_AppliesTheMaximumSelectedShiftToCurrentExperience()
    {
        MainHudExperienceScale scale = MainHudExperienceScale.Create(0x0000_0001_2345_6789ul, 0x8000_1234_5678_9ABCul);

        Assert.Equal(16, scale.Shift);
        Assert.Equal(0x0001_2345, scale.Maximum);
        Assert.Equal(0x1234_5678, scale.Value);
    }
}
