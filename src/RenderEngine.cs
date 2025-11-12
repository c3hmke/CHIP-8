using System.Numerics;
using System.Runtime.InteropServices;

namespace CHIP_8;
using static SDL2.SDL;

public sealed class RenderEngine : IDisposable
{
    private readonly nint    _window, _renderer, _texture;
    private readonly uint[]  _brightnessLUT;
    private readonly float[] _decayBuffer;
    
    private const float DecayRate = 0.9f;

    public RenderEngine(int width, int height, int scale)
    {
        /// Configure graphics
        _window   = SDL_CreateWindow("CHIP-8", 128, 128, width * scale, height * scale, 0);
        _renderer = SDL_CreateRenderer(_window, -1, SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
        _texture  = SDL_CreateTexture(
            _renderer, SDL_PIXELFORMAT_RGBA8888,
            (int) SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
            width, height);
        
        /// Query the texture format details (this can be big / little endian depending on the platform)
        SDL_QueryTexture(_texture, out uint format, out _, out _, out _);
        SDL_PixelFormatEnumToMasks(format, out int bpp, out uint RMask, out uint GMask, out uint BMask, out uint AMask);
        
        /// Precompute shifts based on masks (fast integer shift count)
        int RShift = BitOperations.TrailingZeroCount(RMask);
        int GShift = BitOperations.TrailingZeroCount(GMask);
        int BShift = BitOperations.TrailingZeroCount(BMask);
        int AShift = BitOperations.TrailingZeroCount(AMask);
        
        /// Pre-pack the brightnes values in a LUT for faster shifting
        _brightnessLUT = new uint[256];
        for (var n = 0; n < 256; n++)
            _brightnessLUT[n] = ((uint)n << RShift) | ((uint)n << GShift) | ((uint)n << BShift) | (0xFFu << AShift);
        
        _decayBuffer = new float[width * height];
    }

    public void Render(uint[] display)
    {
        /// Mix in the decay blending
        for (var i = 0; i < 64 * 32; i++)
        {
            bool pixelOn = display[i] == 0xFFFFFFFF;
            if (pixelOn) _decayBuffer[i] = 1.0f;         // instant full brightness
            else         _decayBuffer[i] *= DecayRate;   // decay old light

            display[i] = _brightnessLUT[(byte)(_decayBuffer[i] * 255.0f)];
        }
                
        /// Update the texture on the screen
        GCHandle displayHandle = GCHandle.Alloc(display, GCHandleType.Pinned);
        try
        {
            SDL_UpdateTexture(_texture, IntPtr.Zero, displayHandle.AddrOfPinnedObject(), 64 * 4);
            SDL_RenderClear(_renderer);
            SDL_RenderCopy(_renderer, _texture, IntPtr.Zero, IntPtr.Zero);
            SDL_RenderPresent(_renderer);
        }
        finally
        {
            displayHandle.Free();
        }
    }
    
    public void Dispose()
    {
        SDL_DestroyRenderer(_renderer);
        SDL_DestroyWindow(_window);
    }
}