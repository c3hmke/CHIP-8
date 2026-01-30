// ReSharper disable AccessToDisposedClosure; Disposed after use.
// ReSharper disable InconsistentNaming; Follows library naming style

using CHIP_8.Drivers;
using CHIP_8.Emulation;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using CHIP_8.Graphics;
using CHIP_8.Machines;
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
    private const int MenuH    = 20;
    private const int ScreenW  = 64;
    private const int ScreenH  = 32;
    private const int Scale    = 8;
    
    private static void Main(string[] args)
    {
        // --------------------------------------------------------------------
        //     ROM Discovery
        // --------------------------------------------------------------------
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
            (ScreenW * Scale),
            (ScreenH * Scale) + MenuH,
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
        GL.Viewport(0, 0, ScreenW * Scale, ScreenH * Scale);
        GL.ClearColor(201f / 255f, 219f / 255f, 167f / 255f, 1f);
        
        FullscreenQuad.Init();
        
        // --------------------------------------------------------------------
        //      ImGUI
        // --------------------------------------------------------------------
        SDL_GetWindowSize(window, out int windowWidth, out int windowHeight);
        var imGui = new ImGuiController(windowWidth, windowHeight);
        
        
        // --------------------------------------------------------------------
        //      VM Components
        // --------------------------------------------------------------------
        CHIP8          machine   = new ();
        AudioDriver    audio     = new (machine);
        InputDriver    input     = new (machine);
        
        CHIP8Texture   texture   = new ();
        LCDDecayBuffer lcdBuffer = new (64, 32);
        LCDDecayPass   lcdPass   = new ();
        QuadRenderer   renderer  = new ();

        
        // --------------------------------------------------------------------
        //      Emulator
        // --------------------------------------------------------------------
        Emulator emulator = new (machine);

        void LoadRom(int romIndex)
        {
            currentROM = romIndex;
            emulator.Reset();
            machine.LoadProgram(File.ReadAllBytes(ROMs[currentROM]));
        }

        if (ROMs.Length > 0) LoadRom(currentROM);

        // --------------------------------------------------------------------
        //      Main program loop
        // --------------------------------------------------------------------
        
        var   stopwatch = System.Diagnostics.Stopwatch.StartNew();
        float lastTime  = 0f;

        bool pauseForFocus = false;
        
        while (true)
        {
            // ---- Track Time ----
            float now = (float)stopwatch.Elapsed.TotalSeconds;
            float delta = now - lastTime;
            lastTime = now;
            
            // ---- Process Events ----
            while (SDL_PollEvent(out SDL_Event e) != 0)
            {
                imGui.ProcessEvent(ref e);
                
                switch (e.type)
                {
                    case SDL_EventType.SDL_QUIT: goto Quit;

                    case SDL_EventType.SDL_WINDOWEVENT:
                    {
                        switch (e.window.windowEvent)
                        {
                            case SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_LOST:
                                pauseForFocus = true;
                                break;
                        
                            case SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_GAINED:
                                pauseForFocus = false;
                                break;
                        }
                        break;
                    }

                    case SDL_EventType.SDL_KEYDOWN:
                    case SDL_EventType.SDL_KEYUP:
                    {
                        // Only pass keyboard input to emulation if not used by ImGui
                        if (!ImGui.GetIO().WantCaptureKeyboard)
                            input.HandleEvent(e);
                        
                        break;
                    }
                        
                }
            }

            // ---- ImGUI Frame ----
            SDL_GetWindowSize(window, out windowWidth, out windowHeight);
            imGui.UpdateFrame(delta, windowWidth, windowHeight);
            
            
            // ---- Draw UI ----
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("ROMs"))
                {
                    if (ROMs.Length == 0)
                    {
                        ImGui.MenuItem("No ROMs found", string.Empty, false, false);
                    }
                    else
                    {
                        for (int i = 0; i < ROMs.Length; i++)
                        {
                            if (ImGui.MenuItem(ROMNames[i], string.Empty, i == currentROM))
                                LoadRom(i);
                        }
                    }
                    
                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }

            bool pauseForPopup = ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId);

            // Pause the emulation if any of the triggers caused a pause.
            if (pauseForFocus || pauseForPopup) 
                emulator.Pause();
            else
                emulator.Resume();

            // ---- Update Emulation ----
            emulator.Update();
            
            // ---- Render to Screen ----
            GL.Clear(ClearBufferMask.ColorBufferBit);

            // CHIP-8 output (reduced window size)
            int menuHeight = (int) ImGui.GetFrameHeight();
            int fbWidth    = windowWidth;
            int fbHeight   = windowHeight - menuHeight;
            GL.Viewport(0, 0, fbWidth, fbHeight);
            
            texture.Upload(machine.Display);
            
            // LCD simulation pass
            lcdPass.Execute(
                texture.TextureId,
                lcdBuffer.ReadTexture,
                lcdBuffer.WriteFbo);
            lcdBuffer.Swap();
            
            // Presentation pass
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            renderer.Draw(
                lcdBuffer.ReadTexture,
                fbWidth,
                fbHeight);
            
            // Reset viewport for ImGui
            GL.Viewport(0, 0, windowWidth, windowHeight);
            imGui.Render();
            
            SDL_GL_SwapWindow(window);
        }
        
        // --------------------------------------------------------------------
        //      Cleanup
        // --------------------------------------------------------------------
        Quit:
        {
            renderer.Dispose();
            texture.Dispose();
            imGui.Dispose();
            
            SDL_GL_DeleteContext(glContext);
            SDL_DestroyWindow(window);
            SDL_Quit();
        }
        
    }
}
