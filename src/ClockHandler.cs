using System.Diagnostics;

namespace CHIP_8;

public class ClockHandler
{
    /// Configure clock settings
    private const int CPU_Hz   = 700;   // Frequency the CPU runs at
    private const int Frame_Hz = 60;    // Frequency the display refreshes at
    private long      accumulator;      // Used to keep display & CPU clocks synced
    
    /// Configure the CPU clock, used for executing opcodes
    private readonly Stopwatch _cpuTimer   = Stopwatch.StartNew();           // Timer for the CPU clock
    private readonly long      _cpuTicks   = Stopwatch.Frequency / CPU_Hz;   // running at CPU_Hz
    private readonly Action    _cpuStep;                                     // Action to take on CPU step

    /// Configure the Frame clock, used for display
    private readonly Stopwatch _frameTimer = Stopwatch.StartNew();           // Timer for display out
    private readonly long      _frameTicks = Stopwatch.Frequency / Frame_Hz; // running at Frame_Hz
    private readonly Action    _frameStep;                                   // Action to take on Frame step

    
    public ClockHandler(Action cpuStep, Action frameStep)
    {
        _cpuStep   = cpuStep;
        _frameStep = frameStep;
    }

    public void Tick()
    {
        StepCPU();
        StepFrame();
    }
    

    public void StepCPU()
    {
        if (_cpuTimer.ElapsedTicks >= _cpuTicks)
        {
            _cpuStep();
            _cpuTimer.Restart();
        }
    }

    public void StepFrame()
    {
        long elapsed = _frameTimer.ElapsedTicks;
        _frameTimer.Restart();
        
        accumulator += elapsed;
        
        while (accumulator >= _frameTicks)
        {
            _frameStep();
            accumulator -= _frameTicks;
        }
    }
}