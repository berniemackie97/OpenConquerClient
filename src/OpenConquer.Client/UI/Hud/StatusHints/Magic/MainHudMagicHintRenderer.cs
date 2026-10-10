using OpenConquer.Content.Configuration;
using OpenConquer.Content.Magic;
using OpenConquer.Content.Text;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Client.UI.Hud.StatusHints.Magic;

internal sealed class MainHudMagicHintRenderer : IDisposable
{
    private const int NativeFontHeight = 12;
    private const int NativeWrapCharacterLimit = 30;
    private const int NativeWrapPixelLimit = 180;
    private const int NativeDataIconWidth = 21;

    private static readonly SpriteSourceRectangle s_backdropSource = new(0, 0, 100, 200);
    private static readonly SpriteColor s_outlineColor = new(204, 204, 204, 255);

    private readonly MainHudStatusHintRenderer _shared;
    private readonly MainHudMagicHintContent _content;
    private readonly ClientStringResources _strings;
    private readonly MainHudMagicHintTextBuilder _textBuilder;
    private readonly OpenGLTextContext _magicText;
    private readonly NativeTextRenderOptions _whiteOptions;
    private readonly NativeTextRenderOptions _redOptions;
    private readonly int _logicalWidth;
    private readonly bool _alternateTextAnchor;
    private readonly int _fontWidth;

    private bool _disposed;

    public MainHudMagicHintRenderer(OpenGLGraphicsDevice graphicsDevice, MainHudStatusHintRenderer shared, MainHudMagicHintContent content, ClientStringResources strings, ClientFontSettingsConfiguration fontSettings, ClientCodePageConfiguration codePage, LogicalRenderSize logicalRenderSize)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(shared);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(fontSettings);
        ArgumentNullException.ThrowIfNull(codePage);

        _shared = shared;
        _content = content;
        _strings = strings;
        _logicalWidth = logicalRenderSize.Width;
        _alternateTextAnchor = strings.UsesArabicLayout;

        _textBuilder = new MainHudMagicHintTextBuilder(content.Effects, strings, content.KeyedStrings, content.Subprofessions);

        _whiteOptions = CreateTextOptions(fontSettings, SpriteColor.White);
        _redOptions = CreateTextOptions(fontSettings, new SpriteColor(255, 0, 0, 255));

        OpenGLTextContext magicText = new(graphicsDevice, fontSettings.GuiFontFaceName, NativeFontHeight, codePage.EffectiveCodePage, fontSettings.AntialiasEnabled);

