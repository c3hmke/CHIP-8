using static SDL2.SDL;

namespace CHIP_8;

public class InputHandler(
    Func<bool>     isWaitingForKey,
    Action<byte>   onKeyPressed,
    Func<ushort>   getKeyboard,
    Action<ushort> setKeyboard)
{
    public bool Running { get; private set; } = true;

    public void PollEvent()
    {
        ushort keyboard = getKeyboard();
        
        while (SDL_PollEvent(out SDL_Event e) != 0)
        {
            int key = keycodeToIndex(e.key.keysym.sym);
            switch (e.type)
            {
                case SDL_EventType.SDL_QUIT: 
                    Running = false;
                    break;
                
                case SDL_EventType.SDL_KEYDOWN:
                    keyboard |= (ushort)(1 << key);
                    setKeyboard(keyboard);
                    if (isWaitingForKey()) onKeyPressed((byte)key);
                    break;
                
                case SDL_EventType.SDL_KEYUP:
                    keyboard &= (ushort)~(1 << key);
                    setKeyboard(keyboard);
                    break;
                
                default:
                    // Let anything else simply fall through
                    break;
            }
        }
    }
    
    private static int keycodeToIndex(SDL_Keycode keycode)
    {
        var key = (int)keycode;                     // ascii int value for the key pressed
        return (key < 58) ? key - 48 : key - 87;    // index for that key
    }
}