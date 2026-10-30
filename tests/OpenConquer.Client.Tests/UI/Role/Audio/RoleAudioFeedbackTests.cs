using System.Text;
using OpenConquer.Client.UI.Role.Audio;
using OpenConquer.Content.Login;
using OpenConquer.Content.Role;

namespace OpenConquer.Client.Tests.UI.Role.Audio;

public sealed class RoleAudioFeedbackTests
{
    [Fact]
    public void OriginalRetailIniRoutesEveryDeclaredSoundToItsOwnPcmAsset()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "RetailFixture");
        SelectCharacterIni ini = SelectCharacterIni.Load(new ClientContentRoot(root));
        List<LoginAudioCue> played = [];
        RoleAudioFeedback sounds = new(ini, played.Add);

        (string Section, string Set)[] sections =
        [
            ("classSelection", "snd_cs"),
            ("bodySelection", "snd_bs"),
            ("dialogBox", "snd_db"),
        ];

        int total = 0;
        foreach ((string section, string set) in sections)
        {
            foreach ((string key, string filename) in ini.Section(section))
            {
                sounds.Play(set, key);
                LoginAudioCue cue = Assert.Single(played);
                Assert.Equal(ExpectedCue(filename), cue);
                played.Clear();
                total++;
            }
        }

        Assert.True(total > 0);
        sounds.Play("snd_bs", "does_not_exist");
        Assert.Empty(played);
    }

    [Theory]
    [InlineData("Button_3.mp3", LoginAudioCue.CharacterHover)]
    [InlineData("click_1.mp3", LoginAudioCue.CharacterSelect)]
    public void DistinctCharacterEventsPreserveTheirRetailIdentity(string file, LoginAudioCue expected)
    {
        SelectCharacterIni ini = SelectCharacterIni.Parse(Encoding.ASCII.GetBytes(
            "[classSelection]\r\nrollOver_char=" + file + "\r\n"));
        List<LoginAudioCue> played = [];
        RoleAudioFeedback sounds = new(ini, played.Add);

        sounds.Play("snd_cs", "rollOver_char");
        Assert.Equal(expected, Assert.Single(played));
    }

    [Fact]
    public void UnknownSoundFilesFailInsteadOfSubstitutingAnotherClip()
    {
        SelectCharacterIni ini = SelectCharacterIni.Parse(
            "[classSelection]\r\nclick_char=not-a-retail-sound.mp3\r\n"u8);
        Assert.Throws<InvalidDataException>(() => new RoleAudioFeedback(ini, _ => { }));
    }

    private static LoginAudioCue ExpectedCue(string filename) => filename.ToLowerInvariant() switch
    {
        "page.mp3" => LoginAudioCue.Page,
        "error.mp3" => LoginAudioCue.Error,
        "button_1.mp3" => LoginAudioCue.ButtonClick,
        "button_2.mp3" => LoginAudioCue.ButtonHover,
        "button_3.mp3" => LoginAudioCue.CharacterHover,
        "click_1.mp3" => LoginAudioCue.CharacterSelect,
        "confirm.mp3" => LoginAudioCue.Confirm,
        "glide.mp3" => LoginAudioCue.Glide,
        "key_1.mp3" => LoginAudioCue.KeyOne,
        "key_2.mp3" => LoginAudioCue.KeyTwo,
        "key_3.mp3" => LoginAudioCue.KeyThree,
        _ => throw new InvalidDataException($"Unexpected retail role sound file '{filename}'."),
    };
}
