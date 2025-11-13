using System.Diagnostics;
using static SDL2.SDL;

namespace CHIP_8;

public static class Program
{
    static void Main(string[] args)
    {
        const uint flags = SDL_INIT_VIDEO |
                           SDL_INIT_AUDIO |
                           SDL_INIT_TIMER |
                           SDL_INIT_EVENTS;
        
        if (SDL_Init(flags) < 0) throw new Exception("SDL init failed");

        /// Configure VM components
        CPU          cpu      = new();
        RenderEngine renderer = new(64, 32, 8);
        AudioEngine  _        = new(() => cpu.SoundTimer, v => cpu.SoundTimer = v);
        InputHandler input    = new(cpu);
        
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
        
        while (input.Running)
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
            input.PollEvent();
            
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
}