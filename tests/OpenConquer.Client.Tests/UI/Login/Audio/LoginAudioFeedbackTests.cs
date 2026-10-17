using OpenConquer.Client.UI.Login.Audio;
using OpenConquer.Client.UI.Login.Selection;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Login;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login.Audio;

public sealed class LoginAudioFeedbackTests
{
    private static readonly LoginStageLayout s_layout = LoginStageLayout.Create(new LogicalRenderSize(1024, 768));

    [Fact]
    public void RegionAndServerTilesGlideOncePerEnteredTile()
    {
        LoginAudioFeedback sounds = new(s_layout);
        LoginServerSelection selection = SelectionWithOneServer();

        Assert.Equal(LoginAudioCue.Glide, sounds.PointerMoved(true, 60, 200, selection));
        Assert.Null(sounds.PointerMoved(true, 65, 205, selection));
        Assert.Null(sounds.PointerMoved(true, 450, 225, selection));

        Assert.True(selection.SelectRegionSlot(0));
        Assert.Equal(LoginAudioCue.Glide, sounds.PointerMoved(true, 450, 225, selection));
        Assert.Null(sounds.PointerMoved(true, 450, 230, selection));
        Assert.Null(sounds.PointerMoved(false, 450, 230, selection));
        Assert.Equal(LoginAudioCue.Glide, sounds.PointerMoved(true, 450, 230, selection));
    }

    [Fact]
    public void SelectedRegionDoesNotRollOver()
    {
        LoginAudioFeedback sounds = new(s_layout);
        LoginServerSelection selection = SelectionWithOneServer();
        Assert.True(selection.SelectRegionSlot(0));

        Assert.Null(sounds.PointerMoved(true, 60, 200, selection));
    }

    [Fact]
    public void CredentialFieldsUseButtonRolloverWithoutASelection()
    {
        LoginAudioFeedback sounds = new(s_layout);
        LoginServerSelection selection = new();

        Assert.Equal(LoginAudioCue.ButtonHover, sounds.PointerMoved(true, 480, 650, selection));
        Assert.Null(sounds.PointerMoved(true, 485, 652, selection));
        Assert.Equal(LoginAudioCue.ButtonHover, sounds.PointerMoved(true, 475, 681, selection));
        Assert.Equal(LoginAudioCue.ButtonHover, sounds.PointerMoved(true, 475, 663, selection));
    }

    [Fact]
    public void ResetLetsTheCurrentAreaSoundAgain()
    {
        LoginAudioFeedback sounds = new(s_layout);
        LoginServerSelection selection = new();

        Assert.Equal(LoginAudioCue.ButtonHover, sounds.PointerMoved(true, 480, 650, selection));
        sounds.Reset();
        Assert.Equal(LoginAudioCue.ButtonHover, sounds.PointerMoved(true, 480, 650, selection));
    }

    [Fact]
    public void TypingCyclesThroughTheThreeKeySounds()
    {
        LoginAudioFeedback sounds = new(s_layout);

        Assert.Equal(LoginAudioCue.KeyOne, sounds.NextKeySound());
        Assert.Equal(LoginAudioCue.KeyTwo, sounds.NextKeySound());
        Assert.Equal(LoginAudioCue.KeyThree, sounds.NextKeySound());
        Assert.Equal(LoginAudioCue.KeyOne, sounds.NextKeySound());
        sounds.Reset();
        Assert.Equal(LoginAudioCue.KeyTwo, sounds.NextKeySound());
    }

    private static LoginServerSelection SelectionWithOneServer()
    {
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(new LoginServerCatalog(
            [new LoginRegion("local", "Local Development", [new LoginServer("local-realm", "OpenConquer Local", "LocalRealm")])]));
        return selection;
    }
}
