using System.Diagnostics;

namespace CHIP_8;

public class ClockHandler
{
    /// Used to keep display & CPU clocks synced
    private long accumulator;
    
    /// Configure the CPU clock, used for executing opcodes
    private readonly Stopwatch _cpuTimer   = Stopwatch.StartNew();  // Timer for the CPU clock
    private readonly long      _cpuTicks;                           // Frequency for CPU clock, derived from _cpuHz
    private readonly Action    _cpuStep;                            // Action to take on CPU step

    /// Configure the Frame clock, used for display
    private readonly Stopwatch _frameTimer = Stopwatch.StartNew();  // Timer for display out
    private readonly long      _frameTicks;                         // Frequency for CPU clock, derived from _frameHz
    private readonly Action    _frameStep;                          // Action to take on Frame step

    
    public ClockHandler(Action cpuStep, Action frameStep, int cpuHz = 700, int frameHz = 60)
    {
        _cpuStep   = cpuStep;
        _frameStep = frameStep;
        
        _cpuTicks   = Stopwatch.Frequency / cpuHz;
        _frameTicks = Stopwatch.Frequency / frameHz;
    }

    /// <summary>
    /// Tick over the system clocks. This will step on both CPU and Frame and execute
    /// the event-bound functions which were passed to the ClockHandler on construction.
    /// </summary>
    public void Tick()
    {
        StepCPU();
        StepFrame();
    }

    /// <summary>
    /// Step the CPU timer, then perform the event-bound action & reset the timer if
    /// the number of ticks passed matches the frequency the CPU runs at.
    /// </summary>
    private void StepCPU()
    {
        if (_cpuTimer.ElapsedTicks >= _cpuTicks)
        {
            _cpuStep();
            _cpuTimer.Restart();
        }
    }

    /// <summary>
    /// Step the Frame timer, then perform the event-bound action & reset the timer if
    /// the number of ticks passed matches the frequency the Display runs at.
    /// </summary>
    private void StepFrame()
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