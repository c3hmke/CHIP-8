using System.Runtime.InteropServices;
using static SDL2.SDL;

namespace CHIP_8;

public sealed class AudioEngine
{
    private int _sample, _beeps;

    public AudioEngine(CPU cpu)
    {
        SDL_AudioSpec spec = new()
        {
            channels = 1,
            freq = 44100,
            samples = 256,
            format = AUDIO_S8,
            callback = (_, stream, length) =>
            {
                var data = new sbyte[length];
                for (int i = 0; i < data.Length && cpu.SoundTimer > 0; i++, _beeps++)
                {
                    if (_beeps == 730) { _beeps = 0; cpu.SoundTimer--; }
                    data[i] = (sbyte)(127 * Math.Sin(_sample * Math.PI * 2 * 604.1 / 44100));
                    _sample++;
                }
                Marshal.Copy((byte[])(Array)data, 0, stream, data.Length);
            }
        };
        
        SDL_OpenAudio(ref spec, 0);
        SDL_PauseAudio(0);
    }
}