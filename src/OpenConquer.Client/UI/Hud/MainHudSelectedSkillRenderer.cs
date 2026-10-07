using System.Runtime.ExceptionServices;
using OpenConquer.Content.Images;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Hud;

internal sealed class MainHudSelectedSkillRenderer : IDisposable
{
    private readonly OpenGLGraphicsDevice _graphicsDevice;
    private readonly MainHudSelectedSkillAssets _assets;
    private readonly MainHudSelectedSkillLayout _layout;

    private OpenGLTexture2D? _selectedTexture;
    private OpenGLTexture2D? _coverTexture;
    private string? _selectedSectionName;
    private bool _selectedTextureResolved;
    private bool _coverTextureResolved;
    private bool _disposed;

    public MainHudSelectedSkillRenderer(
        OpenGLGraphicsDevice graphicsDevice,
        MainHudSelectedSkillAssets assets,
        LogicalRenderSize logicalRenderSize)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(assets);

        _graphicsDevice = graphicsDevice;
        _assets = assets;
        _layout = MainHudSelectedSkillLayout.Create(logicalRenderSize);
    }

    public void Draw(OpenGLRenderer renderer, MainHudSelectedSkillState state)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(state);

        MainHudSelectedSkillBounds bounds = _layout.GetBounds();

        if (state.IsImageActive)
        {
            OpenGLTexture2D? selectedTexture = GetSelectedTexture(state.SectionName);

            if (selectedTexture is not null)
            {
                renderer.DrawSprite(
                    selectedTexture,
                    new SpriteSourceRectangle(
                        0,
                        0,
                        MainHudSelectedSkillDefinition.SourceWidth,
                        MainHudSelectedSkillDefinition.SourceHeight),
                    bounds.X,
                    bounds.Y,
                    MainHudSelectedSkillBounds.Width,
                    MainHudSelectedSkillBounds.Height);
            }
        }

        if (state.IsCovered)
        {
            OpenGLTexture2D? coverTexture = GetCoverTexture();

            if (coverTexture is not null)
            {
                renderer.DrawSprite(
                    coverTexture,
                    new SpriteSourceRectangle(
                        0,
                        0,
                        MainHudSelectedSkillDefinition.CoverSourceWidth,
                        MainHudSelectedSkillDefinition.CoverSourceHeight),
                    bounds.X,
                    bounds.Y,
                    MainHudSelectedSkillBounds.Width,
                    MainHudSelectedSkillBounds.Height);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _selectedTexture?.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _coverTexture?.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        _selectedTexture = null;
        _coverTexture = null;
        _selectedSectionName = null;
        _selectedTextureResolved = false;
        _coverTextureResolved = false;
        _disposed = true;

        firstFailure?.Throw();
    }

    private OpenGLTexture2D? GetSelectedTexture(string sectionName)
    {
        if (_selectedTextureResolved && string.Equals(_selectedSectionName, sectionName, StringComparison.Ordinal))
        {
            return _selectedTexture;
        }

        ReplaceSelectedTexture(sectionName);
        return _selectedTexture;
    }

    private void ReplaceSelectedTexture(string sectionName)
    {
        ArgumentException.ThrowIfNullOrEmpty(sectionName);

        _selectedTexture?.Dispose();
        _selectedTexture = null;
        _selectedSectionName = sectionName;
        _selectedTextureResolved = true;

        try
        {
            RgbaImage? image = _assets.GetSelectedFrame(sectionName, MainHudSelectedSkillDefinition.FrameIndex);

            if (image is null)
            {
                return;
            }

            ValidateMinimumDimensions(
                $"selected-skill section [{sectionName}]",
                image,
                MainHudSelectedSkillDefinition.SourceWidth,
                MainHudSelectedSkillDefinition.SourceHeight);

            _selectedTexture = _graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
        }
        catch
        {
            _selectedSectionName = null;
            _selectedTextureResolved = false;
            throw;
        }
    }

    private OpenGLTexture2D? GetCoverTexture()
    {
        if (_coverTextureResolved)
        {
            return _coverTexture;
        }

        _coverTextureResolved = true;

        try
        {
            RgbaImage? image = _assets.GetCoverFrame();

            if (image is null)
            {
                return null;
            }

            ValidateMinimumDimensions(
                $"selected-skill cover [{MainHudSelectedSkillDefinition.CoverSectionName}]",
                image,
                MainHudSelectedSkillDefinition.CoverSourceWidth,
                MainHudSelectedSkillDefinition.CoverSourceHeight);

            _coverTexture = _graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
            return _coverTexture;
        }
        catch
        {
            _coverTextureResolved = false;
            throw;
        }
    }

    private static void ValidateMinimumDimensions(string assetName, RgbaImage image, int minimumWidth, int minimumHeight)
    {
        if (image.Width < minimumWidth || image.Height < minimumHeight)
        {
            throw new InvalidDataException(
                $"{assetName} must be at least {minimumWidth}x{minimumHeight} pixels for the verified native source rectangle; found {image.Width}x{image.Height}.");
        }
    }
}
