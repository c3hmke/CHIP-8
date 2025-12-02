using static SDL2.SDL;

namespace CHIP_8;

public class InputHandler(
    Func<bool>     isWaitingForKey,
    Action<byte>   onKeyPressed,
    Func<ushort>   getKeyboard,
    Action<ushort> setKeyboard,
    Action<SDL_Event>? forwardEvent = null,
    Func<bool>?        captureKeyboard = null)
{
    public bool Running { get; private set; } = true;

    public void PollEvent()
    {
        ushort keyboard = getKeyboard();

        while (SDL_PollEvent(out SDL_Event e) != 0)
        {
            forwardEvent?.Invoke(e);

            switch (e.type)
            {
                case SDL_EventType.SDL_QUIT:
                    Running = false;
                    break;

                case SDL_EventType.SDL_KEYDOWN:
                    if (captureKeyboard?.Invoke() == true) break;
                    int keyDown = keycodeToIndex(e.key.keysym.sym);
                    if (keyDown is < 0 or > 15) break;
                    keyboard |= (ushort)(1 << keyDown);
                    setKeyboard(keyboard);
                    if (isWaitingForKey()) onKeyPressed((byte)keyDown);
                    break;

                case SDL_EventType.SDL_KEYUP:
                    if (captureKeyboard?.Invoke() == true) break;
                    int keyUp = keycodeToIndex(e.key.keysym.sym);
                    if (keyUp is < 0 or > 15) break;
                    keyboard &= (ushort)~(1 << keyUp);
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