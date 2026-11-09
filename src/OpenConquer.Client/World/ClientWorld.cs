using System.Numerics;
using OpenConquer.Client.Diagnostics;
using OpenConquer.Client.UI.Hud.Quickbar;
using OpenConquer.Client.UI.Hud.Rendering;
using OpenConquer.Client.UI.Hud.SelectedSkill;
using OpenConquer.Client.UI.Hud.Vitals;
using OpenConquer.Content;
using OpenConquer.Content.Progression;
using OpenConquer.Networking.Game.Handshake;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Platform.Geometry;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.World;

namespace OpenConquer.Client.World;

/// <summary>
/// One admitted world, from the GameServer handoff until its close has been observed: the authenticated session, its
/// inbound frames and state, the map load and scene, the hero renderer and the HUD. Graphics resources are created
/// lazily on the window's context and released by <see cref="ReleaseGraphics"/>, separately from the network session.
/// </summary>
internal sealed class ClientWorld : IClientWorld
{
    private const int MapTextureUploadsPerFrame = 4;
    private readonly IClientContentSource _content;
    private readonly LogicalRenderSize _logicalRenderSize;
    private readonly Action<OpenGLRenderer> _drawRole;
    private readonly Action<OpenGLRenderer, uint, Matrix4x4, Matrix4x4> _drawRemoteRole;
    private readonly Action<ReadOnlyMemory<byte>> _applyFrame;
    private readonly Action _onClosing;

    private Func<ValueTask>? _closeSession;
    private GameWorldEntrySnapshot? _entry;
    private readonly TimeProvider _clock;
    private readonly ClientDiagnostics _diagnostics;
    private readonly GameSilentInfoReport _silentInfo;
    private uint _frameMilliseconds;
    private readonly uint _clientStartedAt;
    private WorldFrameIngress? _ingress;
    private WorldFrameEgress? _egress;
    private WorldInboundState? _inbound;
    private LevelExperienceTable? _levelExperience;
    private WorldContentLoad<WorldEntryCatalogs>? _catalogLoad;
    private WorldContentLoad<WorldMapScene>? _mapLoad;
    private WorldMapScene? _scene;
    private WorldMapImageLoader? _mapImageLoader;
    private WorldMapTextureRenderer? _mapTextures;
    private WorldRoleLoad? _roleLoad;
    private readonly WorldRemoteRolePresentation? _remoteRolePresentation;
    private WorldRoleRenderer? _role;
    private WorldRoleSources? _roleSources;
    private WorldContentLoad<MainHudPreparation>? _hudLoad;
    private MainHudContent? _hudContent;
    private MainHudPreparation? _hudPreparation;
    private MainHudSelectedSkillImageLoader? _selectedSkillImages;
    private MainHudQuickbarImageLoader? _quickbarImages;
    private WorldHudRuntime? _hud;
    private OpenGLRenderer? _renderer;
    private LogicalRenderSize _rendererSize;
    private bool _texturesReady;
    private Task? _closing;
    private Task? _disposeTask;
    private WorldCloseReason _closeReason;
    private bool _closeObserved;

    /// <summary>
    /// Starts content preparation for an admitted world. <c>onClosing</c> runs once, synchronously, when the world starts
    /// closing. <c>clientStartedAt</c> is the client millisecond counter at client start (native hero construction).
    /// </summary>
    public ClientWorld(GameServerHandshakeSession game, GameWorldEntrySnapshot entry, IClientContentSource content,
        LogicalRenderSize logicalRenderSize, Action onClosing, uint clientStartedAt, uint mapStartedAt, TimeProvider clock, ClientDiagnostics? diagnostics = null)
        : this(entry, content, logicalRenderSize, onClosing,
            (game ?? throw new ArgumentNullException(nameof(game))).DisposeAsync,
            () => new WorldFrameIngress(game), () => new WorldContentLoad<WorldMapScene>(
                token => WorldEntryContent.LoadMap(content, entry, mapStartedAt, token)),
            () => new WorldFrameEgress(
                (now, token) => game.SendWorldLatencyTickAsync(entry.Character.EntityId, now, token),
                (address, latency, now, token) => game.SendWorldLatencyReportAsync(address, latency, now, token),
                (correction, now, token) => game.SendWorldCorrectionReplyAsync(entry.Character.EntityId,
                    correction.X, correction.Y, correction.DirectionIndex, entry.MapId, now, token),
                (reply, now, token) => game.SendWorldActionResponseAsync(reply, now, token)),
            clock, clientStartedAt, diagnostics, game.SilentInfo)
    {
    }

