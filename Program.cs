using SDL2;
using System.Runtime.InteropServices;

namespace CHIP_8;

public static class Program
{
    public static void Main(string[] args)
    {
        if (SDL.SDL_Init(SDL.SDL_INIT_EVERYTHING) < 0) throw new Exception("SDL init failed");
        
        nint window   = SDL.SDL_CreateWindow("CHIP-8", 128, 128, 64 * 16, 32 * 16, SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
        nint renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
        
        var chip8 = new CHIP8();
        using (var reader = new BinaryReader(new FileStream("../../../ROMs/BRIX", FileMode.Open)))
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
            nint SDLTexture = 0;
            
            chip8.Step();
            while (SDL.SDL_PollEvent(_event: out SDL.SDL_Event SDLEvent) != 0)
            {
                int key = keycodeToIndex(SDLEvent.key.keysym.sym);
                switch (SDLEvent.type)
                {
                    case SDL.SDL_EventType.SDL_QUIT: running = false; break;
                    case SDL.SDL_EventType.SDL_KEYDOWN:
                        chip8.Keyboard |= (ushort)key;
                        break;
                    case SDL.SDL_EventType.SDL_KEYUP:
                        chip8.Keyboard &= (ushort)~key;
                        break;
                }
            }

            GCHandle displayHandle = GCHandle.Alloc(chip8.Display, GCHandleType.Pinned);
            nint SDLSurface = SDL.SDL_CreateRGBSurfaceFrom(
                pixels: displayHandle.AddrOfPinnedObject(),
                width:  64,
                height: 32,
                depth:  32,         // 'size' per pixel
                pitch:  64 * 4,     // actual width?
                Rmask:  0x0000FF00, // part of the 'depth' to find the R value
                Gmask:  0x00FF0000, // part of the 'depth' to find the G value
                Bmask:  0xFF000000, // part of the 'depth' to find the B value
                Amask:  0x000000FF  // part of the 'depth' to find the A value
            );
            
            if (SDLTexture != 0) SDL.SDL_DestroyTexture(SDLTexture);
            SDLTexture = SDL.SDL_CreateTextureFromSurface(renderer, SDLSurface);

            displayHandle.Free();

            SDL.SDL_RenderClear(renderer);
            SDL.SDL_RenderCopy(renderer, SDLTexture, 0, 0);
            SDL.SDL_RenderPresent(renderer);
        }
    }

    private static int keycodeToIndex(SDL.SDL_Keycode keycode)
    {
        var key  = (int)keycode;                    // ascii int value for the key pressed
        int keyI = key < 58 ? key - 48 : key - 87;  // index for that key

        return (1 << keyI);
    }
}