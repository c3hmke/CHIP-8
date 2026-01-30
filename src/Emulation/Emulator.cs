using System.Diagnostics;
using CHIP_8.Machines;

namespace CHIP_8.Emulation;

/// <summary>
/// The Emulator drives the 'Virtual Machine'.
/// It coordinates the subsystems and decides what runs when, enforcing lifecycle rules.
/// </summary>
/// <param name="cpuHz">The number of instructions which will be executed per second</param>
/// <param name="refreshHz">Both the display and timers will update this many times per second</param>
public class Emulator (IVirtualMachine machine, int cpuHz = 700, int refreshHz = 60)
{
    // === Configuration ===
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    
    private readonly long _cpuStepFrequency   = Stopwatch.Frequency / cpuHz;
    private readonly long _timerStepFrequency = Stopwatch.Frequency / refreshHz;
    private readonly long _frameStepFrequency = Stopwatch.Frequency / refreshHz;
    
    private long _cpuAccumulator, _timerAccumulator, _frameAccumulator;
    private bool _paused;

    /// <summary>
    /// Hook for rendering. Will call the action at the refreshRate interval.
    /// </summary>
    public event Action? OnFrame;
    
    
    /// <summary>
    /// Advances the emulation based on elapsed wall-clock time.
    /// Called once per host frame / loop iteration.
    /// </summary>
    public void Update()
    {
        if (_paused) return;    // Simply do nothing if emulation is paused
        
        // Get the elapsed ticks since the last update then restart the clock
        long elapsedTicks = _clock.ElapsedTicks;
        _clock.Restart();
        
        // Update the CPU Accumulator and execute instructions
        _cpuAccumulator   += elapsedTicks;
        while (_cpuAccumulator >= _cpuStepFrequency)
        {
            machine.StepInstruction();
            _cpuAccumulator -= _cpuStepFrequency;
        }
        
        // Update the Timer Accumulator and tick down machine timers
        _timerAccumulator += elapsedTicks;
        while (_timerAccumulator >= _timerStepFrequency)
        {
            machine.TickTimers();
            _timerAccumulator -= _timerStepFrequency;
        }
        
        // Update the FrameAccumulator and draw a single step
        _frameAccumulator += elapsedTicks;
        while (_frameAccumulator >= _frameStepFrequency)
        {
            OnFrame?.Invoke();
            _frameAccumulator = 0;
        }
    }
    
    /// <summary>
    /// Resets the emulation and the underlying VM.
    /// </summary>
    public void Reset()
    {
        machine.Reset();
        _cpuAccumulator = _timerAccumulator = _frameAccumulator = 0;

        if (_paused) _clock.Reset();
        else         _clock.Restart();
    }
    
    /// <summary>
    /// Pauses the emulation
    /// </summary>
    public void Pause()
    {
        _paused = true;
        _clock.Stop();
    }
    
    /// <summary>
    /// Resumes the emulation, resetting accumulators and clock.
    /// </summary>
    public void Resume()
    {
        // Guards against resetting if called on a running emulation
        if (!_paused) return;
        
        _paused = false;
        
        _cpuAccumulator = _timerAccumulator = _frameAccumulator = 0;
        _clock.Restart();
    }
}