    /// <summary>
    /// Builds a world from its parts. Ingress can prefetch while content loads. Hydration applies before live frames.
    /// </summary>
    internal ClientWorld(GameWorldEntrySnapshot entry, IClientContentSource content, LogicalRenderSize logicalRenderSize,
        Action onClosing, Func<ValueTask> closeSession, Func<WorldFrameIngress> createIngress, Func<WorldContentLoad<WorldMapScene>> createMapLoad,
        Func<WorldFrameEgress>? createEgress = null, TimeProvider? clock = null, uint clientStartedAt = 0,
        ClientDiagnostics? diagnostics = null, GameSilentInfoReport? silentInfo = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(onClosing);
        ArgumentNullException.ThrowIfNull(closeSession);
        ArgumentNullException.ThrowIfNull(createIngress);
        ArgumentNullException.ThrowIfNull(createMapLoad);

        _content = content;
        _clock = clock ?? TimeProvider.System;
        _diagnostics = diagnostics ?? ClientDiagnostics.Silent;
        _silentInfo = silentInfo ?? GameSilentInfoReport.WithoutFiles;
        _clientStartedAt = clientStartedAt;
        _logicalRenderSize = logicalRenderSize;
        _onClosing = onClosing;
        _drawRole = DrawRole;
        _drawRemoteRole = DrawRemoteRole;
        _applyFrame = ApplyFrame;
        _closeSession = closeSession;
        _entry = entry;

        _ingress = createIngress();
        _egress = createEgress?.Invoke();
        _mapLoad = createMapLoad();
        _catalogLoad = new WorldContentLoad<WorldEntryCatalogs>(token => WorldEntryContent.LoadCatalogs(content, token));
        WorldRoleAssetReader reader = new(content, _diagnostics);
        _roleLoad = new WorldRoleLoad(reader, entry.Character.PackedAppearance);
        _remoteRolePresentation = new WorldRemoteRolePresentation(reader, _diagnostics);
    }

    /// <summary>True until the world starts closing.</summary>
    public bool IsActive => _closing is null && _disposeTask is null;

    /// <summary>The map scene is loaded.</summary>
    public bool HasScene => _scene is not null;

    /// <summary>The first map view, role appearance and HUD are ready to draw.</summary>
    public bool IsPresentable => IsActive && _scene is not null && _texturesReady && _roleSources.HasValue && _hud is not null;

    /// <summary>The session and workers have closed and their results have been observed.</summary>
    public bool IsClosed => _closeObserved && _mapLoad is null && _catalogLoad is null && _hudLoad is null;

    public void DrainIngress(uint frameMilliseconds)
    {
        _frameMilliseconds = frameMilliseconds;
        if (_ingress is not { } ingress)
        {
            return;
        }

        try
        {
            if (_inbound is not { } state)
            {
                ingress.ObserveConnectionFailure();
                return;
            }

            ingress.Drain(_applyFrame);
            state.CullRemoteRoles();
            if (_scene is { } scene)
            {
                SynchronizeSceneRoles(scene, state);
            }
            _egress?.Pump(state.Heartbeat, _frameMilliseconds);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or TimeoutException or
            System.Net.Sockets.SocketException or UnauthorizedAccessException)
        {
            _diagnostics.WorldConnectionClosed(ClientFailure.Classify(exception));
            BeginClose(WorldCloseReason.ConnectionLost);
        }
    }