        try
        {
            _fontWidth = Math.Max(0, magicText.Layout("M"u8).WidthPixels);
            _magicText = magicText;
        }
        catch
        {
            try
            {
                magicText.Dispose();
            }
            catch
            {
                // Preserve the original font-layout initialization failure.
            }

            throw;
        }
    }

    public void Draw(OpenGLRenderer renderer, MainHudStatusHintState state, MainHudMagicHintRuntimeSnapshot? runtime)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(state);

        if (!state.CanRenderMagic || runtime is null)
        {
            return;
        }

        if (state.MagicType == 0)
        {
            DrawZeroMagic(renderer, state, runtime.ZeroMagic);
            return;
        }

        if (!runtime.TryFindLearnedMagic(state.MagicType, out MainHudLearnedMagic learned) || !_content.Types.TryGet(learned.Type, learned.Level, out MagicTypeRecord? record))
        {
            return;
        }

        IReadOnlyList<MainHudMagicHintTextGroup> groups = _textBuilder.Build(record, learned, runtime);
        DrawLearnedMagic(renderer, state, groups);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _magicText.Dispose();
        }
        finally
        {
            _disposed = true;
        }
    }

    private void DrawLearnedMagic(OpenGLRenderer renderer, MainHudStatusHintState state, IReadOnlyList<MainHudMagicHintTextGroup> groups)
    {
        int maximumWidth = 0;
        int lineCount = 0;

        foreach (MainHudMagicHintTextGroup group in groups)
        {
            byte[] normalized = TranslateTildes(group.EncodedText.Span);
            IReadOnlyList<byte[]> lines = Wrap(normalized);
            lineCount = checked(lineCount + lines.Count);

            foreach (byte[] line in lines)
            {
                int width = _magicText.Layout(line, recognizeDataIcons: true, dataIconWidthPixels: NativeDataIconWidth).WidthPixels;
                maximumWidth = Math.Max(maximumWidth, width);
            }
        }

        MainHudMagicHintGeometry geometry = MainHudMagicHintLayout.Learned(_logicalWidth, state.MagicAnchorX, state.MagicStoredAnchorY, maximumWidth, lineCount, _alternateTextAnchor);

        OpenGLTexture2D? backdrop = _shared.BackdropTexture;

        if (backdrop is not null)
        {
            renderer.DrawSprite(backdrop, s_backdropSource, geometry.BackdropX, geometry.BackdropY, geometry.Width, geometry.Height);
            renderer.DrawLineRectangle(geometry.BackdropX, geometry.BackdropY, checked(geometry.BackdropX + geometry.Width), checked(geometry.BackdropY + geometry.Height), s_outlineColor);
        }

        int textY = geometry.TextY;

        foreach (MainHudMagicHintTextGroup group in groups)
        {
            DrawGroup(renderer, group, geometry.TextX, ref textY);
        }
    }

    private void DrawGroup(OpenGLRenderer renderer, MainHudMagicHintTextGroup group, int x, ref int y)
    {
        ReadOnlySpan<byte> encoded = group.EncodedText.Span;

        if (encoded.IsEmpty)
        {
            return;
        }

        NativeTextRenderOptions options = group.Color == new SpriteColor(255, 0, 0, 255)
            ? _redOptions
            : _whiteOptions;

        byte[] normalized = TranslateTildes(encoded);

        if (normalized.Length <= NativeWrapCharacterLimit)
        {
            DrawLine(renderer, normalized, options, x, y);
            y = checked(y + NativeFontHeight);
            return;
        }

        foreach (byte[] line in Wrap(normalized))
        {
            DrawLine(renderer, line, options, x, y);
            y = checked(y + NativeFontHeight);
        }
    }

    private void DrawLine(OpenGLRenderer renderer, ReadOnlySpan<byte> encoded, NativeTextRenderOptions options, int x, int y)
    {
        if (encoded.IsEmpty)
        {
            return;
        }

        OpenGLTextLayout layout = _magicText.Layout(encoded);
        _magicText.Draw(renderer, layout, options, x, y);
    }

    private IReadOnlyList<byte[]> Wrap(ReadOnlySpan<byte> encoded)
    {
        return NativeMagicEnglishWrapper.Wrap(encoded, NativeWrapPixelLimit, _fontWidth, MeasureWidth);
    }

    private int MeasureWidth(ReadOnlySpan<byte> encoded) => _magicText.Layout(encoded).WidthPixels;

    private void DrawZeroMagic(OpenGLRenderer renderer, MainHudStatusHintState state, MainHudZeroMagicState context)
    {
        int anchorX = state.MagicAnchorX;
        int anchorY = state.MagicStoredAnchorY;
        OpenGLTexture2D? backdrop = _shared.BackdropTexture;

        if (context.CanRevive)
        {
            int stringId = anchorX >= _logicalWidth - 50 ? 10486 : 10355;
            DrawRevive(renderer, backdrop, anchorX, anchorY, _strings.GetEncoded(stringId));
            return;
        }

        if (context.Mode == 2)
        {
            if (backdrop is null)
            {
                return;
            }

            bool special = context.CanRestoreAppearance && anchorX == 750;

            DrawSimpleZeroMagic(renderer, backdrop, anchorX, anchorY, _strings.GetEncoded(special ? 10379 : 10357), 0, special);

            return;
        }

        if (context.CanRestoreAppearance)
        {
            if (backdrop is not null)
            {
                DrawSimpleZeroMagic(renderer, backdrop, anchorX, anchorY, _strings.GetEncoded(10379), -4, false);
            }

            return;
        }

        if (context.CanDescend)
        {
            if (backdrop is not null)
            {
                DrawSimpleZeroMagic(renderer, backdrop, anchorX, anchorY, _strings.GetEncoded(10396), 0, false);
            }

            return;
        }

        if (!context.CanTryOn || backdrop is null || context.TryoutItemName.IsEmpty)
        {
            return;
        }

        ReadOnlyMemory<byte> template = _content.KeyedStrings.GetEncoded("STR_WRAPSHOP_TRY_TIP");

        if (template.IsEmpty)
        {
            return;
        }

        byte[] formatted = NativeMagicStringFormatter.Format(template.Span, 255, context.TryoutItemName, unchecked((int)context.TryoutRemainingSeconds));

        DrawTryout(renderer, backdrop, anchorX, anchorY, NativeMagicStringFormatter.TranslateEscapes(formatted));
    }

    private void DrawRevive(OpenGLRenderer renderer, OpenGLTexture2D? backdrop, int anchorX, int anchorY, ReadOnlyMemory<byte> encoded)
    {
        if (encoded.IsEmpty)
        {
            return;
        }

        OpenGLTextLayout layout = _shared.NormalTextContext.Layout(encoded.Span);

        MainHudMagicHintGeometry geometry = MainHudMagicHintLayout.Revive(_logicalWidth, anchorX, anchorY, layout.WidthPixels, layout.HeightPixels, _alternateTextAnchor);

        DrawZeroBackdrop(renderer, backdrop, geometry);

        _shared.NormalTextContext.Draw(renderer, layout, _shared.NormalTextOptions, geometry.TextX, geometry.TextY);
    }

    private void DrawSimpleZeroMagic(OpenGLRenderer renderer, OpenGLTexture2D backdrop, int anchorX, int anchorY, ReadOnlyMemory<byte> encoded, int backdropOffsetX, bool specialTextAnchor)
    {
        if (encoded.IsEmpty)
        {
            return;
        }

        OpenGLTextLayout layout = _shared.NormalTextContext.Layout(encoded.Span);

        MainHudMagicHintGeometry geometry = MainHudMagicHintLayout.Simple(anchorX, anchorY, layout.WidthPixels, layout.HeightPixels, backdropOffsetX, _alternateTextAnchor, specialTextAnchor);

        if (layout.WidthPixels <= 0 || layout.HeightPixels <= 0)
        {
            return;
        }

        DrawZeroBackdrop(renderer, backdrop, geometry);

        _shared.NormalTextContext.Draw(renderer, layout, _shared.NormalTextOptions, geometry.TextX, geometry.TextY);
    }

    private void DrawTryout(OpenGLRenderer renderer, OpenGLTexture2D backdrop, int anchorX, int anchorY, ReadOnlySpan<byte> encoded)
    {
        if (encoded.IsEmpty)
        {
            return;
        }

        OpenGLTextLayout layout = _shared.NormalTextContext.Layout(encoded);

        if (layout.WidthPixels <= 0 || layout.HeightPixels <= 0)
        {
            return;
        }

        MainHudMagicHintGeometry geometry = MainHudMagicHintLayout.Tryout(_logicalWidth, anchorX, anchorY, layout.WidthPixels, layout.HeightPixels, _content.TryWrapTip.OffsetX, _content.TryWrapTip.OffsetY, _alternateTextAnchor);

        DrawZeroBackdrop(renderer, backdrop, geometry);

        _shared.NormalTextContext.Draw(renderer, layout, _shared.NormalTextOptions, geometry.TextX, geometry.TextY);
    }

    private static void DrawZeroBackdrop(OpenGLRenderer renderer, OpenGLTexture2D? backdrop, MainHudMagicHintGeometry geometry)
    {
        if (backdrop is not null && geometry.Width > 0 && geometry.Height > 0)
        {
            renderer.DrawSprite(backdrop, s_backdropSource, geometry.BackdropX, geometry.BackdropY, geometry.Width, geometry.Height);
        }
    }

    private static byte[] TranslateTildes(ReadOnlySpan<byte> encoded)
    {
        byte[] normalized = encoded.ToArray();

        for (int index = 0; index < normalized.Length; index++)
        {
            if (normalized[index] == (byte)'~')
            {
                normalized[index] = (byte)' ';
            }
        }

        return normalized;
    }

    private static NativeTextRenderOptions CreateTextOptions(ClientFontSettingsConfiguration settings, SpriteColor color)
    {
        uint argb = settings.DefaultCornerColorArgb;

        SpriteColor corner = new((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));

        return new NativeTextRenderOptions((NativeTextRenderStyle)settings.DefaultRenderTextStyle, color, corner, ClientFontSettingsConfiguration.DefaultCornerOffsetXPixels, ClientFontSettingsConfiguration.DefaultCornerOffsetYPixels, NativeTextVertexColors.Solid(color));
    }
}
