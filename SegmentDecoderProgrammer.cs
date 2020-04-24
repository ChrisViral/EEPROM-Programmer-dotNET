
namespace EEPROM_Programmer
{
    public class SegmentDecoderProgrammer : Programmer
    {
        #region Constants
        /// <summary>
        /// Seven Segments decoder data
        /// </summary>
        private static readonly byte[] Data = { 0x01, 0x4F, 0x12, 0x06, 0x4C, 0x24, 0x20, 0x0F, 0x00, 0x04, 0x08, 0x60, 0x31, 0x42, 0x30, 0x38 };
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new SegmentDecoderProgrammer on the default port
        /// </summary>
        public SegmentDecoderProgrammer() { }

        /// <summary>
        /// Creates a new SegmentDecoderProgrammer on the specified port
        /// </summary>
        /// <param name="portName">Name of the port to connect to</param>
        public SegmentDecoderProgrammer(string portName) : base(portName) { }
        #endregion

        #region Methods
        /// <summary>
        /// Runs the Seven Segments decoder programmer
        /// </summary>
        public override void Run()
        {
            WriteData(Data);
            PrintContent(1);
        }
        #endregion
    }
}