    /// <summary>Applies completed content work or observes a canceled job after the world has started closing.</summary>
    public void CompleteLoad()
    {
        try
        {
            if (_catalogLoad is { IsCompleted: true } catalogLoad)
            {
                _catalogLoad = null;
                WorldEntryCatalogs catalogs = catalogLoad.Complete();
                if (IsActive && _entry is { } entry)
                {
                    _inbound = new WorldInboundState(entry, catalogs.Items, () => _frameMilliseconds,
                        _clientStartedAt, _diagnostics,
                        _egress is { } egress ? egress.QueuePositionCorrection : null,
                        _egress is { } writer ? writer.QueueWorldActionResponse : null, _silentInfo);
                    _levelExperience = catalogs.Experience;
                }
            }

            if ((_catalogLoad is null || !IsActive) && _mapLoad is { IsCompleted: true } mapLoad)
            {
                _mapLoad = null;
                WorldMapScene scene = mapLoad.Complete();
                if (IsActive && _inbound is { } inbound)
                {
                    ApplyCamera(scene, inbound);
                    SynchronizeSceneRoles(scene, inbound);
                    _scene = scene;
                }
            }

            if (_hudLoad is { IsCompleted: true } hudLoad)
            {
                _hudLoad = null;
                MainHudPreparation preparation = hudLoad.Complete();
                if (IsActive)
                {
                    _hudContent = preparation.Content;
                    _hudPreparation = preparation;
                }
                else
                {
                    preparation.Dispose();
                }
            }
        }
        catch (OperationCanceledException) when (!IsActive)
        {
            // The owner canceled its content jobs during close.
        }
        catch (WorldResponseQueueFullException exception)
        {
            if (IsActive)
            {
                _diagnostics.WorldConnectionClosed(ClientFailure.Classify(exception));
                BeginClose(WorldCloseReason.ConnectionLost);
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            if (IsActive)
            {
                _diagnostics.WorldEntryContentUnavailable(ClientFailure.Classify(exception));
                BeginClose(WorldCloseReason.ContentUnavailable);
            }
        }
        catch
        {
            // Release world ownership before propagating an unexpected preparation failure.
            BeginClose(WorldCloseReason.ContentUnavailable);
            throw;
        }
    }

    public void RefreshAppearance(OpenGLGraphicsDevice? graphicsDevice)
    {
        if (_scene is null || _inbound is not { } state || _entry is not { } entry || _roleLoad is not { } load)
        {
            return;
        }

        WorldRoleSources sources = WorldRoleSources.Resolve(
            entry.Character.PackedAppearance, entry.Character.HairComposite, state.Equipment);
        try
        {
            if (load.TryPrepare(sources, _roleSources, out WorldRoleAssets? assets))
            {
                OpenGLGraphicsDevice device = graphicsDevice ??
                    throw new InvalidOperationException("World graphics device is unavailable.");
                WorldRoleRenderer? next = assets is null ? null : WorldRoleRenderer.Create(device, assets);
                WorldRoleRenderer? previous = _role;
                _role = next;
                _roleSources = sources;
                previous?.Dispose();
                if (next is null && previous is null)
                {
                    _diagnostics.WorldRoleMissing();
                }
            }

        }
        catch (Exception exception) when (WorldRoleLoad.IsContentFailure(exception))
        {
            // A failure preparing the current appearance means required world content is unavailable.
            _diagnostics.WorldRoleUnavailable(ClientFailure.Classify(exception));
            BeginClose(WorldCloseReason.ContentUnavailable);
        }
    }

    /// <summary>Prepares the first complete map view while the login overlay is still visible.</summary>
    public void PrepareFirstTextures(DesktopWindow window, OpenGLGraphicsDevice device, OpenGLRenderer fallback)
    {
        if (_scene is null || (_texturesReady && _hud is not null))
        {
            return;
        }

        try
        {
            PrepareHudGraphics(device);
            _hud?.PrepareFrame();
            if (!_texturesReady)
            {
                _texturesReady = PrepareMapTextures(device, GetRenderer(window, device, fallback));
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            CloseForPresentationContentFailure(exception);
        }
    }

    public OpenGLRenderer GetRenderer(DesktopWindow window, OpenGLGraphicsDevice? graphicsDevice, OpenGLRenderer fallback)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(fallback);

        PixelSize framebuffer = window.FramebufferSize;
        if (framebuffer.Width == 0 || framebuffer.Height == 0)
        {
            return _renderer ?? fallback;
        }

        OpenGLGraphicsDevice device = graphicsDevice ?? throw new InvalidOperationException("The graphics device is unavailable.");
        WorldMapScene scene = _scene ?? throw new InvalidOperationException("The world scene is unavailable.");
        LogicalRenderSize size = WorldViewportSize.FromFramebuffer(_logicalRenderSize, framebuffer,
            scene.BackdropExtent, device.MaximumRenderTargetDimension);
        if (_renderer is { } existing && _rendererSize == size)
        {
            return existing;
        }

        OpenGLRenderer next = device.CreateRenderer(size, framebuffer.Width, framebuffer.Height,
            PresentationPolicy.Stretch, RenderColorFormat.Rgba8);
        OpenGLRenderer? previous = _renderer;
        _renderer = next;
        _rendererSize = size;
        previous?.Dispose();
        return next;
    }

    public void PrepareFrame(OpenGLGraphicsDevice device, OpenGLRenderer renderer)
    {
        try
        {
            PrepareHudGraphics(device);
            _hud?.PrepareFrame();
            _ = PrepareMapTextures(device, renderer);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            CloseForPresentationContentFailure(exception);
        }
    }

    private void PrepareHudGraphics(OpenGLGraphicsDevice device)
    {
        if (_hud is not null)
        {
            return;
        }

        if (_hudPreparation is { } preparation)
        {
            _hudPreparation = null;
            using (preparation)
            {
                _selectedSkillImages ??= new MainHudSelectedSkillImageLoader(preparation.Content.SelectedSkillAssets);
                _quickbarImages ??= new MainHudQuickbarImageLoader(preparation.Content.QuickbarAssets);
                _hud = new WorldHudRuntime(preparation, _selectedSkillImages, _quickbarImages, device,
                    _logicalRenderSize, _clock, magicHintSource: null);
            }
        }
        else
        {
            IClientContentSource source = _content;
            MainHudContent? retainedContent = _hudContent;
            _hudLoad ??= new WorldContentLoad<MainHudPreparation>(
                token => MainHudPreparation.Load(source, retainedContent, token));
        }
    }

    private bool PrepareMapTextures(OpenGLGraphicsDevice device, OpenGLRenderer renderer)
    {
        if (_scene is not { } scene)
        {
            return false;
        }

        if (_inbound is { } state)
        {
            if (_roleSources.HasValue && _remoteRolePresentation is { } remote)
            {
                remote.Synchronize(state.RemoteRoles, state.X, state.Y);
                remote.Prepare(device);
            }
        }

        _mapImageLoader ??= new WorldMapImageLoader(_content);
        _mapTextures ??= WorldMapTextureRenderer.CreateStreaming(_mapImageLoader, device);
        return scene.Prepare(_mapTextures, renderer.Viewport.LogicalWidth, renderer.Viewport.LogicalHeight,
            _frameMilliseconds, textureBudget: MapTextureUploadsPerFrame);
    }

    private void CloseForPresentationContentFailure(Exception exception)
    {
        _diagnostics.WorldPresentationUnavailable(ClientFailure.Classify(exception));
        BeginClose(WorldCloseReason.ContentUnavailable);
    }

    public void DrawFrame(OpenGLRenderer renderer, DesktopWindow window)
    {
        if (_scene is not { } scene || _mapTextures is not { } textures)
        {
            return;
        }

        scene.Draw(textures, renderer, _role is null ? null : _drawRole, _drawRemoteRole);
        if (_hud is { } hud && _inbound is { } state && _levelExperience is { } experienceTable &&
            _entry is { Character.TransformLookSourceId: 0 })
        {
            WorldHeroVitals vitals = state.Hero.AdvanceVitals(experienceTable);
            hud.SetWorldValues(new MainHudVitalsSnapshot(vitals.CurrentLife, vitals.LifeFollower, vitals.MaximumLife,
                vitals.CurrentMana, vitals.ManaFollower, vitals.MaximumMana,
                vitals.Stamina, vitals.MaximumStamina, vitals.HasExtendedStamina),
                vitals.SkillGaugePercent, vitals.Experience, vitals.NextLevelExperience);
        }

        _hud?.Draw(renderer, window, _frameMilliseconds);
    }

    public void HandlePointerMoved(PixelPoint point)
    {
        if (_hud is { } hud && _renderer is { } renderer)
        {
            bool mapped = renderer.Viewport.TryMapPointerToLogical(point.X, point.Y, out int x, out int y);
            hud.HandlePointerMoved(mapped, x, y);
        }
    }

    public void HandlePointerPressed(PixelPoint point)
    {
        if (_hud is { } hud && _renderer is { } renderer)
        {
            bool mapped = renderer.Viewport.TryMapPointerToLogical(point.X, point.Y, out int x, out int y);
            hud.HandlePrimaryPointerPressed(mapped, x, y);
        }
    }

    public void HandlePointerReleased(PixelPoint point)
    {
        if (_hud is { } hud && _renderer is { } renderer)
        {
            bool mapped = renderer.Viewport.TryMapPointerToLogical(point.X, point.Y, out int x, out int y);
            hud.HandlePrimaryPointerReleased(mapped, x, y);
        }
    }

    public void ResizeFramebuffer(PixelSize size) => _renderer?.ResizeHostFramebuffer(size.Width, size.Height);

    /// <summary>Releases the world's OpenGL resources when the window's context goes away.</summary>
    public void ReleaseGraphics()
    {
        WorldHudRuntime? hud = _hud;
        WorldRoleRenderer? role = _role;
        OpenGLRenderer? renderer = _renderer;
        WorldMapTextureRenderer? mapTextures = _mapTextures;
        _mapTextures = null;
        _texturesReady = false;
        _hud = null;
        _role = null;
        _roleSources = null;
        _renderer = null;
        ClientShutdown cleanup = new();
        cleanup.Run(() => hud?.Dispose());
        cleanup.Run(() => role?.Dispose());
        cleanup.Run(() => _remoteRolePresentation?.ReleaseGraphics());
        cleanup.Run(() => mapTextures?.Dispose());
        cleanup.Run(() => renderer?.Dispose());
        cleanup.ThrowIfFailed();
    }

    /// <summary>
    /// Starts closing: drops the scene and its graphics, cancels preparation and closes the session in the background.
    /// </summary>
    public void BeginClose(WorldCloseReason reason)
    {
        if (_closing is not null || _disposeTask is not null)
        {
            return;
        }

        _closeReason = reason;
        WorldRoleLoad? roleLoad = _roleLoad;
        _roleLoad = null;
        WorldRemoteRolePresentation? remoteRolePresentation = _remoteRolePresentation;
        WorldMapImageLoader? mapImageLoader = _mapImageLoader;
        _mapImageLoader = null;
        MainHudSelectedSkillImageLoader? selectedSkillImages = _selectedSkillImages;
        _selectedSkillImages = null;
        MainHudQuickbarImageLoader? quickbarImages = _quickbarImages;
        _quickbarImages = null;
        _texturesReady = false;
        Func<ValueTask>? closeSession = _closeSession;
        WorldFrameIngress? ingress = _ingress;
        WorldFrameEgress? egress = _egress;
        _closeSession = null;
        _ingress = null;
        _egress = null;
        _inbound = null;
        _levelExperience = null;
        _hudContent = null;
        MainHudPreparation? hudPreparation = _hudPreparation;
        _hudPreparation = null;
        _entry = null;
        _scene = null;
        ClientShutdown cleanup = new();
        TaskCompletionSource graphicsReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);
        // Publish the close task before invoking callbacks. The network owners wait for context cleanup.
        _closing = CloseWorldAsync(closeSession, ingress, egress, roleLoad, remoteRolePresentation,
            mapImageLoader, selectedSkillImages, quickbarImages, cleanup, graphicsReleased.Task);
        try
        {
            cleanup.Run(_onClosing);
            cleanup.Run(() => _catalogLoad?.Cancel());
            cleanup.Run(() => _mapLoad?.Cancel());
            cleanup.Run(() => _hudLoad?.Cancel());
            cleanup.Run(() => roleLoad?.Cancel());
            cleanup.Run(() => remoteRolePresentation?.Cancel());
            cleanup.Run(() => mapImageLoader?.Cancel());
            cleanup.Run(() => selectedSkillImages?.Cancel());
            cleanup.Run(() => quickbarImages?.Cancel());
            cleanup.Run(ReleaseGraphics);
            cleanup.Run(() => hudPreparation?.Dispose());
        }
        finally
        {
            graphicsReleased.SetResult();
        }
    }

