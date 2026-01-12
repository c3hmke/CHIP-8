namespace CHIP_8.Machines;

public interface IVirtualMachine
{
    public void Input(byte key);
    public void StepInstruction();
    public void TickTimers();
    public void Reset();
}