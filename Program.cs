using System.Reflection.Emit;

namespace CHIP_8;

public static class Program
{
    public static void Main(string[] args)
    {
        var chip8 = new CHIP8();
        using (var reader = new BinaryReader(new FileStream("../../../ROMs/MISSILE", FileMode.Open)))
        {
            List<byte> program = [];
            while (reader.BaseStream.Position != reader.BaseStream.Length - 1)
            {
                /// CHIP8 Programs are stored in big endian, which means when we read
                /// in the opcodes they appear in reverse byte order from what we want
                /// to operate on, so we shift what's read to get little endian.
                //program.Add((ushort)((reader.ReadByte() << 8) | reader.ReadByte()));
                program.Add(reader.ReadByte());
            }


            chip8.LoadProgram(program.ToArray());
            //try
            {
                while (true) chip8.Step();
            }
            //catch (Exception e) { Console.WriteLine(e.Message); }
        }
    }
}