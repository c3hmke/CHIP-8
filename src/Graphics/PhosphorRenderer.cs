namespace CHIP_8.Graphics;

/// <summary>
/// Renderer emulating a phosphor based screen.
/// These types of screen were quick to show newly drawn pixels but had a long fade-out effect
/// </summary>
public class PhosphorRenderer (int width, int height, int scale) : RenderEngine (width, height, scale)
{
    /// Phosphor decay parameters; dual decay rate for non-linear decay effect.
    private const float   _qckDecayRate = 0.82f;  // quick rate, initial drop
    private const float   _slwDecayRate = 0.96f;  // slow rate, trails off after
    
    public void Render(uint[] display)
    {
        /// Update decay buffer based on display state.
        for (var i = 0; i < _width * _height; i++)
        {
            bool pixelOn = display[i] == 0xFFFFFFFF;           // is the pixel lit?
         
            if (pixelOn) _decayBuffer[i] = 1.0f;               // instant full brightness
            else                                               // decay old light (phosphor effect)
            {
                float v = _decayBuffer[i];
                
                v = MathF.Pow(v, 1.35f);                       // non-linear tail curve (emulates phosphor)
                v *= v > 0.5f ? _qckDecayRate : _slwDecayRate; // higher gamma yields slower fade at low intensities
                
                _decayBuffer[i] = v;
            }
        }

        base.Render();
    }
}