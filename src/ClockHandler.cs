using System.Diagnostics;

namespace CHIP_8;

/// <summary>
/// Keeps the CPU & Frame stepping in sync so that actions which need to be executed in either cadence
/// correctly adhere to the rate between the 2. By default this will be 700Hz CPU & 60 FPS display.
/// </summary>
public class ClockHandler (Action cpuStep, Action frameStep, int cpuHz = 700, int frameHz = 60)
{
    /// Used to keep display & CPU clocks synced
    private long accumulator;
    
    /// Configure the CPU clock, used for executing opcodes
    private readonly Stopwatch _cpuTimer = Stopwatch.StartNew();        // Timer for the CPU clock
    private readonly long      _cpuTicks = Stopwatch.Frequency / cpuHz; // Frequency for CPU clock, derived from _cpuHz
    
    /// Configure the Frame clock, used for display
    private readonly Stopwatch _frameTimer = Stopwatch.StartNew();          // Timer for display out
    private readonly long      _frameTicks = Stopwatch.Frequency / frameHz; // Frequency for CPU clock, derived from _frameHz
    
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
            cpuStep();
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
            frameStep();
            accumulator -= _frameTicks;
        }
    }
}