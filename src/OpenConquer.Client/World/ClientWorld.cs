using OpenConquer.Client.UI.Hud.Rendering;
using OpenConquer.Client.UI.Hud.Vitals;
using OpenConquer.Content;
using OpenConquer.Content.Items;
using OpenConquer.Content.Progression;
using OpenConquer.Networking.Game;
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
    private readonly IClientContentSource _content;
    private readonly LogicalRenderSize _logicalRenderSize;
    private readonly Action<OpenGLRenderer> _drawRole;
    private readonly Action<ReadOnlyMemory<byte>> _applyFrame;
    private readonly Action _onClosing;

    private Func<ValueTask>? _closeSession;
    private GameWorldEntrySnapshot? _entry;
    private readonly TimeProvider _clock;
    private WorldFrameIngress? _ingress;
    private WorldFrameEgress? _egress;
    private WorldInboundState? _inbound;
    private LevelExperienceTable? _levelExperience;
    private WorldMapLoad? _load;
    private WorldMapScene? _scene;
    private WorldRoleLoad? _roleLoad;
    private WorldRoleRenderer? _role;
    private WorldRoleSources? _roleSources;
    private WorldHudRuntime? _hud;
    private OpenGLRenderer? _renderer;
    private LogicalRenderSize _rendererSize;
    private bool _texturesReady;
    private Task? _closing;
    private Task? _disposeTask;
    private WorldCloseReason _closeReason;
    private bool _closeObserved;

    /// <summary>
    /// Starts the map load for an admitted world. <c>onClosing</c> runs once, synchronously, when the world starts
    /// closing. <c>clientStartedAt</c> is the client millisecond counter at client start (native hero construction).
    /// </summary>
    public ClientWorld(GameServerHandshakeSession game, GameWorldEntrySnapshot entry, IClientContentSource content,
        LogicalRenderSize logicalRenderSize, Action onClosing, uint clientStartedAt = 0, TimeProvider? clock = null)
        : this(entry, content, logicalRenderSize, onClosing,
            (game ?? throw new ArgumentNullException(nameof(game))).DisposeAsync,
            () => new WorldFrameIngress(game), () => new WorldMapLoad(content, entry),
            () => new WorldFrameEgress(
                (now, token) => game.SendWorldLatencyTickAsync(entry.Character.EntityId, now, token),
                (address, latency, now, token) => game.SendWorldLatencyReportAsync(address, latency, now, token)),
            clock, clientStartedAt)
    {
    }

    /// <summary>
    /// Builds a world from its parts. The ingress and map load are created only after the entry hydrates, as for an
    /// admitted session.
    /// </summary>
    internal ClientWorld(GameWorldEntrySnapshot entry, IClientContentSource content, LogicalRenderSize logicalRenderSize,
        Action onClosing, Func<ValueTask> closeSession, Func<WorldFrameIngress> createIngress, Func<WorldMapLoad> createLoad,
        Func<WorldFrameEgress>? createEgress = null, TimeProvider? clock = null, uint clientStartedAt = 0)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(onClosing);
        ArgumentNullException.ThrowIfNull(closeSession);
        ArgumentNullException.ThrowIfNull(createIngress);
        ArgumentNullException.ThrowIfNull(createLoad);

        _content = content;
        _clock = clock ?? TimeProvider.System;
        _logicalRenderSize = logicalRenderSize;
        _onClosing = onClosing;
        _drawRole = DrawRole;
        _applyFrame = ApplyFrame;
        _closeSession = closeSession;
        _entry = entry;

        // Item stats feed the hero's equipment snapshot rules, so they load before any hydration or ingress frame.
        ItemStatCatalog itemStats;
        try
        {
            itemStats = ItemStatCatalog.Load(content);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"OpenConquer: world item tables are unavailable ({exception.Message}).");
            BeginClose(WorldCloseReason.ContentUnavailable);
            return;
        }

        try
        {
            _inbound = new WorldInboundState(entry, itemStats, () => GameClientClock.Milliseconds(_clock),
                clientStartedAt);
        }
        catch (InvalidDataException exception)
        {
            Console.Error.WriteLine($"OpenConquer: world equipment hydration is invalid ({exception.Message}).");
            BeginClose(WorldCloseReason.ContentUnavailable);
            return;
        }

        _ingress = createIngress();
        _egress = createEgress?.Invoke();
        _load = createLoad();
        uint appearance = entry.Character.PackedAppearance;
        _roleLoad = new WorldRoleLoad((sources, token) => WorldRoleAssets.Load(content, appearance, sources, token));
    }

    /// <summary>True until the world starts closing.</summary>
    public bool IsActive => _closing is null && _disposeTask is null;

    /// <summary>The map scene is loaded.</summary>
    public bool HasScene => _scene is not null;

    /// <summary>The scene, its first textures and the initial appearance are ready to draw.</summary>
    public bool IsPresentable => IsActive && _scene is not null && _texturesReady && _roleSources.HasValue;

    /// <summary>The session and appearance worker have closed and the canceled map load has been reaped.</summary>
    public bool IsClosed => _closeObserved && _load is null;

    public void DrainIngress()
    {
        if (_ingress is not { } ingress)
        {
            return;
        }

        try
        {
            ingress.Drain(_applyFrame);
            if (_egress is { } egress && _inbound is { } state)
            {
                egress.Pump(state.Heartbeat, GameClientClock.Milliseconds(_clock));
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or TimeoutException or
            System.Net.Sockets.SocketException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"OpenConquer: world connection closed ({exception.Message}).");
            BeginClose(WorldCloseReason.ConnectionLost);
        }
    }

    /// <summary>
    /// Takes a finished map load. A load canceled by closing is observed here too, so no background task outlives the
    /// world.
    /// </summary>
    public void CompleteLoad()
    {
        if (_load is not { IsCompleted: true } completed)
        {
            return;
        }

        _load = null;
        try
        {
            WorldMapScene scene = completed.Complete();
            if (IsActive)
            {
                _scene = scene;
                _levelExperience = LevelExperienceTable.Load(_content);
                if (_inbound is { } inbound)
                {
                    ApplyCamera(scene, inbound);
                }
            }
        }
        catch (OperationCanceledException)
        {
            BeginClose(WorldCloseReason.ContentUnavailable);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"OpenConquer: unable to load map {_entry?.MapDataId} ({exception.Message}).");
            BeginClose(WorldCloseReason.ContentUnavailable);
        }
        catch
        {
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
            if (!load.TryPrepare(sources, _roleSources, out WorldRoleAssets? assets))
            {
                return;
            }

            OpenGLGraphicsDevice device = graphicsDevice ??
                throw new InvalidOperationException("World graphics device is unavailable.");
            WorldRoleRenderer? next = assets is null ? null : WorldRoleRenderer.Create(device, assets);
            WorldRoleRenderer? previous = _role;
            _role = next;
            _roleSources = sources;
            previous?.Dispose();
            if (next is null && previous is null)
            {
                Console.Error.WriteLine("OpenConquer: character mesh resources are missing from the installed content set.");
            }
        }
        catch (Exception exception) when (WorldRoleLoad.IsContentFailure(exception))
        {
            // A failure preparing the current appearance means required world content is unavailable.
            Console.Error.WriteLine("OpenConquer: world character artwork is unavailable.");
            BeginClose(WorldCloseReason.ContentUnavailable);
        }
    }

    /// <summary>Prepares the first world textures one at a time until the scene can be shown.</summary>
    public void PrepareFirstTextures(WorldMapTextureRenderer? textures, DesktopWindow window, OpenGLGraphicsDevice? graphicsDevice,
        OpenGLRenderer fallback)
    {
        if (_scene is not { } scene || _texturesReady)
        {
            return;
        }

        try
        {
            OpenGLRenderer renderer = GetRenderer(window, graphicsDevice, fallback);
            _texturesReady = scene.Prepare(textures ?? throw new InvalidOperationException("World textures are unavailable."),
                renderer.Viewport.LogicalWidth, renderer.Viewport.LogicalHeight,
                unchecked((uint)Environment.TickCount), textureBudget: 1);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"OpenConquer: world artwork is unavailable ({exception.Message}).");
            BeginClose(WorldCloseReason.ContentUnavailable);
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
        if (_hud is null)
        {
            try
            {
                _hud = new WorldHudRuntime(_content, device, _logicalRenderSize, magicHintSource: null);
            }
            catch
            {
                next.Dispose();
                throw;
            }
        }

        OpenGLRenderer? previous = _renderer;
        _renderer = next;
        _rendererSize = size;
        previous?.Dispose();
        return next;
    }

    public void PrepareFrame(WorldMapTextureRenderer? textures, OpenGLRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _scene?.Prepare(textures ?? throw new InvalidOperationException("World textures are unavailable."),
            renderer.Viewport.LogicalWidth, renderer.Viewport.LogicalHeight,
            unchecked((uint)Environment.TickCount));
    }

    public void DrawFrame(WorldMapTextureRenderer? textures, OpenGLRenderer renderer, DesktopWindow window)
    {
        if (_scene is not { } scene)
        {
            return;
        }

        scene.Draw(textures!, renderer, _role is null ? null : _drawRole);
        if (_hud is { } hud && _inbound is { } state && _levelExperience is { } experienceTable &&
            _entry is { Character.TransformLookSourceId: 0 })
        {
            WorldHeroVitals vitals = state.Hero.AdvanceVitals(experienceTable);
            hud.SetWorldValues(new MainHudVitalsSnapshot(vitals.CurrentLife, vitals.LifeFollower, vitals.MaximumLife,
                vitals.CurrentMana, vitals.ManaFollower, vitals.MaximumMana,
                vitals.Stamina, vitals.MaximumStamina, vitals.HasExtendedStamina),
                vitals.SkillGaugePercent, vitals.Experience, vitals.NextLevelExperience);
        }

        _hud?.Draw(renderer, window);
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
        _hud = null;
        _role = null;
        _roleSources = null;
        _renderer = null;
        ClientShutdown cleanup = new();
        cleanup.Run(() => hud?.Dispose());
        cleanup.Run(() => role?.Dispose());
        cleanup.Run(() => renderer?.Dispose());
        cleanup.ThrowIfFailed();
    }

    /// <summary>
    /// Starts closing: drops the scene and its graphics, cancels the map load and closes the session in the background.
    /// </summary>
    public void BeginClose(WorldCloseReason reason)
    {
        if (_closing is not null || _disposeTask is not null)
        {
            return;
        }

        _onClosing();
        _closeReason = reason;
        _load?.Cancel();
        WorldRoleLoad? roleLoad = _roleLoad;
        _roleLoad = null;
        roleLoad?.Cancel();
        _texturesReady = false;
        Func<ValueTask>? closeSession = _closeSession;
        WorldFrameIngress? ingress = _ingress;
        WorldFrameEgress? egress = _egress;
        _closeSession = null;
        _ingress = null;
        _egress = null;
        _inbound = null;
        _levelExperience = null;
        _entry = null;
        _scene = null;
        try
        {
            ReleaseGraphics();
        }
        finally
        {
            // Network ownership must not be lost when releasing graphics fails.
            _closing = CloseWorldAsync(closeSession, ingress, egress, roleLoad);
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
        roleLoad?.Cancel();
        WorldMapLoad? load = _load;
        _load = null;
        cleanup.Run(() => load?.Dispose());
        WorldFrameIngress? ingress = _ingress;
        WorldFrameEgress? egress = _egress;
        _ingress = null;
        _egress = null;
        _inbound = null;
        _levelExperience = null;
        Func<ValueTask>? closeSession = _closeSession;
        _closeSession = null;
        await cleanup.RunAsync(() => new ValueTask(CloseWorldAsync(closeSession, ingress, egress, roleLoad)))
            .ConfigureAwait(false);

        _entry = null;
        _scene = null;
        cleanup.ThrowIfFailed();
    }

    private static async Task CloseWorldAsync(Func<ValueTask>? closeSession, WorldFrameIngress? ingress,
        WorldFrameEgress? egress, WorldRoleLoad? roleLoad)
    {
        ClientShutdown cleanup = new();
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

    private static void ApplyCamera(WorldMapScene scene, WorldInboundState state)
    {
        // Native retains off-grid tiles; projection clamps the camera, not the hero's model coordinates (CP2.4c).
        scene.SetCameraTile(state.X, state.Y);
        scene.SetHeroDirection(state.DirectionIndex);
    }

    private void DrawRole(OpenGLRenderer renderer)
    {
        WorldRoleRenderer role = _role ?? throw new InvalidOperationException("World character renderer is unavailable.");
        WorldMapScene scene = _scene ?? throw new InvalidOperationException("World scene is unavailable.");
        (System.Numerics.Matrix4x4 main, System.Numerics.Matrix4x4 shadow) = scene.HeroMatrices;
        role.Draw(renderer, main, shadow, unchecked((uint)Environment.TickCount64));
    }
}

/// <summary>Why an admitted world closed; the login flow reports the two cases differently.</summary>
internal enum WorldCloseReason
{
    ContentUnavailable,
    ConnectionLost,
}
