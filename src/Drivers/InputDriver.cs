using CHIP_8.Machines;
using static SDL2.SDL;

namespace CHIP_8.Drivers;

public class InputDriver (IVirtualMachine machine)
{
    /// <summary>
    /// Captures SDL input events then correctly passes them through to the Virtual Machine
    /// </summary>
    public void HandleEvent(SDL_Event e)
    {
        int key = KeycodeToIndex(e.key.keysym.sym);
        
        if (key is < 0 or > 0xF) return;
        
        if (e.type == SDL_EventType.SDL_KEYDOWN)
            machine.KeyDown((byte)key);
        
        if (e.type == SDL_EventType.SDL_KEYUP)
            machine.KeyUp((byte)key);
        
    }
    
    /// <summary>
    /// Converts an SDL keycode to a key index recognized by CHIP8.
    /// </summary>
    private static int KeycodeToIndex(SDL_Keycode keycode)
    {
        var key = (int)keycode;                     // ascii int value for the key pressed
        return (key < 58) ? key - 48 : key - 87;    // index for that key
    }
}