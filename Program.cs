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
        ClockHandler clock    = new();
        AudioEngine  _        = new(() => cpu.SoundTimer, v => cpu.SoundTimer = v);
        InputHandler input    = new(
            () => cpu.WaitingForKeyPress,
            k  => cpu.KeyPressed(k),
            () => cpu.Keyboard,
            v  => cpu.Keyboard = v);
        
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
            clock.StepCPU(cpu.Step);                                // Step the CPU
            input.PollEvent();                                      // Handle input outside the CPU & Frame timers
            clock.StepFrame(() => renderer.Render(cpu.Display));    // Draw a frame to the display
        }

        renderer.Dispose();
        SDL_Quit();
    }
}