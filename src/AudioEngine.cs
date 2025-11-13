using System.Runtime.InteropServices;
using static SDL2.SDL;

namespace CHIP_8;

public sealed class AudioEngine
{
    
    private int _sample, _beeps;
    
    private readonly Func<byte>   _getSoundTimer;
    private readonly Action<byte> _setSoundTimer;

    /// This needs to be stored to prevent it from being garbage collected
    private readonly SDL_AudioCallback _callback;

    public AudioEngine(Func<byte> getSoundTimer, Action<byte> setSoundTimer)
    {
        _getSoundTimer = getSoundTimer;
        _setSoundTimer = setSoundTimer;

        _callback = (_, stream, length) =>
        {
            var data = new sbyte[length];
            byte timer = _getSoundTimer();

            for (int i = 0; i < data.Length && timer > 0; i++, _beeps++)
            {
                if (_beeps == 730)
                {
                    _beeps = 0;
                    setSoundTimer((byte)(timer - 1));
                    timer = _getSoundTimer();
                }

                data[i] = (sbyte)(127 * Math.Sin(_sample * Math.PI * 2 * 604.1 / 44100));
                _sample++;
            }

            Marshal.Copy((byte[])(Array)data, 0, stream, data.Length);
        };
        
        SDL_AudioSpec spec = new()
        {
            channels = 1,
            freq     = 44100,
            samples  = 256,
            format   = AUDIO_S8,
            callback = _callback
        };
        
        SDL_OpenAudio(ref spec, 0);
        SDL_PauseAudio(0);
    }
}