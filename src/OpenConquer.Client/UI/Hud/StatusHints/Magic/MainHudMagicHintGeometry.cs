namespace OpenConquer.Client.UI.Hud.StatusHints.Magic;

internal readonly record struct MainHudMagicHintGeometry(int BackdropX, int BackdropY, int TextX, int TextY, int Width, int Height);

internal static class MainHudMagicHintLayout
{
    private const int FontHeight = 12;
    private const int Padding = 5;
    private const int StoredAnchorAdjustment = 42;
    private const int ZeroMagicVerticalAdjustment = 60;

    public static MainHudMagicHintGeometry Learned(int logicalWidth, int anchorX, int storedAnchorY, int maximumTextWidth, int wrappedLineCount, bool alternateTextAnchor)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(logicalWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumTextWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(wrappedLineCount);

        int width = checked(maximumTextWidth + Padding * 2);
        int height = checked(wrappedLineCount * FontHeight + Padding * 2);

        int backdropX = (long)anchorX + width + 2 <= logicalWidth
            ? anchorX
            : logicalWidth - 2 - width;

        int backdropY = Math.Max(0, storedAnchorY - StoredAnchorAdjustment - height);
        int textX = alternateTextAnchor ? checked(backdropX + width - Padding) : checked(backdropX + Padding);

        return new MainHudMagicHintGeometry(backdropX, backdropY, textX, checked(backdropY + Padding), width, height);
    }

    public static MainHudMagicHintGeometry Revive(int logicalWidth, int anchorX, int storedAnchorY, int width, int height, bool alternateTextAnchor)
    {
        bool fits = (long)anchorX + width <= logicalWidth;
        int backdropX = fits ? anchorX - 3 : logicalWidth - 3 - width;

        int textX = alternateTextAnchor
            ? fits ? anchorX + width - 3 : logicalWidth - 3
            : backdropX;

        int y = storedAnchorY - ZeroMagicVerticalAdjustment;

        return new MainHudMagicHintGeometry(backdropX, y, textX, y, width, height);
    }

    public static MainHudMagicHintGeometry Simple(int anchorX, int storedAnchorY, int width, int height, int backdropOffsetX, bool alternateTextAnchor, bool specialTextAnchor)
    {
        int textX = specialTextAnchor
            ? anchorX
            : alternateTextAnchor ? checked(anchorX + width) : anchorX;

        int y = storedAnchorY - ZeroMagicVerticalAdjustment;

        return new MainHudMagicHintGeometry(checked(anchorX + backdropOffsetX), y, textX, y, width, height);
    }

    public static MainHudMagicHintGeometry Tryout(int logicalWidth, int anchorX, int storedAnchorY, int width, int height, int offsetX, int offsetY, bool alternateTextAnchor)
    {
        int backdropX = alternateTextAnchor
            ? anchorX
            : (long)anchorX + width >= logicalWidth
                ? logicalWidth - width
                : anchorX;

        int textX = alternateTextAnchor ? checked(anchorX + width) : backdropX;
        int y = checked(storedAnchorY - ZeroMagicVerticalAdjustment + offsetY);

        return new MainHudMagicHintGeometry(checked(backdropX + offsetX), y, checked(textX + offsetX), y, width, height);
    }
}
