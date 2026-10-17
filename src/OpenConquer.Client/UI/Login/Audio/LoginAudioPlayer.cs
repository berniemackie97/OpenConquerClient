using OpenConquer.Content.Login;
using Silk.NET.OpenAL;

namespace OpenConquer.Client.UI.Login.Audio;

/// <summary>One-device, event-thread-owned startup sound bank. No decoding, allocations, or I/O during playback.</summary>
internal sealed unsafe class LoginAudioPlayer : IDisposable
{
    private const int MaximumEffectVoices = 6;
    private readonly ALContext _alc;
    private readonly AL _al;
    private readonly Device* _device;
    private readonly Context* _context;
    private readonly uint[] _buffers = new uint[Enum.GetValues<LoginAudioCue>().Length];
    private readonly uint[] _effects = new uint[MaximumEffectVoices];
    private readonly uint _music;
    private int _nextVoice;
    private bool _disposed;

    private LoginAudioPlayer()
    {
        try
        {
            _alc = ALContext.GetApi(soft: true);
            _al = AL.GetApi(soft: true);
            _device = _alc.OpenDevice(null!);
            if (_device == null)
            {
                throw new InvalidOperationException("No OpenAL output device is available.");
            }
            _context = _alc.CreateContext(_device, null);
            if (_context == null)
            {
                throw new InvalidOperationException("Unable to create the OpenAL output context.");
            }
            if (!_alc.MakeContextCurrent(_context))
            {
                throw new InvalidOperationException("Unable to make the OpenAL context current.");
            }

            foreach (LoginAudioCue cue in Enum.GetValues<LoginAudioCue>())
            {
                LoginAudioClip clip = LoginAudioAssets.Load(cue);
                uint buffer = _al.GenBuffer();
                _buffers[(int)cue] = buffer;
                fixed (byte* samples = clip.Pcm16.Span)
                {
                    _al.BufferData(buffer, clip.ChannelCount == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16,
                        samples, clip.Pcm16.Length, clip.SampleRate);
                }
            }

            if (_al.GetError() != AudioError.NoError)
            {
                throw new InvalidOperationException("OpenAL rejected a decoded startup audio buffer.");
            }

            _music = _al.GenSource();
            _al.SetSourceProperty(_music, SourceInteger.Buffer, _buffers[(int)LoginAudioCue.Music]);
            _al.SetSourceProperty(_music, SourceBoolean.Looping, true);
            _al.SetSourceProperty(_music, SourceFloat.Gain, 0.4f); // flashLogin.ini [bgMusic] volume=40
            foreach (ref uint voice in _effects.AsSpan())
            {
                voice = _al.GenSource();
                _al.SetSourceProperty(voice, SourceFloat.Gain, 1f); // flashLogin.ini [sound] volume=100
            }
            _al.SourcePlay(_music);
            if (_al.GetError() != AudioError.NoError)
            {
                throw new InvalidOperationException("OpenAL could not start the startup music source.");
            }
        }
        catch
        {
            try
            {
                Dispose();
            }
            catch
            {
                // Preserve the initialization failure.
            }
            throw;
        }
    }

    public static LoginAudioPlayer? TryCreate()
    {
        try
        {
            return new LoginAudioPlayer();
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or
            BadImageFormatException or InvalidOperationException or TypeInitializationException)
        {
            Console.Error.WriteLine($"OpenConquer: startup audio unavailable ({exception.GetType().Name}); continuing silently.");
            return null;
        }
    }

    public void Play(LoginAudioCue? cue)
    {
        if (cue is null)
        {
            return;
        }
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (cue == LoginAudioCue.Music || (uint)cue.Value >= (uint)_buffers.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(cue));
        }
        uint source = _effects[_nextVoice];
        _nextVoice = (_nextVoice + 1) % _effects.Length;
        _al.SourceStop(source);
        _al.SetSourceProperty(source, SourceInteger.Buffer, _buffers[(int)cue.Value]);
        _al.SourcePlay(source);
    }

    public void SetFocused(bool focused)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (focused)
        {
            _al.SourcePlay(_music);
        }
        else
        {
            _al.SourcePause(_music);
            foreach (uint source in _effects)
            {
                _al.SourceStop(source);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        if (_context != null && _alc is not null && _al is not null)
        {
            _alc.MakeContextCurrent(_context);
            foreach (uint source in _effects)
            {
                if (source != 0)
                {
                    _al.SourceStop(source);
                    _al.DeleteSource(source);
                }
            }
            if (_music != 0)
            {
                _al.SourceStop(_music);
                _al.DeleteSource(_music);
            }
            foreach (uint buffer in _buffers)
            {
                if (buffer != 0)
                {
                    _al.DeleteBuffer(buffer);
                }
            }
            _alc.MakeContextCurrent(null);
            _alc.DestroyContext(_context);
        }
        if (_device != null && _alc is not null)
        {
            _alc.CloseDevice(_device);
        }
        _al?.Dispose();
        _alc?.Dispose();
    }
}
