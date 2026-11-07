using System.Runtime.ExceptionServices;
using OpenConquer.Client.UI;
using OpenConquer.Client.UI.Login;
using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Client.UI.Role;
using OpenConquer.Client.World;
using OpenConquer.Content;
using OpenConquer.Networking.Game;
using OpenConquer.Networking.Game.Handshake;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Platform.Geometry;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client;

/// <summary>Composes login, role creation and one admitted world. The window hosts this state machine.</summary>
internal sealed class ClientSession : IDisposable
{
    private readonly ClientLogin _login;
    private readonly ClientFrameClock _frameClock;
    private readonly ClientRoleCreation _roles;
    private readonly Action _closeWindow;
    private readonly Func<GameServerHandshakeSession, GameWorldEntrySnapshot, Action, IClientWorld> _createWorld;
    private IClientWorld? _world;
    private Task? _failedHandoff;
    private uint _frameMilliseconds;

    public ClientSession(string contentRoot, IClientContentSource content, LogicalRenderSize size,
        Action requestExit, Action closeWindow, TimeProvider clock)
        : this(content, size, requestExit, closeWindow,
            canLogin => ClientLogin.Create(contentRoot, content, size, canLogin, requestExit, clock), clock: clock)
    {
    }

    internal ClientSession(IClientContentSource content, LogicalRenderSize size, Action requestExit, Action closeWindow,
        Func<Func<bool>, ClientLogin> createLogin,
        Func<GameServerHandshakeSession, string, IRoleCreationScreen>? createRoleScreen = null,
        Func<GameServerHandshakeSession, GameWorldEntrySnapshot, Action, IClientWorld>? createWorld = null, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(requestExit);
        ArgumentNullException.ThrowIfNull(closeWindow);
        ArgumentNullException.ThrowIfNull(createLogin);
        _closeWindow = closeWindow;
        TimeProvider time = clock ?? TimeProvider.System;
        _frameClock = new ClientFrameClock(time);
        _login = createLogin(() => State == ClientState.Login);
        _roles = new ClientRoleCreation(content, _login.PlaySound, requestExit,
            () => _world is null, () => _world is { HasScene: true }, AcceptRoleWorld, ReturnFromRole, createRoleScreen, time);
        // Native hero construction stamps hero+0x2F74 at client start (0x655BAD); the 1037 report window starts there.
        uint clientStartedAt = GameClientClock.Milliseconds(time);
        _createWorld = createWorld ?? ((game, entry, closing) =>
            new ClientWorld(game, entry, content, size, closing, clientStartedAt, _frameMilliseconds, time));
    }

    private IClientWorld ActiveWorld => _world ?? throw new InvalidOperationException("World state has no admitted owner.");

    public ClientState State { get; private set; } = ClientState.Login;
    public void StartServices() => _login.StartServices();

    public void CreateGraphics(OpenGLGraphicsDevice device, ResourceOwnership graphics)
    {
        _login.CreateGraphics(device, graphics);
        _roles.CreateGraphics(device, graphics);
        _roles.BindCaret();
        _login.BindCaret();
    }

    public void ClearGraphicsBindings()
    {
        _roles.ReleaseGraphicsBinding();
        _login.ReleaseGraphicsBinding();
    }

    public void ReleaseGraphics()
    {
        ClientShutdown cleanup = new();
        cleanup.Run(_login.CancelCatalog);
        cleanup.Run(_login.Screen.FocusLost);
        cleanup.Run(ClearGraphicsBindings);
        cleanup.Run(() => _world?.ReleaseGraphics());
        cleanup.ThrowIfFailed();
    }

