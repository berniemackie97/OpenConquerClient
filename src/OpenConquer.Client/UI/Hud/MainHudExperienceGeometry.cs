using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Hud;

internal readonly record struct MainHudExperienceBandDraw(int X, int Y, int Width, int Height, SpriteColor Color);

internal readonly record struct MainHudExperienceBandSequence
{
    private MainHudExperienceBandSequence(int count, MainHudExperienceBandDraw first, MainHudExperienceBandDraw second, MainHudExperienceBandDraw third)
    {
        Count = count;
        First = first;
        Second = second;
        Third = third;
    }

    public int Count
    {
        get;
    }

    public MainHudExperienceBandDraw First
    {
        get;
    }

    public MainHudExperienceBandDraw Second
    {
        get;
    }

    public MainHudExperienceBandDraw Third
    {
        get;
    }

    public static MainHudExperienceBandSequence Single(MainHudExperienceBandDraw draw) => new(1, draw, default, default);
    public static MainHudExperienceBandSequence Triple(MainHudExperienceBandDraw first, MainHudExperienceBandDraw second, MainHudExperienceBandDraw third) => new(3, first, second, third);
}

internal static class MainHudExperienceGeometry
{
    private static readonly SpriteColor s_topColor = new(0xF1, 0xD0, 0x6E, 0xFF);
    private static readonly SpriteColor s_middleColor = new(0xE8, 0xA3, 0x26, 0xFF);
    private static readonly SpriteColor s_bottomColor = new(0xAB, 0x91, 0x6C, 0xFF);
    private static readonly SpriteColor s_negativeColor = new(0xFF, 0x00, 0x00, 0xFF);

    public static MainHudExperienceBandSequence CreateEnglishStyle10(int x, int y, int maximum, int value)
    {
        if (maximum <= 0)
        {
            return default;
        }

        int clampedValue = Math.Min(value, maximum);

        if (clampedValue == 0)
        {
            return default;
        }

        float pixelsPerUnit = (float)MainHudSkillExperienceLayout.ExperienceWidth / maximum;
        int pixels = (int)(clampedValue * pixelsPerUnit);

        if (clampedValue < 0)
        {
            return MainHudExperienceBandSequence.Single(new MainHudExperienceBandDraw(x, y, -pixels, MainHudSkillExperienceLayout.ExperienceHeight, s_negativeColor));
        }

        MainHudExperienceBandDraw top = new(x, y, pixels, 1, s_topColor);
        MainHudExperienceBandDraw middle = new(x, y + 1, pixels, 2, s_middleColor);
        MainHudExperienceBandDraw bottom = new(x, y + 3, pixels, 1, s_bottomColor);

        return MainHudExperienceBandSequence.Triple(top, middle, bottom);
    }
}
