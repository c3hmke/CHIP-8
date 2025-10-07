namespace CHIP_8;

/// <summary>
/// The CHIP-8 Virtual Machine.
/// This class contains all the implementation details needed to load and run CHIP-8 programs.
///
/// # VM Specifications --
/// 4Kb Memory:     4096 memory locations, all of which are 8-bits wide (where the term CHIP-8 originated)
///                 The interpreter itself occupies the first 512 bytes of memory space on the machine, for
///                 this reason most programs written for CHIP-8 begin at memory location 512 and don't access
///                 any memory below location 512(0x200). The uppermost 256 bytes (0xF00 - 0x1FF) are reserved
///                 for display refresh and the 96 bytes below that (0xEA0 - 0xEFF) are reserved for the call
///                 stack, internal use and other variables.
///
/// Registers:      CHIP-8 has 16 8-bit registers named V0 -> VF. The VF register doubles as a flag for some
///                 instructions and thus should be avoided. It's the carry flag for addition, the 'no borrow'
///                 flag during subtraction and is used for collision detection during draw instructions.
///                 The address register, named I, is 12 bytes wide and used with opcodes involving memory ops.
///
/// The Stack:      The stack is only used to store return addresses when subroutines are called. The original
///                 RCA1802 version allocated 48 bytes for up to 12 levels of nesting.
///
/// Timers:         CHIP-8 has 2 timers, both count down at 60Hz until they reach 0.
///                 - Delay Timer: intended to be used for timing of events in games.
///                 - Sound Timer: when value is non-0 a beeping sound is made.
///
/// Input:          A hex keyboard is used for input with 16 keys ranging from 0 -> F.
///                 The 8,4,6,2 keys are typically used for directional input. 3 opcodes are used for dealing with
///                 input which either check if a key is pressed or not, or simply blocks and waits for input.
///
/// Graphics:       The original display resolution for CHIP-8 is 64x32 pixels with a monochrome colour.
///                 Graphics are drawn to the screen using sprites which are 8px wide and 1 - 15 px in height.
///                 Sprite pixels are XOR'd with corresponding screen pixels to check for collision.
/// </summary>
public class CHIP8
{
    
}