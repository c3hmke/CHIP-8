namespace CHIP_8;

public static class Program
{
    public static void Main(string[] args)
    {
        var chip8 = new CHIP8();
        using (var reader = new BinaryReader(new FileStream("HeartMonitor.ch8", FileMode.Open)))
        {
            while (reader.BaseStream.Position != reader.BaseStream.Length - 1)
            {
                /// CHIP8 Programs are stored in big endian, which means when we read
                /// in the opcodes they appear in reverse byte order from what we want
                /// to operate on, so we shift what's read to get little endian.
                var opcode = (ushort)((reader.ReadByte() << 8) | reader.ReadByte());

                try
                {
                    chip8.Step(opcode);
                }
                catch (Exception e) { Console.WriteLine(e.Message); }
            }
        }

        Console.ReadKey();
    }
}