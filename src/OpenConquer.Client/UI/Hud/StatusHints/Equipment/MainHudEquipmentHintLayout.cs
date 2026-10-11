using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Hud.StatusHints.Equipment;

internal readonly record struct MainHudEquipmentHintGeometry(int BackdropX, int BackdropY, int BackdropWidth, int BackdropHeight, int TextX, int TextY);

internal static class MainHudEquipmentHintLayout
{
    private const int HorizontalOffset = 3;
    private const int VerticalOffset = 60;
    private const int Padding = 5;

    public static MainHudEquipmentHintGeometry Calculate(LogicalRenderSize resolution, int anchorX, int storedAnchorY, int textWidth, int fontHeight, bool alternateLayout)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(textWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontHeight);

        int x = unchecked(anchorX + (alternateLayout ? textWidth : 0) - HorizontalOffset);
        int y = unchecked(storedAnchorY - VerticalOffset);
        int verticalExtent = unchecked(-12 - fontHeight);

        if (unchecked(x + textWidth) > resolution.Width)
        {
            x = unchecked(resolution.Width - textWidth);
        }
        else if (x < 0)
        {
            x = 10;
        }

        if (unchecked(y + verticalExtent) > resolution.Height)
        {
            y = unchecked(resolution.Height - verticalExtent);
        }
        else if (y < 0)
        {
            y = 10;
        }

        int backdropHeight = unchecked((int)((uint)unchecked(3 * fontHeight) >> 1) + Padding * 2);
        int textX = alternateLayout ? unchecked(x + textWidth) : x;

        return new MainHudEquipmentHintGeometry(unchecked(x - Padding), unchecked(y - Padding), unchecked(textWidth + Padding * 2), backdropHeight, textX, y);
    }
}