    /// <summary>
    /// Returns true once, when the background close has finished; rethrows a close failure.
    /// </summary>
    public bool TryObserveClose(out WorldCloseReason reason)
    {
        reason = _closeReason;
        if (_closeObserved || _closing is not { IsCompleted: true } closing)
        {
            return false;
        }

        _closeObserved = true;
        closing.GetAwaiter().GetResult();
        return true;
    }

    /// <summary>Closes the network session and reaps background preparation. Graphics are released separately.</summary>
    public ValueTask DisposeAsync() => new(_disposeTask ??= DisposeCoreAsync());

    private async Task DisposeCoreAsync()
    {
        ClientShutdown cleanup = new();
        if (_closing is { } closing)
        {
            await cleanup.RunAsync(() => new ValueTask(closing)).ConfigureAwait(false);
        }
        WorldRoleLoad? roleLoad = _roleLoad;
        _roleLoad = null;
        cleanup.Run(() => roleLoad?.Cancel());
        WorldRemoteRolePresentation? remoteRolePresentation = _remoteRolePresentation;
        cleanup.Run(() => remoteRolePresentation?.Cancel());
        WorldMapImageLoader? mapImageLoader = _mapImageLoader;
        _mapImageLoader = null;
        cleanup.Run(() => mapImageLoader?.Cancel());
        MainHudSelectedSkillImageLoader? selectedSkillImages = _selectedSkillImages;
        _selectedSkillImages = null;
        cleanup.Run(() => selectedSkillImages?.Cancel());
        MainHudQuickbarImageLoader? quickbarImages = _quickbarImages;
        _quickbarImages = null;
        cleanup.Run(() => quickbarImages?.Cancel());
        WorldContentLoad<WorldEntryCatalogs>? catalogLoad = _catalogLoad;
        _catalogLoad = null;
        cleanup.Run(() => catalogLoad?.Cancel());
        WorldContentLoad<WorldMapScene>? mapLoad = _mapLoad;
        _mapLoad = null;
        cleanup.Run(() => mapLoad?.Cancel());
        WorldContentLoad<MainHudPreparation>? hudLoad = _hudLoad;
        _hudLoad = null;
        cleanup.Run(() => hudLoad?.Cancel());
        WorldFrameIngress? ingress = _ingress;
        WorldFrameEgress? egress = _egress;
        _ingress = null;
        _egress = null;
        _inbound = null;
        _levelExperience = null;
        _hudContent = null;
        MainHudPreparation? hudPreparation = _hudPreparation;
        _hudPreparation = null;
        Func<ValueTask>? closeSession = _closeSession;
        _closeSession = null;
        await cleanup.RunAsync(() => new ValueTask(CloseWorldAsync(closeSession, ingress, egress, roleLoad, remoteRolePresentation,
            mapImageLoader, selectedSkillImages, quickbarImages)))
            .ConfigureAwait(false);

        if (catalogLoad is not null)
        {
            await cleanup.RunAsync(catalogLoad.DisposeAsync).ConfigureAwait(false);
        }

        if (mapLoad is not null)
        {
            await cleanup.RunAsync(mapLoad.DisposeAsync).ConfigureAwait(false);
        }

        if (hudLoad is not null)
        {
            await cleanup.RunAsync(hudLoad.DisposeAsync).ConfigureAwait(false);
        }

        cleanup.Run(() => hudPreparation?.Dispose());
        _entry = null;
        _scene = null;
        cleanup.ThrowIfFailed();
    }

