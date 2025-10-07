namespace CHIP_8;

/// <summary>
/// The CHIP-8 Virtual Machine.
/// This class contains all the implementation details needed to load and run CHIP-8 programs, as well as execute other
/// VM related operations and tasks. Full specifications for the Virtual Machine and opcodes can be found below.
///
/// # VM Specifications ------------------------------------------------------------------------------------------------
/// 4Kb Memory:     4096 memory locations, all of which are 8-bits wide (where the term CHIP-8 originated from).        
///                 The interpreter itself occupies the first 512 bytes of memory space on the machine, for this reason 
///                 most programs written for CHIP-8 begin at memory location 512 and don't access any memory below     
///                 location 512 (0x200). The uppermost 256 bytes (0xF00 - 0x1FF) are reserved for display refresh and  
///                 the 96 bytes below that (0xEA0 - 0xEFF) are reserved for the call stack, internal use and variables.
///
/// Registers:      CHIP-8 has 16 8-bit registers named V0 -> VF. The VF register doubles as a flag for some instructions
///                 and thus should be avoided. It's the carry flag for addition, the 'no borrow' flag during subtraction
///                 and is used for collision detection during draw instructions.
///                 The address register, named I, is 12 bytes wide and used with opcodes involving memory ops.
///
/// The Stack:      The stack is only used to store return addresses when subroutines are called. The original RCA1802
///                 version allocated 48 bytes for up to 12 levels of nesting. Some modern implementations have more.
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
///                 Sprite pixels are XOR'd with the corresponding screen pixels to check for collision.
///
/// Opcodes:        CHIP-8 has 35 opcodes which are 2 bytes long and stored in big-endian. The opcodes are listed 
///                 below in hexadecimal with the following symbols:
///                 - NNN   : address
///                 - NN    : 8-bit constant
///                 - N     : 4-bit constant
///                 - X & Y : 4-bit register identifier
///                 - PC    : Program Counter
///                 - I     : 12-bit register for memory address
///                 - VN    : One of the 16 available variables, 0 - F
/// # Opcode Table -----------------------------------------------------------------------------------------------------
/// | Opcode | Type  |  Pseudo C         | Description                                                                 |
/// --------------------------------------------------------------------------------------------------------------------
/// |  ONNN  | call  |                   | Calls RCA1802 Program at address NNN. Not necessary for most ROMs.          |
/// |  00E0  | displ |  disp_clear()     | Clears the Screen.                                                          |
/// |  00EE  | flow  |  return;          | Returns from a subroutine.                                                  |
/// |  1NNN  | flow  |  goto NNN;        | Jumps to address NNN.                                                       |
/// |  2NNN  | flow  |  *(0xNNN)()       | Calls subroutine at NNN.                                                    |
/// |  3XNN  | cond  |  if (Vx == NN)    | Skips the next instruction if Vx == NN.                                     |
/// |  4XNN  | cond  |  if (Vx != NN)    | Skips the next instruction if Vx != NN.                                     |
/// |  5XY0  | cond  |  if (Vx == Vy)    | Skips the next instruction if Vx == Vy.                                     |
/// |  6XNN  | const |  Vx = NN          | Sets Vx to NN.                                                              |
/// |  7XNN  | const |  Vx += NN         | Adds NN to Vx; Carry flag will remain unchanged.                            |
/// |  8XY0  | assig |  Vx = Vy          | Sets Vx to value of Vy.                                                     |
/// |  8XY1  | bitop |  Vx |= Vy         | Sets Vx to value of Vx | Vy (bitwise OR).                                   |
/// |  8XY2  | bitop |  Vx &= Vy         | Sets Vx to value of Vx & Vy (bitwise AND).                                  |
/// |  8XY3  | bitop |  Vx ^= Vy         | Sets Vx to Vx XOR Vy.                                                       |
/// |  8XY4  | math  |  Vx += Vy         | Adds Vy to Vx; VF is set to 1 if there is an overflow and 0 if not.         |
/// |  8XY5  | math  |  Vx -= Vy         | Subtracts Vy from Vx; VF is set to 1 if Vx >= Vy and 0 if not.              |
/// |  8XY6  | bitop |  Vx >>= 1         | Store the LSB of Vx in VF, then shift Vx right by 1.                        |
/// |  8XY7  | math  |  Vx = Vy - Vx     | Sets Vx to Vy - Vx; VF is set to 1 if Vy >= Vx and 0 if not.                |
/// |  8XYE  | bitop |  Vx <<= 1         | Store the MSB of Vx in VF, then shift Vx left by 1.                         |
/// |  9XY0  | cond  |  if (Vx != Vy)    | Skips the next instruction if Vx != Vy.                                     |
/// |  ANNN  | mem   |  I = NNN          | Sets I to the address NNN.                                                  |
/// |  BNNN  | flow  |  PC = V0 + NNN    | Jumps to the address NNN plus V0.                                           |
/// |  CXNN  | rand  |  Vx = rand & NN   | Sets Vx to result of bitwise AND on a random number (0-255) and NN.         |
/// |  DXYN  | displ |  draw(Vx, Vy, N)  | Draws a sprite at coord Vx,Vy with a depth of 8 pixels and height of N.     |
/// |        |       |                   | Each row of 8 pixels is read as bit-coded starting from memory location I.  |
/// |        |       |                   | The value of I remains unchanged by the execution of instructions. VF is    |
/// |        |       |                   | set to 1 if any pixels are flipped to unset and 0 if not.                   |
/// |  EX9E  | keyop |  if (key == Vx)   | Skips the next instruction if the key in Vx is pressed.                     |
/// |  EXA1  | keyop |  if (key != Vx)   | Skips the next instruction if the key in Vx is not pressed.                 |
/// |  FX07  | timer |  Vx = getDelay()  | Sets Vx to the value of the DelayTimer                                      |
/// |  FX0A  | keyop |  Vx = getKey()    | Key press is awaited then stored in Vx (Blocking operation).                | 
/// |  FX15  | timer |  setDelay(Vx)     | Sets the delay timer to Vx.                                                 |
/// |  FX18  | sound |  setSound(Vx)     | Sets the sound timer to Vx.                                                 |
/// |  FX1E  | mem   |  I += Vx          | Adds Vx to I; VF remains unchanged.                                         |
/// |  FX29  | mem   |  I = sprite[Vx]   | Sets I to location of the sprite for the character in Vx.                   |
/// |  FX33  | bcd   |                   | Stores the binary-coded decimal of Vx in I, with the                        |
/// |        |       |                   | hundreds digit at I, tens at I+1, and ones at I+2.                          |
/// |  FX55  | mem   | reg_dump(Vx, &I)  | Stores V0 -> VX in memory starting at address I; I remains unchanged.       |
/// |  FX65  | mem   | reg_load(Vx, &I)  | Fills V0 -> VX from memory starting at address I; I remains unchanged.      |
/// --------------------------------------------------------------------------------------------------------------------
/// </summary>
public class CHIP8
{
    
}