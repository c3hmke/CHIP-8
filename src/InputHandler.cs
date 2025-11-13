using static SDL2.SDL;

namespace CHIP_8;

public class InputHandler
{
    private readonly CPU _cpu;
    
    public bool Running { get; private set; } = true;
    
    public InputHandler(CPU cpu)
    {
        _cpu = cpu;
    }

    public void PollEvent()
    {
        while (SDL_PollEvent(out SDL_Event e) != 0)
        {
            int key = keycodeToIndex(e.key.keysym.sym);
            switch (e.type)
            {
                case SDL_EventType.SDL_QUIT: 
                    Running = false;
                    break;
                
                case SDL_EventType.SDL_KEYDOWN:
                    _cpu.Keyboard |= (ushort)(1 << key);
                    if (_cpu.WaitingForKeyPress)
                        _cpu.KeyPressed((byte)key);
                    break;
                
                case SDL_EventType.SDL_KEYUP:
                    _cpu.Keyboard &= (ushort)~(1 << key);
                    break;
                
                default:
                    // Let anything else simply fall through
                    break;
            }
        }
    }
    
    private static int keycodeToIndex(SDL_Keycode keycode)
    {
        var key  = (int)keycode;                    // ascii int value for the key pressed
        return (key < 58) ? key - 48 : key - 87;    // index for that key
    }
}