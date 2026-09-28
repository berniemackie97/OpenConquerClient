using OpenConquer.Client.UI.Hud;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudExperienceGeometryTests
{
    [Fact]
    public void CreateEnglishStyle10_ZeroValueProducesNoBands()
    {
        MainHudExperienceBandSequence sequence = MainHudExperienceGeometry.CreateEnglishStyle10(99, 555, 100, 0);

        Assert.Equal(0, sequence.Count);
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(50, 199)]
    [InlineData(99, 394)]
    [InlineData(100, 398)]
    public void CreateEnglishStyle10_PositiveValuesMatchNativeReplayWidths(int value, int expectedWidth)
    {
        MainHudExperienceBandSequence sequence = MainHudExperienceGeometry.CreateEnglishStyle10(99, 555, 100, value);

        Assert.Equal(3, sequence.Count);
        AssertBand(sequence.First, 99, 555, expectedWidth, 1, new SpriteColor(0xF1, 0xD0, 0x6E, 0xFF));
        AssertBand(sequence.Second, 99, 556, expectedWidth, 2, new SpriteColor(0xE8, 0xA3, 0x26, 0xFF));
        AssertBand(sequence.Third, 99, 558, expectedWidth, 1, new SpriteColor(0xAB, 0x91, 0x6C, 0xFF));
    }

    [Fact]
    public void CreateEnglishStyle10_UpperClampStopsAtFullWidth()
    {
        MainHudExperienceBandSequence sequence = MainHudExperienceGeometry.CreateEnglishStyle10(99, 555, 100, 101);

        Assert.Equal(3, sequence.Count);
        Assert.Equal(398, sequence.First.Width);
        Assert.Equal(398, sequence.Second.Width);
        Assert.Equal(398, sequence.Third.Width);
    }

    [Fact]
    public void CreateEnglishStyle10_NegativeValueMatchesNativeRedPath()
    {
        MainHudExperienceBandSequence sequence = MainHudExperienceGeometry.CreateEnglishStyle10(99, 555, 100, -1);

        Assert.Equal(1, sequence.Count);
        AssertBand(sequence.First, 99, 555, 3, 4, new SpriteColor(0xFF, 0x00, 0x00, 0xFF));
    }

    [Theory]
    [InlineData(100, 50, 199)]
    [InlineData(1000, 500, 199)]
    [InlineData(10000, 5000, 199)]
    public void CreateEnglishStyle10_UsesTheLiveRangeMaximum(int maximum, int value, int expectedWidth)
    {
        MainHudExperienceBandSequence sequence = MainHudExperienceGeometry.CreateEnglishStyle10(99, 555, maximum, value);

        Assert.Equal(3, sequence.Count);
        Assert.Equal(expectedWidth, sequence.First.Width);
        Assert.Equal(expectedWidth, sequence.Second.Width);
        Assert.Equal(expectedWidth, sequence.Third.Width);
    }

    [Theory]
    [InlineData(800, 600, 555)]
    [InlineData(1024, 768, 723)]
    public void CreateEnglishStyle10_ComposesWithVerifiedHudPlacement(int width, int height, int expectedY)
    {
        MainHudSkillExperienceLayout layout = MainHudSkillExperienceLayout.Create(new OpenConquer.Rendering.Presentation.LogicalRenderSize(width, height));

        MainHudExperienceBandSequence sequence = MainHudExperienceGeometry.CreateEnglishStyle10(MainHudSkillExperienceLayout.ExperienceX, layout.ExperienceY, 100, 50);

        Assert.Equal(3, sequence.Count);
        AssertBand(sequence.First, 99, expectedY, 199, 1, new SpriteColor(0xF1, 0xD0, 0x6E, 0xFF));
        AssertBand(sequence.Second, 99, expectedY + 1, 199, 2, new SpriteColor(0xE8, 0xA3, 0x26, 0xFF));
        AssertBand(sequence.Third, 99, expectedY + 3, 199, 1, new SpriteColor(0xAB, 0x91, 0x6C, 0xFF));
    }

    private static void AssertBand(MainHudExperienceBandDraw draw, int x, int y, int width, int height, SpriteColor color)
    {
        Assert.Equal(x, draw.X);
        Assert.Equal(y, draw.Y);
        Assert.Equal(width, draw.Width);
        Assert.Equal(height, draw.Height);
        Assert.Equal(color, draw.Color);
    }
}
