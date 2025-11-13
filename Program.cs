// ReSharper disable AccessToDisposedClosure; Disposed after use.

using static SDL2.SDL;

namespace CHIP_8;

public static class Program
{
    private static void Main(string[] args)
    {
        const uint flags = SDL_INIT_VIDEO |
                           SDL_INIT_AUDIO |
                           SDL_INIT_TIMER |
                           SDL_INIT_EVENTS;
        
        if (SDL_Init(flags) < 0) throw new Exception($"SDL init failed: {SDL_GetError()}");

        /// Configure VM components
        CPU          cpu      = new();
        RenderEngine renderer = new(64, 32, 8);
        AudioEngine  _        = new(() => cpu.SoundTimer, v => cpu.SoundTimer = v);
        ClockHandler clock    = new(cpu.Step, () => renderer.Render(cpu.Display));
        InputHandler input    = new(
            () => cpu.WaitingForKeyPress,
            k  => cpu.KeyPressed(k),
            () => cpu.Keyboard,
            v  => cpu.Keyboard = v);
        
        /// Load a ROM into the program memory
        string romPath = args.Length > 0 ? args[0] : "../../../ROMs/BRIX";
        cpu.LoadProgram(File.ReadAllBytes(romPath));
        
        /// Program loop
        while (input.Running)
        {
            clock.Tick();
            input.PollEvent();
        }

        renderer.Dispose();
        SDL_Quit();
    }
}