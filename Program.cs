using System.Diagnostics;
using static SDL2.SDL;

namespace CHIP_8;

public static class Program
{
    static void Main(string[] args)
    {
        if (SDL_Init(SDL_INIT_EVERYTHING) < 0) throw new Exception("SDL init failed");

        CPU          cpu      = new();
        RenderEngine renderer = new(64, 32, 8);
        AudioEngine  audio    = new(cpu);
        
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

            cpu.LoadProgram(program.ToArray());
        }
        
        var running = true;
        while (running)
        {
            if (!cpu.WaitingForKeyPress && cpuTimer.ElapsedTicks >= cpuTicks)
            {
                cpu.Step();
                cpuTimer.Restart();
            }
            
            // Accumulate for a stable 60Hz
            long elapsed = frameTimer.ElapsedTicks;
            frameTimer.Restart();
            accumulator += elapsed;

            // Handle input outside the 60Hz rate
            while (SDL_PollEvent(out SDL_Event e) != 0)
            {
                int key = keycodeToIndex(e.key.keysym.sym);
                switch (e.type)
                {
                    case SDL_EventType.SDL_QUIT: running = false; break;
                    case SDL_EventType.SDL_KEYDOWN:
                        cpu.Keyboard |= (ushort)(1 << key);
                        if (cpu.WaitingForKeyPress) cpu.KeyPressed((byte)key);
                        break;
                    case SDL_EventType.SDL_KEYUP:
                        cpu.Keyboard &= (ushort)~(1 << key);
                        break;
                }
            }
            
            /// Output to display at exactly 60Hz cadence
            while (accumulator >= frameTicks)
            {
                renderer.Render(cpu.Display);

                accumulator -= frameTicks;
            }
        }

        renderer.Dispose();
        SDL_Quit();
    }

    private static int keycodeToIndex(SDL_Keycode keycode)
    {
        var key  = (int)keycode;                    // ascii int value for the key pressed
        return (key < 58) ? key - 48 : key - 87;    // index for that key
    }
}