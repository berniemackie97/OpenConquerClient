using System.Runtime.ExceptionServices;
using OpenConquer.Content.Images;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Hud;

internal sealed class MainHudVitalsRenderer : IDisposable
{
    private readonly MainHudVitalsLayout _layout;

    private readonly OpenGLTexture2D? _lifeFrame0;
    private readonly OpenGLTexture2D? _lifeFrame1;
    private readonly OpenGLTexture2D? _lifeFrame2;
    private readonly OpenGLTexture2D? _manaFrame0;
    private readonly OpenGLTexture2D? _manaFrame1;
    private readonly OpenGLTexture2D? _manaFrame2;
    private readonly OpenGLTexture2D? _staminaFrame0;
    private readonly OpenGLTexture2D? _extendedStaminaFrame0;

    private bool _disposed;

    public MainHudVitalsRenderer(OpenGLGraphicsDevice graphicsDevice, MainHudVitalsAssets assets, LogicalRenderSize logicalRenderSize)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(assets);

        _layout = MainHudVitalsLayout.Create(logicalRenderSize);

        OpenGLTexture2D? lifeFrame0 = null;
        OpenGLTexture2D? lifeFrame1 = null;
        OpenGLTexture2D? lifeFrame2 = null;
        OpenGLTexture2D? manaFrame0 = null;
        OpenGLTexture2D? manaFrame1 = null;
        OpenGLTexture2D? manaFrame2 = null;
        OpenGLTexture2D? staminaFrame0 = null;
        OpenGLTexture2D? extendedStaminaFrame0 = null;

