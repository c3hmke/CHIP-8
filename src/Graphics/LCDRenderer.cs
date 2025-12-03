namespace CHIP_8.Graphics;

/// <summary>
/// Renderer emulating an LCD screen.
/// These screens would fade lit pixels in / out.
/// </summary>
public class LCDRenderer(int width, int height, int scale) : RenderEngine(width, height, scale)
{
    /// LCD ghosting parameters
    private const float RiseRate = 0.22f;   // ON transition speed
    private const float FallRate = 0.12f;   // OFF transition speed

    public void Render(uint[] display)
    {
        /// Update decay buffer based on display state.
        for (var i = 0; i < _width * _height; i++)
        {
            // Fade-in and fade-out based on whether the pixel is lit
            _decayBuffer[i] += display[i] == 0xFFFFFFFF 
                ? (1.0f - _decayBuffer[i]) * RiseRate   // slow rise towards 1.0
                : (0.0f - _decayBuffer[i]) * FallRate;  // slow fall towards 0.0


            // Convert brightness [0,1] to grayscale RGBA
            var brightness = (byte)(_decayBuffer[i] * 255.0f);
            
            _drawBuffer[i * 4 + 0] = brightness;       // R
            _drawBuffer[i * 4 + 1] = brightness;       // G
            _drawBuffer[i * 4 + 2] = brightness;       // B
            _drawBuffer[i * 4 + 3] = 255;              // A
        }

        base.Render();
    }
}