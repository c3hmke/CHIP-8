using System.Diagnostics;

namespace CHIP_8;

/// <summary>
/// Keeps the CPU & Frame stepping in sync so that actions which need to be executed in either cadence
/// correctly adhere to the rate between the 2. By default this will be 700Hz CPU & 60 FPS display.
/// </summary>
public class ClockHandler (Action cpuStep, Action frameStep, int cpuHz = 700, int frameHz = 60)
{
    /// Keeps track of whether the clock handler should be running or not.
    private bool _paused = false;
    
    /// Configure the CPU clock, used for executing opcodes
    private readonly Stopwatch _cpuTimer = Stopwatch.StartNew();        // Timer for the CPU clock
    private readonly long      _cpuTicks = Stopwatch.Frequency / cpuHz; // Frequency for CPU clock, derived from _cpuHz
    private long               _cpuAccumulator;
    
    /// Configure the Frame clock, used for display
    private readonly Stopwatch _frameTimer = Stopwatch.StartNew();          // Timer for display out
    private readonly long      _frameTicks = Stopwatch.Frequency / frameHz; // Frequency for CPU clock, derived from _frameHz
    private long               _frameAccumulator;


    /// Pause the Timers
    public void Pause()
    {
        _paused = true;
    }

    /// Resume the Timers
    public void Resume()
    {
        // This prevents the timers from being reset when calling this method while
        // the timers are already running.
        if (!_paused) return;
        
        _paused = false;
        Reset();
    }

    /// Reset all the timers
    public void Reset()
    {
        _cpuAccumulator = _frameAccumulator = 0;
        _cpuTimer.Restart();
        _frameTimer.Restart();
    }
    
    /// <summary>
    /// Tick over the system clocks. This will step on both CPU and Frame and execute
    /// the event-bound functions which were passed to the ClockHandler on construction.
    /// </summary>
    public void Tick()
    {
        if (_paused) return;
        
        StepCPU();
        StepFrame();
    }

    /// <summary>
    /// Step the CPU timer, then perform the event-bound action & reset the timer if
    /// the number of ticks passed matches the frequency the CPU runs at.
    /// </summary>
    private void StepCPU()
    {
        long elapsed = _cpuTimer.ElapsedTicks;
        _cpuTimer.Restart();
        
        _cpuAccumulator += elapsed;
        
        while (_cpuAccumulator >= _cpuTicks)
        {
            cpuStep();
            _cpuAccumulator -= _cpuTicks;
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
        
        _frameAccumulator += elapsed;
        
        while (_frameAccumulator >= _frameTicks)
        {
            frameStep();
            _frameAccumulator = 0; // discard any missed ticks
        }
    }
}