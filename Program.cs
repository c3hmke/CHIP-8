using System.Diagnostics;
using SDL2;
using System.Runtime.InteropServices;

namespace CHIP_8;

public static class Program
{
    static void Main(string[] args)
    {
        if (SDL.SDL_Init(SDL.SDL_INIT_EVERYTHING) < 0) throw new Exception("SDL init failed");

        CHIP8 chip8 = new();
        nint window = SDL.SDL_CreateWindow("CHIP-8", 128, 128, 64 * 8, 32 * 8, 0);
        nint renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);

        using (BinaryReader reader = new(new FileStream("../../../ROMs/BRIX", FileMode.Open)))
        {
            List<byte> program = [];
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                program.Add(reader.ReadByte());
            }

            chip8.LoadProgram(program.ToArray());
        }

        int sample = 0, beeps = 0;
        SDL.SDL_AudioSpec audioSpec = new()
        {
            channels = 1,
            freq     = 44100,
            samples  = 256,
            format   = SDL.AUDIO_S8,
            callback = new SDL.SDL_AudioCallback((_, stream, length) =>
            {
                sbyte[] waveData = new sbyte[length];
                for (int i = 0; i < waveData.Length && chip8.SoundTimer > 0; i++, beeps++)
                {
                    if (beeps == 730)
                    {
                        beeps = 0;
                        chip8.SoundTimer--;
                    }

                    waveData[i] = (sbyte)(127 * Math.Sin(sample * Math.PI * 2 * 604.1 / 44100));
                    sample++;
                }

                byte[] byteData = (byte[])(Array)waveData;
                Marshal.Copy(byteData, 0, stream, byteData.Length);
            })
        };

        SDL.SDL_OpenAudio(ref audioSpec, 0);
        SDL.SDL_PauseAudio(0);

        nint SDLSurface, SDLTexture = 0;
        var frameTimer = Stopwatch.StartNew();
        var ticks60hz = (int)(Stopwatch.Frequency * 0.016);

        SDL.SDL_Event evt;
        bool running = true;
        while (running)
        {
            if (!chip8.WaitingForKeyPress) chip8.Step();

            if (frameTimer.ElapsedTicks > ticks60hz)
            {
                while (SDL.SDL_PollEvent(out SDL.SDL_Event e) != 0)
                {
                    int key = keycodeToIndex(e.key.keysym.sym);
                    switch (e.type)
                    {
                        case SDL.SDL_EventType.SDL_QUIT: running = false; break;
                        case SDL.SDL_EventType.SDL_KEYDOWN:
                            chip8.Keyboard |= (ushort)(1 << key);
                            if (chip8.WaitingForKeyPress) chip8.KeyPressed((byte)key);
                            break;
                        case SDL.SDL_EventType.SDL_KEYUP:
                            chip8.Keyboard &= (ushort)~(1 << key);
                            break;
                    }
                }

                var displayHandle = GCHandle.Alloc(chip8.Display, GCHandleType.Pinned);
                if (SDLTexture != IntPtr.Zero) SDL.SDL_DestroyTexture(SDLTexture);

                SDLSurface = SDL.SDL_CreateRGBSurfaceFrom(
                    pixels: displayHandle.AddrOfPinnedObject(),
                    width:  64,         // screen width in px
                    height: 32,         // screen height in px
                    depth:  32,         // bits per pixel
                    pitch:  64 * 4,     // actual width?
                    Rmask:  0x000000FF, // Red channel
                    Gmask:  0x0000FF00, // Blue channel
                    Bmask:  0x00FF0000, // Green channel
                    Amask:  0xFF000000  // Alpha channel
                );
                SDLTexture = SDL.SDL_CreateTextureFromSurface(renderer, SDLSurface);

                displayHandle.Free();

                SDL.SDL_RenderClear(renderer);
                SDL.SDL_RenderCopy(renderer, SDLTexture, IntPtr.Zero, IntPtr.Zero);
                SDL.SDL_RenderPresent(renderer);

                frameTimer.Restart();
            }

            Thread.Sleep(2);
        }
        
        SDL.SDL_DestroyRenderer(renderer);
        SDL.SDL_DestroyWindow(window);
    }

    private static int keycodeToIndex(SDL.SDL_Keycode keycode)
    {
        var key  = (int)keycode;                    // ascii int value for the key pressed
        return (key < 58) ? key - 48 : key - 87;    // index for that key
    }
}