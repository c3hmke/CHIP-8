using System.ComponentModel.Design;

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
public class CHIP8 ()
{
    /// Define the properties of the VirtualMachine so that it can be correctly simulated.
    public byte[] RAM = new byte[4096];         // 4Kb Memory
    public byte[] V = new byte[16];             // Registers [V0 -> VF]
    public ushort I = 0;                        // Address register with 16-bits
    public ushort PC = 0;                       // Program Counter
    public Stack<ushort> Stack = new();         // Stack (currently 'unlimited')
    public byte DelayTimer;                     // DelayTimer used for timed events
    public byte SoundTimer;                     // SoundTimer used for beep
    public byte Keyboard;                       // Use the lower4 bits for 16 keys
    public byte[] Display = new byte[64 * 32];  // 64x32 display (only LSB is relevant)

    /// Random number generator used to randomize some events
    private readonly Random _rng = new (Environment.TickCount);

    /// Used to block and wait for input from the Keyboard
    private bool _awaitingInput = false;

    /// Load a program into RAM to be executed by the emulator
    public void LoadProgram(byte[] program)
    {
        RAM = new byte[4096];   // clear memory
        PC = 512;               // set PC to start locations

        // Load in the program passed to this function
        for (int i = 0; i < program.Length; i++)
            RAM[512 + i] = program[i];
    }

