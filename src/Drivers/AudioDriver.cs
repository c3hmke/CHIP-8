using System.Runtime.InteropServices;
using CHIP_8.Machines;
using static SDL2.SDL;

namespace CHIP_8.Drivers;

public sealed class AudioDriver
{
    private int _sample;
    
    // This is assigned here to prevent it from being garbage collected.
    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    private readonly SDL_AudioCallback _callback;
    
    public AudioDriver(IVirtualMachine vm)
    {
        _callback = (_, stream, length) =>
        {
            var buffer = new sbyte[length];

            if (vm.isAudioActive)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = (sbyte)(127 * Math.Sin(_sample * Math.PI * 2 * 604.1 / 44100)); // sin waveform
                    _sample++;
                }
            }
            else { Array.Clear(buffer); }
            
            Marshal.Copy((byte[])(Array)buffer, 0, stream, buffer.Length);
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