    private static async Task CloseWorldAsync(Func<ValueTask>? closeSession, WorldFrameIngress? ingress,
        WorldFrameEgress? egress, WorldRoleLoad? roleLoad,
        WorldRemoteRolePresentation? remoteRolePresentation, WorldMapImageLoader? mapImageLoader,
        MainHudSelectedSkillImageLoader? selectedSkillImages, MainHudQuickbarImageLoader? quickbarImages,
        ClientShutdown? cleanup = null, Task? graphicsReleased = null)
    {
        if (graphicsReleased is not null)
        {
            await graphicsReleased.ConfigureAwait(false);
        }

        cleanup ??= new ClientShutdown();
        if (egress is not null)
        {
            await cleanup.RunAsync(egress.DisposeAsync).ConfigureAwait(false);
        }

        if (ingress is not null)
        {
            await cleanup.RunAsync(ingress.DisposeAsync).ConfigureAwait(false);
        }

        if (closeSession is not null)
        {
            await cleanup.RunAsync(closeSession).ConfigureAwait(false);
        }

        if (roleLoad is not null)
        {
            await cleanup.RunAsync(roleLoad.DisposeAsync).ConfigureAwait(false);
        }

        if (remoteRolePresentation is not null)
        {
            await cleanup.RunAsync(remoteRolePresentation.DisposeAsync).ConfigureAwait(false);
        }

        if (mapImageLoader is not null)
        {
            await cleanup.RunAsync(mapImageLoader.DisposeAsync).ConfigureAwait(false);
        }

        if (selectedSkillImages is not null)
        {
            await cleanup.RunAsync(selectedSkillImages.DisposeAsync).ConfigureAwait(false);
        }

        if (quickbarImages is not null)
        {
            await cleanup.RunAsync(quickbarImages.DisposeAsync).ConfigureAwait(false);
        }

        cleanup.ThrowIfFailed();
    }