    internal void Update(TimeSpan elapsed, OpenGLGraphicsDevice? device, uint frameMilliseconds)
    {
        ObjectDisposedException.ThrowIf(State is ClientState.Closing or ClientState.Closed, this);
        _frameMilliseconds = frameMilliseconds;
        _login.Update();
        if (State == ClientState.Login && _login.Attempts.TryTakeAdmittedWorld(
                out GameServerHandshakeSession? game, out GameWorldEntrySnapshot? entry))
        {
            AcceptWorld(game!, entry!);
        }
        _roles.ObserveClose();
        if (_world is { } world && world.TryObserveClose(out WorldCloseReason reason))
        {
            if (reason == WorldCloseReason.ConnectionLost)
            {
                _login.Attempts.FailWorldConnection();
            }
            else
            {
                _login.Attempts.FailWorldContent();
            }
        }
        if (State == ClientState.Login && _roles.CanOpen &&
            _login.Attempts.Status == LoginAttemptStatus.CharacterCreation &&
            _login.Attempts.TryTakeAcceptedGame(out GameServerHandshakeSession? accepted, out string account))
        {
            State = ClientState.RoleCreation;
            if (!_roles.TryOpen(accepted!, account))
            {
                _login.Attempts.FailAcceptedGame();
                State = ClientState.ReturningToLogin;
            }
        }
        _roles.Update(elapsed);
        _world?.DrainIngress(frameMilliseconds);
        _world?.CompleteLoad();
        if (_world is { IsClosed: true })
        {
            _world = null;
        }
        if (State == ClientState.ReturningToLogin && _world is null && !_roles.IsClosing)
        {
            State = ClientState.Login;
        }
        _world?.RefreshAppearance(device);
        _login.ApplyFeedback();
    }

    internal void UpdatePresentation(TimeSpan elapsed)
    {
        _login.UpdateLoading(_world is { IsActive: true }, _world is { IsPresentable: true }, elapsed);
        if (State == ClientState.WorldLoading && _world is { IsPresentable: true } && _login.CanPresentWorld)
        {
            State = ClientState.World;
        }
        _login.SetWorldActive(State == ClientState.World);
    }

    public void RenderFrame(DesktopWindow window, OpenGLGraphicsDevice device, OpenGLRenderer fallback)
    {
        (TimeSpan elapsed, uint frameMilliseconds) = _frameClock.Next();
        Update(elapsed, device, frameMilliseconds);
        bool alreadyPresentingWorld = State == ClientState.World;
        if (!alreadyPresentingWorld)
        {
            _world?.PrepareFirstTextures(window, device, fallback);
        }

        UpdatePresentation(elapsed);
        if (State != ClientState.World)
        {
            _login.Advance(elapsed, fallback);
        }
        IClientWorld? visible = State == ClientState.World ? _world : null;
        OpenGLRenderer renderer = visible?.GetRenderer(window, device, fallback) ?? fallback;
        if (alreadyPresentingWorld && visible is not null)
        {
            visible.PrepareFrame(device, renderer);
            if (!visible.IsActive)
            {
                // A content failure can close the world and release its render target during preparation.
                visible = null;
                renderer = fallback;
            }
        }

        renderer.BeginFrame();
        ExceptionDispatchInfo? firstFailure = null;
        try
        {
            if (visible is not null)
            {
                visible.DrawFrame(renderer, window);
            }
            else if (State == ClientState.RoleCreation)
            {
                _roles.Draw(renderer);
            }
            else
            {
                _login.Draw(renderer);
            }
        }
        catch (Exception failure)
        {
            // End the frame before propagating the first drawing failure.
            firstFailure = ExceptionDispatchInfo.Capture(failure);
        }
        try
        {
            renderer.EndFrame();
        }
        catch (Exception failure)
        {
            // Preserve an earlier drawing failure when frame completion also fails.
            firstFailure ??= ExceptionDispatchInfo.Capture(failure);
        }
        firstFailure?.Throw();
    }

    private void AcceptWorld(GameServerHandshakeSession game, GameWorldEntrySnapshot entry)
    {
        State = ClientState.WorldLoading;
        try
        {
            _world = _createWorld(game, entry, OnWorldClosing) ?? throw new InvalidOperationException("The world factory returned no world.");
        }
        catch
        {
            // A failed factory did not accept ownership; retain its cleanup until shutdown observes it.
            _failedHandoff = CloseFailedHandoffAsync(game);
            OnWorldClosing();
            throw;
        }
    }

    private static async Task CloseFailedHandoffAsync(GameServerHandshakeSession game) =>
        await game.DisposeAsync().ConfigureAwait(false);

    private void AcceptRoleWorld(GameServerHandshakeSession game, GameWorldEntrySnapshot entry)
    {
        _login.StartLoading();
        AcceptWorld(game, entry);
    }

