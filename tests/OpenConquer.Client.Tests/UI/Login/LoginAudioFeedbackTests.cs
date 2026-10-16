using OpenConquer.Client.UI.Login;
using OpenConquer.Content.Startup;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginAudioFeedbackTests
{
    [Fact]
    public void RolloverProducesOneEffectPerOccupiedInteractiveCell()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));
        LoginAudioFeedback sounds = new(layout);
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(new LoginServerCatalog([new LoginRegion("local", "Local Development", [new LoginServer("local-realm", "OpenConquer Local", "LocalRealm")])]));
        Assert.Equal(LoginAudioCue.Glide, sounds.PointerMoved(true, 60, 200, selection));
        Assert.Null(sounds.PointerMoved(true, 65, 205, selection));
        Assert.Null(sounds.PointerMoved(true, 450, 225, selection)); // no selected region
        Assert.True(selection.SelectRegionSlot(0));
        Assert.Equal(LoginAudioCue.Glide, sounds.PointerMoved(true, 450, 225, selection));
        Assert.Null(sounds.PointerMoved(true, 450, 230, selection));
        Assert.Null(sounds.PointerMoved(false, 450, 230, selection));
        Assert.Equal(LoginAudioCue.Glide, sounds.PointerMoved(true, 450, 230, selection));
    }

    [Fact]
    public void DisabledEnterCannotEmitAudioButUnselectedCredentialFieldsCan()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));
        LoginAudioFeedback sounds = new(layout);
        LoginServerSelection selection = new();
        Assert.Null(sounds.PointerMoved(true, 660, 730, selection));
        Assert.Equal(LoginAudioCue.ButtonHover, sounds.PointerMoved(true, 480, 650, selection));
        Assert.Equal(LoginAudioCue.ButtonHover, sounds.PointerMoved(true, 870, 733, selection));
        Assert.Null(sounds.PointerMoved(true, 875, 735, selection));
        Assert.Null(sounds.PointerMoved(false, 870, 733, selection));
    }

    [Fact]
    public void PageAndCredentialRolloversAreConditionalOnCurrentSelection()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));
        LoginAudioFeedback sounds = new(layout);
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(new LoginServerCatalog(Enumerable.Range(0, 17)
            .Select(i => new LoginRegion($"region{i}", $"Region {i}", [new LoginServer($"s{i}", $"Server {i}", $"Protocol{i}")]))));
        Assert.Equal(LoginAudioCue.ButtonHover, sounds.PointerMoved(true, 26, 575, selection));
        Assert.Null(sounds.PointerMoved(true, 26, 200, selection)); // previous page unavailable
        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(0));
        Assert.Equal(LoginAudioCue.ButtonHover, sounds.PointerMoved(true, 475, 663, selection));
        Assert.Equal(LoginAudioCue.ButtonHover, sounds.PointerMoved(true, 475, 681, selection));
    }

    [Fact]
    public void KeyboardEffectsCycleThroughOriginalThreeFilesWithoutRng()
    {
        LoginAudioFeedback sounds = new(LoginVisualLayout.Create(new LogicalRenderSize(1024, 768)));
        Assert.Equal(LoginAudioCue.KeyOne, sounds.AcceptedCredentialEdit());
        Assert.Equal(LoginAudioCue.KeyTwo, sounds.AcceptedCredentialEdit());
        Assert.Equal(LoginAudioCue.KeyThree, sounds.AcceptedCredentialEdit());
        Assert.Equal(LoginAudioCue.KeyOne, sounds.AcceptedCredentialEdit());
        sounds.Reset();
        Assert.Equal(LoginAudioCue.KeyTwo, sounds.AcceptedCredentialEdit());
    }
}
