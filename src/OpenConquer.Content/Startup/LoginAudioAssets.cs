using System.IO.Compression;

namespace OpenConquer.Content.Startup;

/// <summary>Closed set of retail 5517 flashLogin.ini audio events.</summary>
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
}

/// <summary>Validated, decoded signed PCM samples; conversion occurs offline, not on the render thread.</summary>
public sealed class LoginAudioClip
{
    public LoginAudioClip(int sampleRate, int channelCount, byte[] pcm16)
    {
        ArgumentNullException.ThrowIfNull(pcm16);
        if (sampleRate is < 16000 or > 48000 || channelCount is not (1 or 2) || pcm16.Length == 0 || pcm16.Length % (2 * channelCount) != 0)
        {
            throw new ArgumentException("Invalid embedded startup audio format.");
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

/// <summary>Loads only curated, bounded source-derived retail audio, never catalog-controlled paths.</summary>
public static class LoginAudioAssets
{
    private const string ResourcePrefix = "OpenConquer.Content.Assets.Login.Audio.";
    public static LoginAudioClip Load(LoginAudioCue cue)
    {
        (string filename, int rate, int channels, int bytes) = cue switch
        {
            LoginAudioCue.Music => ("music.pcm.br", 44100, 2, 5299200),
            LoginAudioCue.Glide => ("Glide.pcm.br", 22050, 1, 11520),
            LoginAudioCue.ServerSelect => ("click_2.pcm.br", 22050, 1, 41472),
            LoginAudioCue.Page => ("page.pcm.br", 22050, 1, 33408),
            LoginAudioCue.Dragon => ("Dragon.pcm.br", 22050, 2, 112896),
            LoginAudioCue.ButtonClick => ("Button_1.pcm.br", 22050, 1, 10368),
            LoginAudioCue.ButtonHover => ("Button_2.pcm.br", 22050, 1, 9216),
            LoginAudioCue.Confirm => ("Confirm.pcm.br", 22050, 2, 324864),
            LoginAudioCue.KeyOne => ("key_1.pcm.br", 22050, 1, 23040),
            LoginAudioCue.KeyTwo => ("key_2.pcm.br", 22050, 1, 31104),
            LoginAudioCue.KeyThree => ("key_3.pcm.br", 22050, 1, 23040),
            LoginAudioCue.Error => ("error.pcm.br", 22050, 1, 41472),
            _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, "Unknown retail sound event."),
        };

        using Stream input = typeof(LoginAudioAssets).Assembly.GetManifestResourceStream(ResourcePrefix + filename)
            ?? throw new InvalidDataException($"Embedded retail audio resource {filename} is missing.");
        using BrotliStream decoder = new(input, CompressionMode.Decompress);
        byte[] pcm = new byte[bytes];
        try
        {
            decoder.ReadExactly(pcm);
        }
        catch (EndOfStreamException error)
        {
            throw new InvalidDataException($"Embedded retail audio resource {filename} is truncated.", error);
        }
        if (decoder.ReadByte() != -1)
        {
            throw new InvalidDataException($"Embedded retail audio resource {filename} contains trailing decoded bytes.");
        }
        return new LoginAudioClip(rate, channels, pcm);
    }
}
