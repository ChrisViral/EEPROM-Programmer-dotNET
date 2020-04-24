using System;

namespace EEPROM_Programmer
{
    internal static class Program
    {
        private static void Main()
        {
            try
            {
                using SegmentDecoderProgrammer programmer = new SegmentDecoderProgrammer("COM4");
                programmer.Run();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[{e.GetType().Name}]: {e.Message}\n{e.StackTrace}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey(true);
        }
    }
}
