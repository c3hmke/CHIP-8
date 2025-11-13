using System.Diagnostics;

namespace CHIP_8;

public class ClockHandler
{
    private const int CPU_Hz   = 700;   // Frequency the CPU runs at
    private const int Frame_Hz = 60;    // Frequency the display refreshes at
    
    private long accumulator;           // Used to keep display & CPU clocks synced
    
    private readonly Stopwatch _cpuTimer   = Stopwatch.StartNew();           // Timer for the CPU clock
    private readonly long      _cpuTicks   = Stopwatch.Frequency / CPU_Hz;   // running at CPU_Hz
    private readonly Stopwatch _frameTimer = Stopwatch.StartNew();           // Timer for display out
    private readonly long      _frameTicks = Stopwatch.Frequency / Frame_Hz; // running at Frame_Hz
    

    public void StepCPU(Action step)
    {
        if (_cpuTimer.ElapsedTicks >= _cpuTicks)
        {
            step();
            _cpuTimer.Restart();
        }
    }

    public void StepFrame(Action render)
    {
        long elapsed = _frameTimer.ElapsedTicks;
        _frameTimer.Restart();
        accumulator += elapsed;
        while (accumulator >= _frameTicks)
        {
            render();
            accumulator -= _frameTicks;
        }
    }
}