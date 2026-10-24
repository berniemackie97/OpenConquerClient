using OpenConquer.Client.UI.Login.Account;
using OpenConquer.Client.UI.Login.Controls;
using OpenConquer.Client.UI.Login.Credentials;
using OpenConquer.Client.UI.Login.Registration;
using OpenConquer.Client.UI.Login.Selection;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Content.Login;
using OpenConquer.Rendering.OpenGL;

namespace OpenConquer.Client.UI.Login;

/// <summary>
/// Every OpenGL resource of the login screen for one context. Draws the screen in the same order as the retail
/// <c>start.dat</c> root display list.
/// </summary>
internal sealed class LoginScreenRenderer : IDisposable
{
    private readonly ResourceOwnership _ownership = new();
    private readonly LoginStagePresentation _stage;
    private readonly LoginBirdPresentation _bird;
    private readonly LoginServerPresentation _servers;
    private readonly LoginSelectionLabels _labels;
    private readonly LoginDirectoryStatusPresentation _directoryStatus;
    private readonly LoginErrorPresentation _errors;
    private readonly LoginRegionPresentation _regions;
    private readonly LoginCredentialPresentation _credentials;
    private readonly LoginButtonPresentation _buttons;
    private readonly LoginAccountPresentation _account;
    private readonly LoginRegistrationPresentation _registration;
    private readonly LoginLogoPresentation _logo;
    private readonly LoginVersionPresentation _version;
    private readonly LoginOpeningPresentation _opening;

    public LoginScreenRenderer(OpenGLGraphicsDevice graphicsDevice, LoginScreen screen)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(screen);

        LoginStageLayout layout = screen.Layout;
        try
        {
            _stage = _ownership.Track(new LoginStagePresentation(graphicsDevice, layout));
            _bird = _ownership.Track(new LoginBirdPresentation(graphicsDevice, layout));
            _servers = _ownership.Track(new LoginServerPresentation(graphicsDevice, layout));
            _labels = _ownership.Track(new LoginSelectionLabels(graphicsDevice, layout));
            _directoryStatus = _ownership.Track(new LoginDirectoryStatusPresentation(graphicsDevice, layout));
            _errors = _ownership.Track(new LoginErrorPresentation(graphicsDevice, layout));
            _regions = _ownership.Track(new LoginRegionPresentation(graphicsDevice, layout));
            _credentials = _ownership.Track(new LoginCredentialPresentation(graphicsDevice, layout));
            _buttons = _ownership.Track(new LoginButtonPresentation(graphicsDevice));
            _account = _ownership.Track(new LoginAccountPresentation(graphicsDevice, screen.AccountLayout));
            _registration = _ownership.Track(new LoginRegistrationPresentation(graphicsDevice, screen.Registration.Layout));
            _logo = _ownership.Track(new LoginLogoPresentation(graphicsDevice, layout));
            _version = _ownership.Track(new LoginVersionPresentation(graphicsDevice, layout, screen.VersionLabel));
            _opening = _ownership.Track(new LoginOpeningPresentation(graphicsDevice, layout));
        }
        catch
        {
            _ownership.DisposeAfterInitializationFailure();
            throw;
        }
    }

    /// <summary>Glyph measurements of the credential fields, for caret placement.</summary>
    public ILoginCaretLocator CaretLocator => _credentials;

    /// <summary>Glyph measurements of the Sign Up fields, for caret placement.</summary>
    public ILoginRegistrationCaretLocator RegistrationCaretLocator => _registration;

    public void Draw(OpenGLRenderer renderer, LoginScreen screen, LoginDirectoryAvailability directoryAvailability)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(screen);

        LoginServerSelection selection = screen.Selection;
        LoginSelectionAnimation animation = screen.SelectionAnimation;

        // Depths 1 and 2: backdrop and bird.
        _stage.DrawBackdrop(renderer);
        _bird.Draw(renderer, screen.Clock.Frame);

        // Depth 4: server tiles, their effects, then their names.
        _servers.Draw(renderer, selection, animation);
        _labels.DrawServers(renderer, selection);
        _directoryStatus.Draw(renderer, directoryAvailability);

        // Depth 138: region panel.
        _regions.Draw(renderer, selection, animation);
        _labels.DrawRegions(renderer, selection);
        if (screen.SelectionInput.PagesVisible)
        {
            _regions.DrawPageButtons(renderer, screen.SelectionInput, _buttons);
            _labels.DrawRegionPage(renderer, selection);
        }

        // Depth179 errorMC and depth205 quitWindow, beneath the border and account panel.
        _errors.Draw(renderer, screen.Error);
        _errors.Draw(renderer, screen.Quit);

        // Depths 216 and 221: border ornament, then enterMC.
        _stage.DrawBorder(renderer);
        _stage.DrawAccountPanel(renderer);
        _credentials.Draw(renderer, screen.Credentials);
        foreach (LoginButton button in LoginControlLayout.Buttons)
        {
            _buttons.Draw(renderer, button, screen.Buttons.StateOf(button), screen.Buttons.BoundsOf(button));
        }

        _account.Draw(renderer, screen.Account);

        // The OpenConquer Sign Up dialog takes the account dialogs' place in enterMC.
        _registration.Draw(renderer, screen.Registration);

        // Depths 274 and 276: logo and version, shown from root frame 3 once the opening has finished.
        if (!screen.IsOpeningPlaying)
        {
            _logo.Draw(renderer, screen.LogoFrame);
            _version.Draw(renderer);
        }

        // Root depth294: quitWindow2, used while loading.
        _errors.Draw(renderer, screen.LoadingQuit);
        _opening.Draw(renderer, screen.Clock.Frame);
    }

    public void Dispose() => _ownership.Dispose();
}
