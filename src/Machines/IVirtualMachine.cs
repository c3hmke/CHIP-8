namespace CHIP_8.Machines;

public interface IVirtualMachine
{
    void LoadProgram(byte[] program);
    void Reset();
    
    void StepInstruction();
    void TickTimers();
    
    void KeyDown(byte key);
    void KeyUp(byte key);
}