    private void ApplyFrame(ReadOnlyMemory<byte> packet)
    {
        WorldInboundState state = _inbound ?? throw new InvalidOperationException("World inbound state is not active.");
        if (state.Apply(packet) && _scene is { } scene)
        {
            ApplyCamera(scene, state);
        }
    }

    private static void SynchronizeSceneRoles(WorldMapScene scene, WorldInboundState state) =>
        scene.SetRemoteRoles(state.RemoteRoles.Revision, state.RemoteRoles.All.Select(static player =>
            new WorldSceneRole(player.EntityId, player.X, player.Y, player.Direction)));

    private static void ApplyCamera(WorldMapScene scene, WorldInboundState state)
    {
        // Native retains off-grid tiles; projection clamps the camera, not the hero's model coordinates (CP2.4c).
        scene.SetCameraTile(state.X, state.Y);
        scene.SetHeroDirection(state.DirectionIndex);
    }

    private void DrawRemoteRole(OpenGLRenderer renderer, uint entityId, Matrix4x4 main, Matrix4x4 shadow)
    {
        if (_inbound is { } state)
        {
            _remoteRolePresentation?.Draw(renderer, entityId, main, shadow,
                _frameMilliseconds, state.RemoteRoles);
        }
    }

    private void DrawRole(OpenGLRenderer renderer)
    {
        WorldRoleRenderer role = _role ?? throw new InvalidOperationException("World character renderer is unavailable.");
        WorldMapScene scene = _scene ?? throw new InvalidOperationException("World scene is unavailable.");
        (System.Numerics.Matrix4x4 main, System.Numerics.Matrix4x4 shadow) = scene.HeroMatrices;
        role.Draw(renderer, main, shadow, _frameMilliseconds);
    }
}

/// <summary>Why an admitted world closed; the login flow reports the two cases differently.</summary>
internal enum WorldCloseReason
{
    ContentUnavailable,
    ConnectionLost,
}
