// ReSharper disable AccessToDisposedClosure; Disposed after use.

using OpenTK;
using OpenTK.Graphics.OpenGL4;
using CHIP_8.Graphics;
using ImGuiNET;
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

/// <summary>
/// Main Emulator program loop, this builds all the components to make the emulator
/// run and is responsible for the render pipeline.
/// </summary>
public static class Program
{
    private const int menuH    = 20;
    private const int screenW  = 64;
    private const int screenH  = 32;
    private const int scale    = 16;
    
    private static void Main(string[] args)
    {
        /// --------------------------------------------------------------------
        ///     ROM Discovery
        /// --------------------------------------------------------------------
        string ROMDir = Path.Combine(AppContext.BaseDirectory, "ROMs");
        string[] ROMs = Directory.GetFiles(ROMDir)
                                 .OrderBy(Path.GetFileNameWithoutExtension)
                                 .ToArray();

        int currentROM    = 0;  // currently loaded ROM index
        string[] ROMNames = ROMs.Select(Path.GetFileName).ToArray()!;
        
        // --------------------------------------------------------------------
        //      SDL and emulator initialization
        // --------------------------------------------------------------------
        const uint flags = SDL_INIT_VIDEO |
                           SDL_INIT_AUDIO |
                           SDL_INIT_TIMER |
                           SDL_INIT_EVENTS;
        
        if (SDL_Init(flags) < 0) 
            throw new Exception($"SDL init FAIL: {SDL_GetError()}");
        
        // Setup OpenGL Attributes
        SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_MAJOR_VERSION, 3);
        SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_MINOR_VERSION, 3);
        SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_PROFILE_MASK, SDL_GLprofile.SDL_GL_CONTEXT_PROFILE_CORE);

        SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_DOUBLEBUFFER, 1);
        SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_DEPTH_SIZE, 24);
        SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_STENCIL_SIZE, 8);
        
        nint window = SDL_CreateWindow(
            "CHIP-8", 
            SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED,
            (screenW * scale),
            (screenH * scale) + menuH,
            SDL_WindowFlags.SDL_WINDOW_OPENGL);
        
        if (window == 0)
            throw new Exception($"SDL_CreateWindow FAIL: {SDL_GetError()}");

        nint glContext = SDL_GL_CreateContext(window);
        
        if (glContext == 0)
            throw new Exception($"SDL_GL_CreateContext FAIL: {SDL_GetError()}");
        
        // Set up the Window
        SDL_GL_MakeCurrent(window, glContext);      // Active window
        SDL_GL_SetSwapInterval(1);                  // VSync

        GL.LoadBindings(new SDL_GL_BindingsContext());
        GL.Viewport(0, 0, screenW * scale, screenH * scale);
        GL.ClearColor(1f, 0f, 0f, 1f);
        
        // --------------------------------------------------------------------
        //      ImGUI
        // --------------------------------------------------------------------
        SDL_GetWindowSize(window, out int winW, out int winH);
        var imgui = new ImGuiRenderer(winW, winH);        
        
        // --------------------------------------------------------------------
        //      VM Components
        // --------------------------------------------------------------------
        CPU              cpu      = new();
        PhosphorRenderer renderer = new(screenW, screenH, scale);
        AudioEngine      _        = new(() => cpu.SoundTimer, v => cpu.SoundTimer = v);
        InputHandler     input    = new(
            () => cpu.WaitingForKeyPress,
            k  => cpu.KeyPressed(k),
            () => cpu.Keyboard,
            v  => cpu.Keyboard = v);
        
        // Load initial ROM into program memory (first in list by default)
        cpu.LoadProgram(File.ReadAllBytes(ROMs[currentROM]));
        
        // ClockHandler handles syncing and execution of the Display & CPU
        ClockHandler clock = new(cpu.Step, () =>
        {
            renderer.Render(cpu.Display);   // Render CHIP-8 framebuffer
        });
        
        // --------------------------------------------------------------------
        //      Main program loop
        // --------------------------------------------------------------------
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        float lastTime = 0f;
        
        while (input.Running)
        {
            // Timing for ImGUI
            float now = (float)stopwatch.Elapsed.TotalSeconds;
            float delta = now - lastTime;
            lastTime = now;
            
            // Tick ImGUI
            imgui.UpdateFrame(delta, winW, winH); 
            
            // Demo Window
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("Foo"))
                {
                    if (ImGui.MenuItem("Bar")) { }
                    if (ImGui.MenuItem("Baz")) { }

                    ImGui.Separator();

                    if (ImGui.MenuItem("Gib")) { }

                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Ringus"))
                {
                    if (ImGui.MenuItem("Dingus")) { }
                    if (ImGui.MenuItem("Pingus")) { }
                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }
            
            
            clock.Tick();        // CPU steps and renderer.Render(cpu.Display)
            //input.PollEvent(); // Handle SDL events for input handler

            // Drain SDL event queue
            while (SDL_PollEvent(out SDL_Event e) != 0)
            {
                input.HandleEvent(ref e);  // Handle SDL events for input handler
                imgui.ProcessEvent(ref e); // Handle SDL events for ImGUI
            }
            
            // Render ImGUI
            imgui.Render();
            
            SDL_GL_SwapWindow(window);
        }

        // --------------------------------------------------------------------
        //      Cleanup
        // --------------------------------------------------------------------
        renderer.Dispose();
        SDL_GL_DeleteContext(glContext);
        SDL_DestroyWindow(window);
        SDL_Quit();
    }
}