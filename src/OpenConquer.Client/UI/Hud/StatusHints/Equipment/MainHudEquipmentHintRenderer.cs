using OpenConquer.Content.Configuration;
using OpenConquer.Content.Text;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Hud.StatusHints.Equipment;

internal sealed class MainHudEquipmentHintRenderer
{
    private const int TextIconWidth = 21;
    private static readonly SpriteSourceRectangle s_backdropSource = new(0, 0, 100, 100);

    private readonly MainHudStatusHintRenderer _shared;
    private readonly LogicalRenderSize _resolution;
    private readonly OpenGLTextLayout _mainEquipmentText;
    private readonly OpenGLTextLayout _alternateEquipmentText;
    private readonly int _fontHeight;
    private readonly bool _alternateLayout;

    public MainHudEquipmentHintRenderer(MainHudStatusHintRenderer shared, ClientKeyedStringResources strings, ClientFontSizeConfiguration fontSize, LogicalRenderSize resolution, bool alternateLayout)
    {
        ArgumentNullException.ThrowIfNull(shared);
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(fontSize);

        _shared = shared;
        _resolution = resolution;
        _fontHeight = fontSize.NormalFontHeightPixels;
        _alternateLayout = alternateLayout;
        _mainEquipmentText = CreateLayout(strings.GetEncoded("STR_SWAP_MAJOR_WEAPONTIP"));
        _alternateEquipmentText = CreateLayout(strings.GetEncoded("STR_SWAP_SUB_WEAPONTIP"));
    }

    public void Draw(OpenGLRenderer renderer, MainHudStatusHintState state, uint? equipmentSwitchState)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(state);

        if (!state.CanRenderEquipmentSwitch || equipmentSwitchState is not uint equipmentState)
        {
            return;
        }

        OpenGLTextLayout text = equipmentState == 1 ? _mainEquipmentText : _alternateEquipmentText;
        MainHudEquipmentHintGeometry geometry = MainHudEquipmentHintLayout.Calculate(_resolution, state.MagicAnchorX, state.MagicStoredAnchorY, text.WidthPixels, _fontHeight, _alternateLayout);
        OpenGLTexture2D? backdrop = _shared.BackdropTexture;

        if (backdrop is not null)
        {
            renderer.DrawSprite(backdrop, s_backdropSource, geometry.BackdropX, geometry.BackdropY, geometry.BackdropWidth, geometry.BackdropHeight);
        }

        _shared.NormalTextContext.Draw(renderer, text, _shared.NormalTextOptions, geometry.TextX, geometry.TextY);
    }

    private OpenGLTextLayout CreateLayout(ReadOnlyMemory<byte> encoded) => _shared.NormalTextContext.Layout(encoded.Span, recognizeDataIcons: true, dataIconWidthPixels: TextIconWidth);
}
