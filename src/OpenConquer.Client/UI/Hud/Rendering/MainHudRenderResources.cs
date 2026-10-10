using System.Runtime.ExceptionServices;
using OpenConquer.Client.UI.Hud.ActionButtons;
using OpenConquer.Client.UI.Hud.CheckControls;
using OpenConquer.Client.UI.Hud.Chrome;
using OpenConquer.Client.UI.Hud.Quickbar;
using OpenConquer.Client.UI.Hud.SelectedSkill;
using OpenConquer.Client.UI.Hud.SkillExperience;
using OpenConquer.Client.UI.Hud.StatusHints;
using OpenConquer.Client.UI.Hud.StatusHints.Magic;
using OpenConquer.Client.UI.Hud.Vitals;
using OpenConquer.Content.Configuration;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Hud.Rendering;

internal sealed class MainHudRenderResourceDependencies
{
    public required MainHudChromeAssets ChromeAssets
    {
        get; init;
    }

    public required MainHudVitalsAssets VitalsAssets
    {
        get; init;
    }

    public required MainHudSkillAssets SkillAssets
    {
        get; init;
    }

    public required MainHudQuickbarAssets QuickbarAssets
    {
        get; init;
    }

    public required MainHudActionButtonAssets ActionButtonAssets
    {
        get; init;
    }

    public required MainHudCheckControlAssets CheckControlAssets
    {
        get; init;
    }

    public required MainHudSelectedSkillAssets SelectedSkillAssets
    {
        get; init;
    }

    public required MainHudStatusHintAssets StatusHintAssets
    {
        get; init;
    }

    public required MainHudMagicHintContent MagicHintContent
    {
        get; init;
    }

    public required SelectedMagicCooldownTextConfiguration CooldownTextConfiguration
    {
        get; init;
    }

    public required ClientFontSettingsConfiguration FontSettingsConfiguration
    {
        get; init;
    }

    public required ClientFontSizeConfiguration FontSizeConfiguration
    {
        get; init;
    }

    public required ClientCodePageConfiguration CodePageConfiguration
    {
        get; init;
    }
}

internal sealed class MainHudRenderResources : IDisposable
{
    private readonly MainHudRendererOwnership _ownership = new();

    public MainHudChromeRenderer Chrome
    {
        get;
    }

    public MainHudVitalsRenderer Vitals
    {
        get;
    }

    public MainHudSkillExperienceRenderer SkillExperience
    {
        get;
    }

    public MainHudQuickbarRenderer Quickbar
    {
        get;
    }

    public MainHudActionButtonStripRenderer ActionButtons
    {
        get;
    }

    public MainHudCheckControlRenderer CheckControls
    {
        get;
    }

    public MainHudSelectedSkillRenderer SelectedSkill
    {
        get;
    }

    public MainHudSelectedSkillCooldownRenderer SelectedSkillCooldown
    {
        get;
    }

    public MainHudStatusHintRenderer StatusHints
    {
        get;
    }

    public MainHudMagicHintRenderer MagicHint
    {
        get;
    }

    public MainHudRenderResources(OpenGLGraphicsDevice graphicsDevice, LogicalRenderSize logicalRenderSize,
        MainHudRenderResourceDependencies dependencies, Func<uint> readTickCount)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(readTickCount);

        try
        {
            Chrome = _ownership.Track(new MainHudChromeRenderer(graphicsDevice, dependencies.ChromeAssets, logicalRenderSize));
            Vitals = _ownership.Track(new MainHudVitalsRenderer(graphicsDevice, dependencies.VitalsAssets, logicalRenderSize));
            SkillExperience = _ownership.Track(new MainHudSkillExperienceRenderer(graphicsDevice, dependencies.SkillAssets, logicalRenderSize));
            Quickbar = _ownership.Track(new MainHudQuickbarRenderer(graphicsDevice, dependencies.QuickbarAssets, logicalRenderSize, readTickCount: readTickCount));
            ActionButtons = _ownership.Track(new MainHudActionButtonStripRenderer(graphicsDevice, dependencies.ActionButtonAssets, logicalRenderSize));
            CheckControls = _ownership.Track(new MainHudCheckControlRenderer(graphicsDevice, dependencies.CheckControlAssets, logicalRenderSize));
            SelectedSkill = _ownership.Track(new MainHudSelectedSkillRenderer(graphicsDevice, dependencies.SelectedSkillAssets, logicalRenderSize));
            SelectedSkillCooldown = _ownership.Track(new MainHudSelectedSkillCooldownRenderer(graphicsDevice,
                dependencies.CooldownTextConfiguration, dependencies.FontSettingsConfiguration, dependencies.CodePageConfiguration, logicalRenderSize));
            StatusHints = _ownership.Track(new MainHudStatusHintRenderer(graphicsDevice, dependencies.StatusHintAssets,
                dependencies.FontSettingsConfiguration, dependencies.FontSizeConfiguration, dependencies.CodePageConfiguration, logicalRenderSize));
            MagicHint = _ownership.Track(new MainHudMagicHintRenderer(graphicsDevice, StatusHints, dependencies.MagicHintContent,
                dependencies.StatusHintAssets.Strings, dependencies.FontSettingsConfiguration, dependencies.CodePageConfiguration, logicalRenderSize));
        }
        catch
        {
            _ownership.DisposeAfterInitializationFailure();
            throw;
        }
    }

    public void Dispose() => _ownership.Dispose();
}

internal sealed class MainHudRendererOwnership : IDisposable
{
    private readonly List<IDisposable> _renderers = new(capacity: 10);
    private bool _disposed;

    public T Track<T>(T renderer) where T : class, IDisposable
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);

        try
        {
            _renderers.Add(renderer);
        }
        catch
        {
            try
            {
                renderer.Dispose();
            }
            catch
            {
                // Preserve the registration failure.
            }

            throw;
        }

        return renderer;
    }

    public void DisposeAfterInitializationFailure()
    {
        try
        {
            Dispose();
        }
        catch
        {
            // Preserve the renderer-construction failure.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ExceptionDispatchInfo? firstFailure = null;

        for (int index = _renderers.Count - 1; index >= 0; index--)
        {
            try
            {
                _renderers[index].Dispose();
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        _renderers.Clear();
        firstFailure?.Throw();
    }
}
