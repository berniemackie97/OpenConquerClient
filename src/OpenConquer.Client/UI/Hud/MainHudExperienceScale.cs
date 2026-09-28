namespace OpenConquer.Client.UI.Hud;

internal readonly record struct MainHudExperienceScale(int Shift, int Maximum, int Value)
{
    public static MainHudExperienceScale Create(ulong maximum, ulong value)
    {
        int shift = maximum >> 48 != 0
            ? 32
            : maximum >> 32 != 0
                ? 16
                : 0;

        int scaledMaximum = unchecked((int)(unchecked((long)maximum) >> shift));
        int scaledValue = unchecked((int)(unchecked((long)value) >> shift));

        return new MainHudExperienceScale(shift, scaledMaximum, scaledValue);
    }
}