    ///<summary> Execute a step in the Program (execute the next opcode in memory) </summary>
    public void Step()
    {
        ushort opcode = (ushort)(RAM[PC] << 8 | RAM[PC + 1]);

        if (_awaitingInput)                 // block if awaiting input
        {
            V[(opcode & 0x0F00) >> 8] = Keyboard;
            return;
        }

        PC += 2;                            // increment the program counter

        /// opcodes are grouped by the first 4 bits, switch on that.
        switch ((ushort)(opcode & 0xF000))  // then execute the opcode
        {
            case 0x0000:
                switch (opcode)
                {
                    case 0x00E0: for (var i = 0; i < Display.Length; i++) Display[i] = 0; break;
                    case 0x00EE: PC = Stack.Pop(); break;
                    default: throw new Exception($"Unsupported opcode {opcode:x4}");
                }
                break;
            case 0x1000: // ( 1NNN )
                PC = (ushort)(opcode & 0x0FFF);
                break;
            case 0x2000: // ( 2NNN )
                Stack.Push(PC);
                PC = (ushort)(opcode & 0x0FFF);
                break;
            case 0x3000: // ( 3XNN )
                if (V[(opcode & 0x0F00) >> 8] == (opcode & 0x00FF)) PC += 2;
                break;
            case 0x4000: // ( 4XNN )
                if (V[(opcode & 0x0F00) >> 8] != (opcode & 0x00FF)) PC += 2;
                break;
            case 0x5000: // ( 5XY0 )
                if (V[(opcode & 0x0F00) >> 8] == V[(opcode & 0x00F0) >> 4]) PC += 2;
                break;
            case 0x6000: // ( 6XNN )
                V[(opcode & 0x0F00) >> 8] = (byte)(opcode & 0x00FF);
                break;
            case 0x7000: // ( 7XNN )
                V[(opcode & 0x0F00) >> 8] += (byte)(opcode & 0x00FF);
                break;

            case 0x8000: // (ALU in 8 range, switches on lowest 4-bits)
                int vx = (opcode & 0x0F00) >> 8;
                int vy = (opcode & 0x00F0) >> 4;
                switch (opcode & 0x000F)
                {
                    case 0: V[vx] = V[vy]; break;
                    case 1: V[vx] = (byte)(V[vx] | V[vy]); break;
                    case 2: V[vx] = (byte)(V[vx] & V[vy]); break;
                    case 3: V[vx] = (byte)(V[vx] ^ V[vy]); break;
                    case 4:
                        V[15] = (byte)(V[vx] + V[vy] > 255 ? 1 : 0);
                        V[vx] = (byte)((V[vx] + V[vy]) & 0x000F);
                        break;
                    case 5:
                        V[15] = (byte)(V[vx] > V[vy] ? 1 : 0);
                        V[vx] = (byte)((V[vx] - V[vy]) & 0x00FF);
                        break;
                    case 6:
                        V[15] = (byte)(V[vx] & 0x0001);
                        V[vx] = (byte)(V[vx] >> 1);
                        break;
                    case 7:
                        V[15] = (byte)(V[vy] > V[vx] ? 1 : 0);
                        V[vx] = (byte)((V[vy] - V[vx]) & 0x00FF);
                        break;
                    case 14:
                        V[15] = (byte)(((V[vx] & 0x80) == 0x80) ? 1 : 0);
                        V[vx] = (byte)(V[vx] << 1);
                        break;
                    default: throw new Exception($"Unsupported opcode {opcode:x4}");

                }
                break;

            case 0x9000: // ( 9XY0 )
                if (V[(opcode & 0x0F00) >> 8] != V[(opcode & 0x00F0) >> 4]) PC += 2;
                break;
            case 0xA000: // ( ANNN )
                I = (ushort)(opcode & 0x0FFF);
                break;
            case 0xB000: // ( BNNN )
                PC = (ushort)((opcode & 0x0FFF) + V[0]);
                break;
            case 0xC000: // ( CXNN )
                V[(opcode & 0x0F00) >> 8] = (byte)(_rng.Next() & (opcode & 0x0FF));
                break;
            
            case 0xD000: // ( DXYN )
                int x = V[(opcode & 0x0F00) >> 8];                      // x coordinate
                int y = V[(opcode & 0x00F0) >> 4];                      // y coordinate
                int n = opcode & 0x000F;                                // n lines to draw

                V[15] = 0;                                              // pixel flip flag

                for (int i = 0; i < n; i++)                             // loop over each row
                {
                    byte mem = RAM[I + i];                              // the location drawing starts
                    for (int j = 0; j < 8; j++)                         // loop over each of the 8-bits
                    {   
                        byte px = (byte)((mem >> (7 - j)) & 0x01);      // the pixel we want to draw

                        int  di = x + j + (y + i) * 64;                 // index on the display grid
                        if (di > 2047) continue;                        // ignoring any out of bounds
                        
                        if (px == 1 && Display[di] != 0) V[15] = 1;     // if any pixels will flip off set flag
                        Display[di] = (byte)(Display[di] ^ px);         // flip the pixel on the display
                    }
                }
                DrawDisplay();
                break;

            case 0xE000: // Keyboard input opcodes in E, range
                switch (opcode & 0x00FF)
                {
                    case 0x009E:
                        if (((Keyboard >> V[(opcode & 0x0F00) >> 8]) & 0x01) == 0x01) PC += 2;
                        break;
                    case 0x00A1:
                        if (((Keyboard >> V[(opcode & 0x0F00) >> 8]) & 0x01) != 0x01) PC += 2;
                        break;

                    default: throw new Exception($"Unsupported opcode {opcode:x4}");
                }
                break;

            case 0xF000: // Additional opcodes switching on lowest byte
                int tx = (opcode & 0x0F00) >> 8;
                switch (opcode & 0x00FF)
                {
                    case 0x07: V[tx] = DelayTimer; break;
                    case 0x0A: _awaitingInput = true; PC -= 2; break;
                    case 0x15: DelayTimer = V[tx]; break;
                    case 0x18: SoundTimer = V[tx]; break;
                    case 0x1E: I = (ushort)(I + V[tx]); break;
                    case 0x29: I = (ushort)(V[tx * 5]); break;
                    case 0x33:
                        RAM[I]     = (byte)(V[tx] / 100);
                        RAM[I + 1] = (byte)((V[tx] % 100) / 10);
                        RAM[I + 2] = (byte)(V[tx % 10]);
                        break;
                    case 0x55:
                        for (int i = 0; i <= tx; i++) RAM[I + i] = V[i];
                        break;
                    case 0x65:
                        for (int i = 0; i <= tx; i++) V[i] = RAM[I + i];
                        break;
                }
                break;

            default: throw new Exception($"Unsupported opcode {opcode:x4}");
        }
    }

    public void DrawDisplay()
    {
        Console.Clear();
        Console.SetCursorPosition(0, 0);
        for (int y = 0; y < 32; y++)
        {
            string line = "";
            for (int x = 0; x < 64; x++)
            {
                line += (Display[x + y * 64] != 0) ? "*" : " "; 
            }
            Console.WriteLine(line);
        }
        Thread.Sleep(5);
    }
}