    private void OnWorldClosing()
    {
        _login.ResetLoading();
        if (State is not (ClientState.Closing or ClientState.Closed))
        {
            State = ClientState.ReturningToLogin;
        }
    }

    private void ReturnFromRole(bool connectionLost)
    {
        _login.Attempts.CompleteRoleReturn(connectionLost);
        State = ClientState.ReturningToLogin;
    }

    public void ResizeFramebuffer(PixelSize size) => _world?.ResizeFramebuffer(size);

    public void PointerMoved(PixelPoint point, OpenGLRenderer renderer)
    {
        if (State == ClientState.World)
        {
            ActiveWorld.HandlePointerMoved(point);
            return;
        }
        _login.TrackPointer(point);
        bool mapped = renderer.Viewport.TryMapPointerToLogical(point.X, point.Y, out int x, out int y);
        if (State == ClientState.RoleCreation)
        {
            _roles.PointerMoved(mapped, x, y);
        }
        else
        {
            _login.Screen.PointerMoved(mapped, x, y);
            _login.ContinueCredentialDrag(renderer);
        }
    }

    public void PointerPressed(PixelPoint point, OpenGLRenderer renderer, bool extend)
    {
        if (State == ClientState.World)
        {
            ActiveWorld.HandlePointerPressed(point);
            return;
        }
        _login.TrackPointer(point);
        bool mapped = renderer.Viewport.TryMapPointerToLogical(point.X, point.Y, out int x, out int y);
        if (State == ClientState.RoleCreation)
        {
            _roles.PointerPressed(mapped, x, y, extend);
        }
        else
        {
            _login.Screen.PointerPressed(mapped, x, y, extend);
        }
    }

    public void PointerReleased(PixelPoint point, OpenGLRenderer renderer)
    {
        if (State == ClientState.World)
        {
            ActiveWorld.HandlePointerReleased(point);
            return;
        }
        bool mapped = renderer.Viewport.TryMapPointerToLogical(point.X, point.Y, out int x, out int y);
        if (State == ClientState.RoleCreation)
        {
            _roles.PointerReleased(mapped, x, y);
        }
        else
        {
            _login.Screen.PointerReleased(mapped, x, y);
        }
    }

    public void KeyPressed(DesktopKeyEvent key, Func<string?> readClipboard, Action<string> writeClipboard)
    {
        if (State == ClientState.World)
        {
            return;
        }
        if (State == ClientState.RoleCreation)
        {
            _roles.KeyPressed(key, readClipboard, writeClipboard);
        }
        else
        {
            _login.Screen.KeyPressed(key, readClipboard, writeClipboard);
        }
    }

    public void TextEntered(char character)
    {
        if (State == ClientState.World)
        {
            return;
        }
        if (State == ClientState.RoleCreation)
        {
            _roles.TextEntered(character);
        }
        else
        {
            _login.Screen.TextEntered(character);
        }
    }

    public void SetFocused(bool focused)
    {
        if (!focused)
        {
            _login.Screen.FocusLost();
            _roles.FocusLost();
        }
        _login.SetFocused(focused);
    }

    public void Dispose()
    {
        if (State is ClientState.Closing or ClientState.Closed)
        {
            return;
        }
        State = ClientState.Closing;
        ClientShutdown cleanup = new();
        cleanup.Run(_login.CancelCatalog);
        cleanup.Run(() => _roles.DisposeAsync().AsTask().GetAwaiter().GetResult());
        cleanup.Run(() => _login.Attempts.DisposeAsync().AsTask().GetAwaiter().GetResult());
        cleanup.Run(() => _world?.DisposeAsync().AsTask().GetAwaiter().GetResult());
        cleanup.Run(() => _failedHandoff?.GetAwaiter().GetResult());
        cleanup.Run(() => _login.Registration.DisposeAsync().AsTask().GetAwaiter().GetResult());
        cleanup.Run(_closeWindow);
        cleanup.Run(_login.ReleaseServices);
        cleanup.Run(_roles.ReleaseContent);
        cleanup.Run(_login.Dispose);
        _world = null;
        _failedHandoff = null;
        State = ClientState.Closed;
        cleanup.ThrowIfFailed();
    }
}