        try
        {
            if (assets.HasLife)
            {
                lifeFrame0 = CreateTexture(graphicsDevice, assets.GetLifeFrame(0)!);
                lifeFrame1 = CreateTexture(graphicsDevice, assets.GetLifeFrame(1)!);
                lifeFrame2 = CreateTexture(graphicsDevice, assets.GetLifeFrame(2)!);
            }

            if (assets.HasMana)
            {
                manaFrame0 = CreateTexture(graphicsDevice, assets.GetManaFrame(0)!);
                manaFrame1 = CreateTexture(graphicsDevice, assets.GetManaFrame(1)!);
                manaFrame2 = CreateTexture(graphicsDevice, assets.GetManaFrame(2)!);
            }

            if (assets.HasStamina)
            {
                staminaFrame0 = CreateTexture(graphicsDevice, assets.GetStaminaFrame(0)!);
            }

            if (assets.HasExtendedStamina)
            {
                extendedStaminaFrame0 = CreateTexture(graphicsDevice, assets.GetExtendedStaminaFrame(0)!);
            }

            _lifeFrame0 = lifeFrame0;
            _lifeFrame1 = lifeFrame1;
            _lifeFrame2 = lifeFrame2;
            _manaFrame0 = manaFrame0;
            _manaFrame1 = manaFrame1;
            _manaFrame2 = manaFrame2;
            _staminaFrame0 = staminaFrame0;
            _extendedStaminaFrame0 = extendedStaminaFrame0;
        }
        catch
        {
            DisposeCreatedTextures(extendedStaminaFrame0, staminaFrame0, manaFrame2, manaFrame1, manaFrame0, lifeFrame2, lifeFrame1, lifeFrame0);
            throw;
        }
    }

    public void Draw(OpenGLRenderer renderer, MainHudVitalsState state)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(state);

        if (state.Snapshot is not { } snapshot)
        {
            return;
        }

        DrawLife(renderer, snapshot, state.LifeSubVariant);
        DrawMana(renderer, snapshot, state.ManaSubVariant);
        DrawStamina(renderer, snapshot);
        DrawExtendedStamina(renderer, snapshot);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ExceptionDispatchInfo? firstFailure = null;

        DisposeTexture(_extendedStaminaFrame0, ref firstFailure);
        DisposeTexture(_staminaFrame0, ref firstFailure);
        DisposeTexture(_manaFrame2, ref firstFailure);
        DisposeTexture(_manaFrame1, ref firstFailure);
        DisposeTexture(_manaFrame0, ref firstFailure);
        DisposeTexture(_lifeFrame2, ref firstFailure);
        DisposeTexture(_lifeFrame1, ref firstFailure);
        DisposeTexture(_lifeFrame0, ref firstFailure);

        _disposed = true;
        firstFailure?.Throw();
    }

    private void DrawLife(OpenGLRenderer renderer, MainHudVitalsSnapshot snapshot, int subVariant)
    {
        if (_lifeFrame0 is null || _lifeFrame1 is null || _lifeFrame2 is null)
        {
            return;
        }

        MainHudGaugeDrawSequence sequence = MainHudGaugeGeometry.CreateStyle0(MainHudVitalsLayout.LifeX, _layout.LifeY, 36, 74, 86, snapshot.MaxLife, snapshot.CurrentLife, snapshot.LifeFollower, subVariant);
        DrawSequence(renderer, sequence, _lifeFrame0, _lifeFrame1, _lifeFrame2);
    }

    private void DrawMana(OpenGLRenderer renderer, MainHudVitalsSnapshot snapshot, int subVariant)
    {
        if (_manaFrame0 is null || _manaFrame1 is null || _manaFrame2 is null)
        {
            return;
        }

        MainHudGaugeDrawSequence sequence = MainHudGaugeGeometry.CreateStyle0(MainHudVitalsLayout.ManaX, _layout.ManaY, 34, 74, 34, snapshot.MaxMana, snapshot.CurrentMana, snapshot.ManaFollower, subVariant);
        DrawSequence(renderer, sequence, _manaFrame0, _manaFrame1, _manaFrame2);
    }

    private void DrawStamina(OpenGLRenderer renderer, MainHudVitalsSnapshot snapshot)
    {
        if (_staminaFrame0 is null)
        {
            return;
        }

        MainHudGaugeDrawSequence sequence = MainHudGaugeGeometry.CreateStyle0(MainHudVitalsLayout.StaminaX, _layout.StaminaY, 8, 70, 8, snapshot.MaxStamina, snapshot.Stamina, snapshot.Stamina, subVariant: 0);
        DrawSequence(renderer, sequence, _staminaFrame0, frame1: null, frame2: null);
    }

    private void DrawExtendedStamina(OpenGLRenderer renderer, MainHudVitalsSnapshot snapshot)
    {
        if (_extendedStaminaFrame0 is null || !snapshot.HasExtendedStaminaGauge || snapshot.Stamina < 100)
        {
            return;
        }

        int value = snapshot.Stamina - 100;
        MainHudGaugeDrawSequence sequence = MainHudGaugeGeometry.CreateStyle0(MainHudVitalsLayout.ExtendedStaminaX, _layout.ExtendedStaminaY, 8, 35, 8, 50, value, value, subVariant: 0);
        DrawSequence(renderer, sequence, _extendedStaminaFrame0, frame1: null, frame2: null);
    }

    private static void DrawSequence(OpenGLRenderer renderer, MainHudGaugeDrawSequence sequence, OpenGLTexture2D frame0, OpenGLTexture2D? frame1, OpenGLTexture2D? frame2)
    {
        if (sequence.Count >= 1)
        {
            DrawGauge(renderer, sequence.First, frame0, frame1, frame2);
        }

        if (sequence.Count >= 2)
        {
            DrawGauge(renderer, sequence.Second, frame0, frame1, frame2);
        }
    }

    private static void DrawGauge(OpenGLRenderer renderer, MainHudGaugeDraw draw, OpenGLTexture2D frame0, OpenGLTexture2D? frame1, OpenGLTexture2D? frame2)
    {
        OpenGLTexture2D texture = draw.FrameIndex switch
        {
            0 => frame0,
            1 => frame1 ?? throw new InvalidOperationException("Gauge geometry requested unavailable frame 1."),
            2 => frame2 ?? throw new InvalidOperationException("Gauge geometry requested unavailable frame 2."),
            _ => throw new InvalidOperationException($"Gauge geometry requested unsupported frame {draw.FrameIndex}."),
        };

        renderer.DrawRepeatedSprite(texture, draw.SourceBounds, draw.X, draw.Y, draw.Width, draw.ResolveDestinationHeight(texture.Height));
    }

    private static OpenGLTexture2D CreateTexture(OpenGLGraphicsDevice graphicsDevice, RgbaImage image)
    {
        return graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
    }

    private static void DisposeTexture(OpenGLTexture2D? texture, ref ExceptionDispatchInfo? firstFailure)
    {
        try
        {
            texture?.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }
    }

    private static void DisposeCreatedTextures(params OpenGLTexture2D?[] textures)
    {
        foreach (OpenGLTexture2D? texture in textures)
        {
            try
            {
                texture?.Dispose();
            }
            catch
            {
                // Preserve the texture-creation failure that initiated cleanup.
            }
        }
    }
}
