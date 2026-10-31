using OpenConquer.Content.Login;
using OpenConquer.Content.Role;

namespace OpenConquer.Client.UI.Role.Audio;

/// <summary>Resolves role.dat sound events using the original selectCharacter.ini.</summary>
internal sealed class RoleAudioFeedback : IRoleSounds
{
    private static readonly Dictionary<string, LoginAudioCue> s_audioByFile = new(StringComparer.OrdinalIgnoreCase)
    {
        ["page.mp3"] = LoginAudioCue.Page,
        ["error.mp3"] = LoginAudioCue.Error,
        ["Button_1.mp3"] = LoginAudioCue.ButtonClick,
        ["Button_2.mp3"] = LoginAudioCue.ButtonHover,
        ["Button_3.mp3"] = LoginAudioCue.CharacterHover,
        ["click_1.mp3"] = LoginAudioCue.CharacterSelect,
        ["Confirm.mp3"] = LoginAudioCue.Confirm,
        ["Glide.mp3"] = LoginAudioCue.Glide,
        ["key_1.mp3"] = LoginAudioCue.KeyOne,
        ["key_2.mp3"] = LoginAudioCue.KeyTwo,
        ["key_3.mp3"] = LoginAudioCue.KeyThree,
    };

    private static readonly (string Set, string Section)[] s_sections =
    [
        ("snd_cs", "classSelection"),
        ("snd_bs", "bodySelection"),
        ("snd_db", "dialogBox"),
    ];

    private readonly Dictionary<(string Set, string Handle), LoginAudioCue> _events = new();
    private readonly Action<LoginAudioCue> _playSound;

    public RoleAudioFeedback(SelectCharacterIni ini, Action<LoginAudioCue> playSound)
    {
        ArgumentNullException.ThrowIfNull(ini);
        _playSound = playSound ?? throw new ArgumentNullException(nameof(playSound));

        foreach ((string set, string section) in s_sections)
        {
            foreach ((string handle, string file) in ini.Section(section))
            {
                if (!s_audioByFile.TryGetValue(file, out LoginAudioCue cue))
                {
                    throw new InvalidDataException($"Unknown role sound '{file}' in [{section}].");
                }

                _events.Add((set, handle), cue);
            }
        }
    }

    public void Play(string set, string handle)
    {
        if (_events.TryGetValue((set, handle), out LoginAudioCue cue))
        {
            _playSound(cue);
        }
    }
}
