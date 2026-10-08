using System.Runtime.ExceptionServices;
using OpenConquer.Content.Images;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Hud.SkillExperience;

internal sealed class MainHudSkillExperienceRenderer : IDisposable
{
    private readonly MainHudSkillExperienceLayout _layout;
    private readonly OpenGLTexture2D? _skillFrame0;
    private readonly OpenGLTexture2D? _skillFrame2;

    private bool _disposed;

    public MainHudSkillExperienceRenderer(OpenGLGraphicsDevice graphicsDevice, MainHudSkillAssets assets, LogicalRenderSize logicalRenderSize)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(assets);

        _layout = MainHudSkillExperienceLayout.Create(logicalRenderSize);

        OpenGLTexture2D? skillFrame0 = null;
        OpenGLTexture2D? skillFrame2 = null;

        try
        {
            if (assets.HasSkill)
            {
                skillFrame0 = CreateTexture(graphicsDevice, assets.GetSkillFrame(0)!);
                skillFrame2 = CreateTexture(graphicsDevice, assets.GetSkillFrame(2)!);
            }

            _skillFrame0 = skillFrame0;
            _skillFrame2 = skillFrame2;
        }
        catch
        {
            DisposeCreatedTextures(skillFrame2, skillFrame0);
            throw;
        }
    }

    public void Draw(OpenGLRenderer renderer, MainHudSkillExperienceState state)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(state);

        if (state.Snapshot is not { } snapshot)
        {
            return;
        }

        DrawSkill(renderer, snapshot.Skill, state.SkillSubVariant);
        DrawExperience(renderer, snapshot);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ExceptionDispatchInfo? firstFailure = null;

        DisposeTexture(_skillFrame2, ref firstFailure);
        DisposeTexture(_skillFrame0, ref firstFailure);

        _disposed = true;
        firstFailure?.Throw();
    }

    private void DrawSkill(OpenGLRenderer renderer, int skill, int subVariant)
    {
        if (_skillFrame0 is null || _skillFrame2 is null)
        {
            return;
        }

        if (subVariant == 2)
        {
            renderer.DrawSprite(_skillFrame2, MainHudSkillExperienceLayout.SkillX, _layout.SkillY);
            return;
        }

        if (subVariant is not 0 and not 1)
        {
            throw new InvalidOperationException($"Unsupported Progress42 subvariant {subVariant}.");
        }

        MainHudGaugeDrawSequence sequence = MainHudGaugeGeometry.CreateStyle0(
            MainHudSkillExperienceLayout.SkillX, _layout.SkillY, width: 92, height: 92, alternateWidth: 92,
            maximum: 100, value: skill, follower: skill, subVariant);

        DrawSkillSequence(renderer, sequence);
    }

    private void DrawSkillSequence(OpenGLRenderer renderer, MainHudGaugeDrawSequence sequence)
    {
        if (sequence.Count >= 1)
        {
            DrawSkillGauge(renderer, sequence.First);
        }

        if (sequence.Count >= 2)
        {
            DrawSkillGauge(renderer, sequence.Second);
        }
    }

    private void DrawSkillGauge(OpenGLRenderer renderer, MainHudGaugeDraw draw)
    {
        OpenGLTexture2D texture = draw.FrameIndex switch
        {
            0 => _skillFrame0 ?? throw new InvalidOperationException("Progress42 frame 0 is unavailable."),
            2 => _skillFrame2 ?? throw new InvalidOperationException("Progress42 frame 2 is unavailable."),
            _ => throw new InvalidOperationException($"Progress42 geometry requested unsupported frame {draw.FrameIndex}."),
        };

        renderer.DrawRepeatedSprite(texture, draw.SourceBounds, draw.X, draw.Y, draw.Width, draw.ResolveDestinationHeight(texture.Height));
    }

    private void DrawExperience(OpenGLRenderer renderer, MainHudSkillExperienceSnapshot snapshot)
    {
        MainHudExperienceScale scale = MainHudExperienceScale.Create(snapshot.ExperienceMaximum, snapshot.Experience);
        MainHudExperienceBandSequence sequence = MainHudExperienceGeometry.CreateEnglishStyle10(
            MainHudSkillExperienceLayout.ExperienceX, _layout.ExperienceY, scale.Maximum, scale.Value);

        if (sequence.Count >= 1)
        {
            DrawExperienceBand(renderer, sequence.First);
        }

        if (sequence.Count >= 2)
        {
            DrawExperienceBand(renderer, sequence.Second);
        }

        if (sequence.Count >= 3)
        {
            DrawExperienceBand(renderer, sequence.Third);
        }
    }

    private static void DrawExperienceBand(OpenGLRenderer renderer, MainHudExperienceBandDraw draw)
    {
        renderer.DrawSolidRectangle(draw.X, draw.Y, draw.Width, draw.Height, draw.Color);
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
