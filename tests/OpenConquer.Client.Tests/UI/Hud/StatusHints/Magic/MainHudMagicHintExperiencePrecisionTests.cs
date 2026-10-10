using System.Globalization;
using OpenConquer.Client.UI.Hud.StatusHints.Magic;

namespace OpenConquer.Client.Tests.UI.Hud.StatusHints.Magic;

public sealed class MainHudMagicHintExperiencePrecisionTests
{
    [Theory]
    [InlineData(0u, 2000u, "EXP: 0.000% ")]
    [InlineData(1000u, 2000u, "EXP: 50.000% ")]
    [InlineData(2000u, 2000u, "EXP: 100.000% ")]
    [InlineData(3000u, 2000u, "EXP: 150.000% ")]
    [InlineData(1302141499u, 164152212u, "EXP: 793.253% ")]
    public void CalculateExperiencePercentage_UsesNativeFloat32DivisionBeforeDoubleFormatting(uint current, uint required, string expected)
    {
        double percentage = MainHudMagicHintTextBuilder.CalculateExperiencePercentage(current, required);
        byte[] formatted = NativeMagicStringFormatter.Format("EXP: %0.3f%% "u8, 255, percentage);

        Assert.Equal(expected, System.Text.Encoding.ASCII.GetString(formatted));
    }

    [Fact]
    public void CalculateExperiencePercentage_DiffersFromPrematureDoublePromotionAtRoundingBoundary()
    {
        const uint current = 1302141499;
        const uint required = 164152212;

        double native = MainHudMagicHintTextBuilder.CalculateExperiencePercentage(current, required);
        double incorrect = (double)(float)((double)current * 100.0) / required;

        Assert.Equal("793.253", native.ToString("F3", CultureInfo.InvariantCulture));
        Assert.Equal("793.252", incorrect.ToString("F3", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void CalculateExperiencePercentage_RejectsInvalidZeroDenominator()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MainHudMagicHintTextBuilder.CalculateExperiencePercentage(1000, 0));
    }
}
