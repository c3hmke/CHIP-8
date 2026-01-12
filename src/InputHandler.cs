using static SDL2.SDL;

namespace CHIP_8;

public class InputHandler(
    Func<bool>     isWaitingForKey,
    Action<byte>   onKeyPressed,
    Func<ushort>   getKeyboard,
    Action<ushort> setKeyboard)
{
    
    
    public void HandleKeypress(SDL_Event e)
    {
        ushort keyboard = getKeyboard();
        int key = KeycodeToIndex(e.key.keysym.sym);

        if (e.type == SDL_EventType.SDL_KEYDOWN)
        {
            keyboard |= (ushort)(1 << key);
            setKeyboard(keyboard);
            if (isWaitingForKey()) onKeyPressed((byte)key);
        }

        if (e.type == SDL_EventType.SDL_KEYUP)
        {
            keyboard &= (ushort)~(1 << key);
            setKeyboard(keyboard);
        }

    }
    
    private static int KeycodeToIndex(SDL_Keycode keycode)
    {
        var key = (int)keycode;                     // ascii int value for the key pressed
        return (key < 58) ? key - 48 : key - 87;    // index for that key
    }
}