using OpenConquer.Client.UI.Hud.StatusHints.Magic;
using OpenConquer.Content;
using OpenConquer.Rendering.Text.Fonts;

namespace OpenConquer.Client.UI.Hud.Rendering;

/// <summary>HUD content and its unclaimed CPU font faces. The content can survive a graphics context change.</summary>
internal sealed class MainHudPreparation(MainHudContent content, PreparedTextFonts fonts) : IDisposable
{
    internal const int CooldownFont = 0;
    internal const int StatusFont = 1;
    internal const int MagicFont = 2;

    internal MainHudContent Content { get; } = content;
    internal PreparedTextFonts Fonts { get; } = fonts;

    internal static MainHudPreparation Load(IClientContentSource source, MainHudContent? retainedContent,
        CancellationToken cancellationToken)
    {
        MainHudContent content = retainedContent ?? MainHudContent.Load(source, cancellationToken);
        PreparedTextFonts fonts = PreparedTextFonts.Prepare(content.FontSettingsConfiguration.GuiFontFaceName,
            content.FontSettingsConfiguration.AntialiasEnabled,
            [content.CooldownTextConfiguration.FontSizePixels, content.FontSizeConfiguration.NormalFontHeightPixels,
                MainHudMagicHintRenderer.HintFontHeight], cancellationToken);
        return new MainHudPreparation(content, fonts);
    }

    public void Dispose() => Fonts.Dispose();
}
