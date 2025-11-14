// ReSharper disable AccessToDisposedClosure; Disposed after use.

using OpenTK;
using OpenTK.Graphics.OpenGL4;
using static SDL2.SDL;

namespace CHIP_8;

/// <summary>
/// Custom binding context for OpenGL <-> SDL.
/// OpenTK 4.x requires explicit binding initialization, this class is used to tell
/// it about the SDL OpenGL context which is created when the renderer is created.
/// </summary>
public class SDL_GL_BindingsContext : IBindingsContext
{
    public IntPtr GetProcAddress(string procName) => SDL_GL_GetProcAddress(procName);
}

public static class Program
{
    private const int width  = 64;
    private const int height = 32;
    private const int scale  = 8;
    
    private static void Main(string[] args)
    {
        // /// --------------------------------------------------------------------
        // ///     ROM selection menu (console-based, before SDL is initialized)
        // /// --------------------------------------------------------------------
        // // Load the ROMs in the provided directory
        // string ROMDir = Path.GetFullPath("../../../ROMs");
        // string[] ROMs = Directory.GetFiles(ROMDir)
        //     .OrderBy(Path.GetFileNameWithoutExtension)
        //     .ToArray();
        //     
        // if (ROMs.Length == 0) throw new Exception("No ROMs found!");
        //
        // Console.WriteLine($"ROMs:");
        // for (var i = 0; i < ROMs.Length; i++)
        //     Console.WriteLine($"[{i}] {Path.GetFileName(ROMs[i])}");
        //
        // Console.WriteLine("Select ROM index: ");
        // string line = Console.ReadLine()!;
        //
        // string ROMPath = ROMs[int.Parse(line)];
        
        // --------------------------------------------------------------------
        //      SDL and emulator initialization
        // --------------------------------------------------------------------
        const uint flags = SDL_INIT_VIDEO |
                           SDL_INIT_AUDIO |
                           SDL_INIT_TIMER |
                           SDL_INIT_EVENTS;
        
        if (SDL_Init(flags) < 0) 
            throw new Exception($"SDL init FAIL: {SDL_GetError()}");
        
        /// Setup OpenGL Attributes
        SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_MAJOR_VERSION, 3);
        SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_MINOR_VERSION, 3);
        SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_PROFILE_MASK, SDL_GLprofile.SDL_GL_CONTEXT_PROFILE_CORE);

        SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_DOUBLEBUFFER, 1);
        SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_DEPTH_SIZE, 24);
        SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_STENCIL_SIZE, 8);
        
        nint window = SDL_CreateWindow(
            "CHIP-8", 
            SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED,
            width * scale, height * scale,
            SDL_WindowFlags.SDL_WINDOW_OPENGL);

        if (window == 0)
            throw new Exception($"SDL_CreateWindow FAIL: {SDL_GetError()}");

        nint glContext = SDL_GL_CreateContext(window);
        
        if (glContext == 0)
            throw new Exception($"SDL_GL_CreateContext FAIL: {SDL_GetError()}");
        
        /// Set up the Window
        SDL_GL_MakeCurrent(window, glContext);      // Active window
        SDL_GL_SetSwapInterval(1);                  // VSync

        GL.LoadBindings(new SDL_GL_BindingsContext());
        GL.Viewport(0, 0, width * scale, height * scale);
        GL.ClearColor(1f, 0f, 0f, 1f);
        
        /// Configure VM components
        CPU          cpu      = new();
        RenderEngine renderer = new(width, height, scale);
        AudioEngine  _        = new(() => cpu.SoundTimer, v => cpu.SoundTimer = v);
        InputHandler input    = new(
            () => cpu.WaitingForKeyPress,
            k  => cpu.KeyPressed(k),
            () => cpu.Keyboard,
            v  => cpu.Keyboard = v);
        ClockHandler clock = new(cpu.Step, () =>
        {
            renderer.Render(cpu.Display);
            SDL_GL_SwapWindow(window);
        });
        
        /// Load a ROM into the program memory
        cpu.LoadProgram(File.ReadAllBytes("../../../ROMs/BRIX"));
        
        // --------------------------------------------------------------------
        //      Main program loop
        // --------------------------------------------------------------------
        while (input.Running)
        {
            clock.Tick();
            input.PollEvent();
        }

        /// Dispose of objects once done running
        renderer.Dispose();
        SDL_GL_DeleteContext(glContext);
        SDL_DestroyWindow(window);
        SDL_Quit();
    }
}