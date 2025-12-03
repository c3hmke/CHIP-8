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
        }
        
        ApplyDirectionalSmear(_decayBuffer);

        base.Render();
    }
    
    private void ApplyDirectionalSmear(float[] buffer)
    {
        // Horizontal smear only (primary artifact of old calculators)
        for (int y = 0; y < _height; y++)
        {
            int row = y * _width;

            float prev = buffer[row]; // left edge

            for (int x = 0; x < _width; x++)
            {
                int i = row + x;

                float curr = buffer[i];

                // 70% current, 30% previous pixel in row
                float smeared = curr * 0.97f + prev * 0.03f;

                buffer[i] = smeared;
                prev      = smeared;
            }
        }
    }
}