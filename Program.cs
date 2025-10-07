namespace CHIP_8;

public static class Program
{
    public static void Main(string[] args)
    {
        using (var reader = new BinaryReader(new FileStream("HeartMonitor.ch8", FileMode.Open)))
        {
            while (reader.BaseStream.Position != reader.BaseStream.Length - 1)
            {
                /// CHIP8 Programs are stored in big endian, which means when we read
                /// in the opcodes they appear in reverse byte order from what we want
                /// to operate on, so we shift what's read to get little endian.
                var opcode = (ushort)((reader.ReadByte() << 8) | reader.ReadByte());
                Console.WriteLine($"{opcode:X4}");
            }
        }

        Console.ReadKey();
    }
}