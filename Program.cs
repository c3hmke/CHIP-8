using System.Diagnostics;
using System.Runtime.InteropServices;
using static SDL2.SDL;

namespace CHIP_8;

public static class Program
{
    static void Main(string[] args)
    {
        if (SDL_Init(SDL_INIT_EVERYTHING) < 0) throw new Exception("SDL init failed");

        CHIP8 chip8 = new();
        
        /// Configure graphics
        nint window = SDL_CreateWindow("CHIP-8", 128, 128, 64 * 8, 32 * 8, 0);
        nint renderer = SDL_CreateRenderer(window, -1, SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
        
        nint SDLTexture = SDL_CreateTexture(
            renderer, SDL_PIXELFORMAT_RGBA8888,
            (int) SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
            64, 32);

        /// Configure audio
        int sample = 0, beeps = 0;
        SDL_AudioSpec audioSpec = new()
        {
            channels = 1,
            freq     = 44100,
            samples  = 256,
            format   = AUDIO_S8,
            callback = (_, stream, length) =>
            {
                var waveData = new sbyte[length];
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
                Marshal.Copy((byte[])(Array)waveData, 0, stream, waveData.Length);
            }
        };

        SDL_OpenAudio(ref audioSpec, 0);
        SDL_PauseAudio(0);
        
        /// Confifure program timers
        var  frameTimer  = Stopwatch.StartNew();         // Timer for display out
        long frameTicks  = Stopwatch.Frequency / 60;     // running at 60Hz
        var  cpuTimer    = Stopwatch.StartNew();         // Timer for the CPU clock
        long cpuTicks    = Stopwatch.Frequency / 700;    // running at 700Hz
        long accumulator = 0;                            // Used to keep display synced
        
        /// Read the program into memory
        using (BinaryReader reader = new(new FileStream("../../../ROMs/BRIX", FileMode.Open)))
        {
            List<byte> program = [];
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                program.Add(reader.ReadByte());
            }

            chip8.LoadProgram(program.ToArray());
        }
        
        var running = true;
        while (running)
        {
            if (!chip8.WaitingForKeyPress && cpuTimer.ElapsedTicks >= cpuTicks)
            {
                chip8.Step();
                cpuTimer.Restart();
            }
            
            // Accumulate for a stable 60Hz
            long elapsed = frameTimer.ElapsedTicks;
            frameTimer.Restart();
            accumulator += elapsed;

            // Handle input outside of the 60Hz rate
            while (SDL_PollEvent(out SDL_Event e) != 0)
            {
                int key = keycodeToIndex(e.key.keysym.sym);
                switch (e.type)
                {
                    case SDL_EventType.SDL_QUIT: running = false; break;
                    case SDL_EventType.SDL_KEYDOWN:
                        chip8.Keyboard |= (ushort)(1 << key);
                        if (chip8.WaitingForKeyPress) chip8.KeyPressed((byte)key);
                        break;
                    case SDL_EventType.SDL_KEYUP:
                        chip8.Keyboard &= (ushort)~(1 << key);
                        break;
                }
            }
            
            // Output to display at exactly 60Hz cadence
            while (accumulator >= frameTicks)
            {
                GCHandle displayHandle = GCHandle.Alloc(chip8.Display, GCHandleType.Pinned);
                try
                {
                    SDL_UpdateTexture(SDLTexture, IntPtr.Zero, displayHandle.AddrOfPinnedObject(), 64 * 4);
                }
                finally
                {
                    displayHandle.Free();
                }

                // Clear -> Copy -> Present
                SDL_RenderClear(renderer);
                SDL_RenderCopy(renderer, SDLTexture, IntPtr.Zero, IntPtr.Zero);
                SDL_RenderPresent(renderer);

                accumulator -= frameTicks;
            }
        }
        
        SDL_DestroyRenderer(renderer);
        SDL_DestroyWindow(window);
    }

    private static int keycodeToIndex(SDL_Keycode keycode)
    {
        var key  = (int)keycode;                    // ascii int value for the key pressed
        return (key < 58) ? key - 48 : key - 87;    // index for that key
    }
}