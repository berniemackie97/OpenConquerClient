using OpenConquer.Content.Media;
namespace OpenConquer.Content.Login;

/// <summary>The sound events named by the retail <c>flashLogin.ini</c>.</summary>
public enum LoginAudioCue
{
    Music,
    Glide,
    ServerSelect,
    Page,
    Dragon,
    ButtonClick,
    ButtonHover,
    Confirm,
    KeyOne,
    KeyTwo,
    KeyThree,
    Error,
    CharacterHover,
    CharacterSelect,
}

/// <summary>A decoded, signed 16-bit PCM clip.</summary>
public sealed class LoginAudioClip
{
    public LoginAudioClip(int sampleRate, int channelCount, byte[] pcm16)
    {
        ArgumentNullException.ThrowIfNull(pcm16);
        if (sampleRate is < 16000 or > 48000 || channelCount is not (1 or 2) || pcm16.Length == 0 || pcm16.Length % (2 * channelCount) != 0)
        {
            throw new ArgumentException("Invalid login audio format.");
        }

        SampleRate = sampleRate;
        ChannelCount = channelCount;
        Pcm16 = pcm16;
    }

    public int SampleRate
    {
        get;
    }

    public int ChannelCount
    {
        get;
    }

    public ReadOnlyMemory<byte> Pcm16
    {
        get;
    }
}

/// <summary>Startup and role sounds, decoded offline from the retail MP3 files to PCM.</summary>
public static class LoginAudioAssets
{
    private readonly record struct Clip(LoginAudioCue Cue, int SampleRate, int ChannelCount, int Length, string Sha256);

    // Ordered by cue; each comment names the retail source file.
    private static readonly Clip[] s_clips =
    [
        new(LoginAudioCue.Music, 44100, 2, 5299200, "9512FCB22755239FA39CE88E95A2A805BD17D5E8A5B2414754382145283A9E67"), // music.mp3
        new(LoginAudioCue.Glide, 22050, 1, 11520, "E93E7BB99D8D2C9B87B696EE8247B0E45DF90D147C964C8095723D0BEB8914E3"), // Glide.mp3
        new(LoginAudioCue.ServerSelect, 22050, 1, 41472, "FDD34965D059BDC27667113E9D98A1A2DAD9908C913F99AC0FF5B3AE28E2CF5E"), // click_2.mp3
        new(LoginAudioCue.Page, 22050, 1, 33408, "BFC6CDD3FAB21AE4EFBD3707E96C1188B17520463999C48921577C4A355E94B3"), // page.mp3
        new(LoginAudioCue.Dragon, 22050, 2, 112896, "D509867F9A57370D2A6837E31322287BD97E88A477E272C0FE23A7FBDA2C59B9"), // Dragon.mp3
        new(LoginAudioCue.ButtonClick, 22050, 1, 10368, "EBFD04942ADD2F4FD32CCE06DC9224E1E7FDBCCAD7D66924321B79F74C6D9D5B"), // Button_1.mp3
        new(LoginAudioCue.ButtonHover, 22050, 1, 9216, "B7D82E29DCF09DE0768B4D3D78CFA9CA3E74B3E3315C0C41A477D43CC7A0A31B"), // Button_2.mp3
        new(LoginAudioCue.Confirm, 22050, 2, 324864, "E09699DA989573E077A9C0AE67D0AA3A70158AC16E439A1FC043B3A68274D04A"), // Confirm.mp3
        new(LoginAudioCue.KeyOne, 22050, 1, 23040, "76E683239293F3B797EE338EAB0CD028247F79081F6EFE65B2BB00BEC413A153"), // key_1.mp3
        new(LoginAudioCue.KeyTwo, 22050, 1, 31104, "B6186B1DA76A320E38E0C6490E251DEB9AF1D1A97BF4752D1D22B02AD5B4A0BB"), // key_2.mp3
        new(LoginAudioCue.KeyThree, 22050, 1, 23040, "141BCC2FA5F83987B5423D259BCF637C9324A85880B78CAD1354C9DFC1B79BAE"), // key_3.mp3
        new(LoginAudioCue.Error, 22050, 1, 41472, "25B43D6597E9F320E6895A0363B5EFCFB9A3911DDFC45600AC66875FC5B9E067"), // error.mp3
        new(LoginAudioCue.CharacterHover, 22050, 1, 20736, "4050100296A5C84B03AA8FC72BB4F015D4135C1091BAF49024B94EA51C952A1B"), // Button_3.mp3
        new(LoginAudioCue.CharacterSelect, 22050, 1, 44928, "C167E81BA7DD21118B6BDCFC2A3CE991D0E5410E5F9A895A8A0987184D08988B"), // click_1.mp3
    ];

    public static LoginAudioClip Load(LoginAudioCue cue)
    {
        if (!Enum.IsDefined(cue))
        {
            throw new ArgumentOutOfRangeException(nameof(cue), cue, "Unknown login sound.");
        }

        Clip clip = s_clips[(int)cue];
        byte[] pcm = EmbeddedMedia.Read($"Login/Audio/{cue}.pcm.br", clip.Length, clip.Sha256, compressed: true);
        return new LoginAudioClip(clip.SampleRate, clip.ChannelCount, pcm);
    }
}
