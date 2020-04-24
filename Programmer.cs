using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using ArduinoDriver;
using ArduinoDriver.SerialProtocol;
using ArduinoUploader.Hardware;
using Driver = ArduinoDriver.ArduinoDriver;

namespace EEPROM_Programmer
{
    public abstract class Programmer : Driver
    {
        /// <summary>
        /// Pin layout for the EEPROM programmer
        /// </summary>
        public enum Pins : byte
        {
            SHIFT_DATA    = 2,
            SHIFT_CLOCK   = 3,
            LATCH_CLOCK   = 4,
            DATA0         = 5,
            DATA1         = 6,
            DATA2         = 7,
            DATA3         = 8,
            DATA4         = 9,
            DATA5         = 10,
            DATA6         = 11,
            DATA7         = 12,
            WRITE_ENABLE  = 13,
            OUTPUT_ENABLE = 14
        }

        #region Constants
        /// <summary>
        /// Default value of the EEPROM
        /// </summary>
        private const byte DEFAULT = 0xFF;
        /// <summary>
        /// Nanoseconds per seconds
        /// </summary>
        private const long NANOS_PER_SECOND = 1000000000L;
        /// <summary>
        /// Nanoseconds per microsecond
        /// </summary>
        private const long NANOS_PER_MICROSECOND = 1000L;
        /// <summary>
        /// Size of the EEPROM in bytes
        /// </summary>
        private const ushort SIZE = 2048;
        #endregion

        #region Static properties
        /// <summary>
        /// Maximum resolution of the clock, amount of nanoseconds per clock tick
        /// </summary>
        public static long NanosecondsPerTick { get; } = NANOS_PER_SECOND / Stopwatch.Frequency;
        #endregion

        #region Fields
        private readonly Stopwatch timer = new Stopwatch();
        #endregion

        #region Properties
        private bool outputEnabled;
        /// <summary>
        /// OutputEnable flag of the EEPROM
        /// </summary>
        protected bool OutputEnabled
        {
            get => this.outputEnabled;
            set
            {
                if (this.outputEnabled != value)
                {
                    this.outputEnabled = value;
                    DigitalWrite(Pins.OUTPUT_ENABLE, value ? DigitalValue.Low : DigitalValue.High);
                }
            }
        }

        private ushort currentAddress = ushort.MaxValue;
        /// <summary>
        /// Current address of the EEPROM
        /// </summary>
        protected ushort Address
        {
            get => this.currentAddress;
            set
            {
                if (this.currentAddress != value)
                {
                    //Set address in shift registers
                    this.currentAddress = value;
                    ShiftOut(Pins.SHIFT_DATA, Pins.SHIFT_CLOCK, HighByte(value));
                    ShiftOut(Pins.SHIFT_DATA, Pins.SHIFT_CLOCK, LowByte(value));

                    //Pulse latch clock
                    DigitalWrite(Pins.LATCH_CLOCK, DigitalValue.High);
                    DelayMicroseconds(10L);
                    DigitalWrite(Pins.LATCH_CLOCK, DigitalValue.Low);
                }
            }
        }

        private PinMode currentMode = PinMode.Input;
        /// <summary>
        /// Pin mode of the data pins
        /// </summary>
        protected PinMode Mode
        {
            get => this.currentMode;
            set
            {
                if (this.currentMode != value)
                {
                    //Set mode for all pins
                    this.currentMode = value;
                    for (Pins pin = Pins.DATA0; pin <= Pins.DATA7; pin++)
                    {
                        SetPinMode(pin, value);
                    }
                }
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new EEPROM Programmer on the default port
        /// </summary>
        protected Programmer() : base(ArduinoModel.NanoR3, true) => Setup();

        /// <summary>
        /// Creates a new EEPROM Programmer on the specified port
        /// </summary>
        /// <param name="portName">Name of the port to connect to</param>
        protected Programmer(string portName) : base(ArduinoModel.NanoR3, portName, true) => Setup();
        #endregion

        #region Static methods
        /// <summary>
        /// Gets the higher byte of an unsigned short value
        /// </summary>
        /// <param name="value">Value to get the high byte from</param>
        /// <returns>The high order byte of the short</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte HighByte(ushort value) => (byte)(value >> 8);

        /// <summary>
        /// Gets the lower byte of an unsigned short value
        /// </summary>
        /// <param name="value">Value to get the low byte from</param>
        /// <returns>The low order byte of the short</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte LowByte(ushort value) => (byte)(value & 0xFF);
        #endregion

        #region Abstract methods
        /// <summary>
        /// Runs the EEPROM Programmer program
        /// </summary>
        public abstract void Run();
        #endregion

        #region Methods
        /// <summary>
        /// Sets up pins for EEPROM programming
        /// </summary>
        private void Setup()
        {
            //Notify connection
            Console.WriteLine("Connected to Arduino board");

            //Setup pin modes
            SetPinMode(Pins.SHIFT_DATA,  PinMode.Output);
            SetPinMode(Pins.SHIFT_CLOCK, PinMode.Output);
            SetPinMode(Pins.LATCH_CLOCK, PinMode.Output);

            //Setup write enable pin
            SetPinMode(Pins.WRITE_ENABLE, PinMode.Output);
            DigitalWrite(Pins.WRITE_ENABLE, DigitalValue.High);

            //Setup output enable pin
            SetPinMode(Pins.OUTPUT_ENABLE, PinMode.Output);
            this.OutputEnabled = true;
        }

        /// <summary>
        /// Reads the value in the EEPROM at the specified address
        /// </summary>
        /// <param name="address">Address to read at</param>
        /// <returns>The value stored in the EEPROM at the specified address</returns>
        protected byte ReadAtAddress(ushort address)
        {
            //Set address
            this.Address = address;
            this.Mode = PinMode.Input;
            this.OutputEnabled = true;

            //Get value from each pin
            int value = 0;
            for (Pins pin = Pins.DATA7; pin >= Pins.DATA0; pin--)
            {
                value = (value << 1) | DigitalRead(pin);
            }

            //Return read value as a byte
            return (byte)value;
        }

        /// <summary>
        /// Writes the given value to the EEPROM at the specified address
        /// </summary>
        /// <param name="address">Address to write the value at</param>
        /// <param name="value">Value to write to the EEPROM</param>
        protected void WriteAtAddress(ushort address, byte value)
        {
            //Set address and pin mode
            this.Address = address;
            this.OutputEnabled = false;
            this.Mode = PinMode.Output;

            //Keep reference to MSB
            int msb = value >> 7;

            //Write to all pins
            for (Pins pin = Pins.DATA0; pin <= Pins.DATA7; pin++)
            {
                DigitalWrite(pin, (DigitalValue)(value & 1));
                value >>= 1;
            }

            //Pulse write signal
            DigitalWrite(Pins.WRITE_ENABLE, DigitalValue.Low);
            //DelayNanoseconds(500L);
            DigitalWrite(Pins.WRITE_ENABLE, DigitalValue.High);
            //DelayMicroseconds(2200);

            //Check for write end using data polling
            SetPinMode(Pins.DATA7, PinMode.Input);
            this.OutputEnabled = true;
            while (DigitalRead(Pins.DATA7) != msb) { }
            SetPinMode(Pins.DATA7, PinMode.Output);
        }

        /// <summary>
        /// Writes the given data to the EEPROM, starting at address 0
        /// </summary>
        /// <param name="data">Data to write to the EEPROM</param>
        /// <exception cref="ArgumentOutOfRangeException">If the data to write exceeds the size of the EEPROM</exception>
        protected void WriteData(byte[] data)
        {
            //Check write size
            if (data.Length > SIZE) throw new ArgumentOutOfRangeException(nameof(data), data.Length, "Written data must fit within the EEPROM (max 2Kb)");

            Console.WriteLine("Writing Data");
            //Write sequentially to the EEPROM
            for (ushort address = 0; address < data.Length; address++)
            {
                WriteAtAddress(address, data[address]);
            }
        }

        /// <summary>
        /// Prints the content of
        /// </summary>
        /// <param name="lines"></param>
        protected void PrintContent(int lines = 16)
        {
            //Useful printing constants
            const ushort stride = 16;
            const ushort maxLines = SIZE / stride;
            const int lineSize = (3 * stride) + 8;

            Console.WriteLine("Printing data");
            //Setup for writing
            lines = Math.Min(Math.Max(lines, 1), maxLines);
            int size = lines * stride;
            StringBuilder sb = new StringBuilder(lineSize * lines);
            //Go by line
            for (ushort i = 0; i < size; i += stride)
            {
                //Write line number
                sb.Append($"{i:X3}: ");
                //Read each value on the line
                for (ushort j = 0; j < stride; j++)
                {
                    sb.Append($" {ReadAtAddress((ushort)(i + j)):X2}{(j == 7 ? " " : string.Empty)}");
                }
                sb.AppendLine();
            }

            //Write results
            Console.Write(sb);
        }

        /// <summary>
        /// Clears the data in the EEPROM up to the specified byte by resetting it to the default value 0xFF
        /// </summary>
        /// <param name="bytes">Amount of bytes to clear, up to the maximum size of the EEPROM</param>
        protected void Clear(ushort bytes = 256)
        {
            //Get amount of bytes written
            bytes = Math.Min(bytes, SIZE);
            //Reset all bytes to their default value
            for (ushort address = 0; address < bytes; address++)
            {
                WriteAtAddress(address, DEFAULT);
            }
        }
        #endregion

        #region Arduino methods
        /// <summary>
        /// Waits for a specified delay in nanoseconds <para/>
        /// CAREFUL: Please take note of the maximum resolution of the clock (typically 100ns/tick)<para/>
        /// Accuracy is not guaranteed under 500ns
        /// </summary>
        /// <param name="nanoseconds">Amount of nanoseconds to wait</param>
        protected void DelayNanoseconds(long nanoseconds)
        {
            //Start timer and wait for target tick amount
            this.timer.Restart();
            long targetTicks = nanoseconds / NanosecondsPerTick;
            while (this.timer.ElapsedTicks < targetTicks)
            {
                Thread.SpinWait(10);
            }
        }

        /// <summary>
        /// Waits for a specified delay in microseconds
        /// </summary>
        /// <param name="microseconds">Amount of microseconds to wait</param>
        protected void DelayMicroseconds(long microseconds)
        {
            //Start the timer and wait for target tick amount
            this.timer.Restart();
            long targetTicks = (microseconds * NANOS_PER_MICROSECOND) / NanosecondsPerTick;
            while (this.timer.ElapsedTicks < targetTicks)
            {
                Thread.SpinWait(10);
            }
        }

        /// <summary>
        /// Waits for a specified delay in milliseconds by sleeping the current thread
        /// </summary>
        /// <param name="milliseconds">Amount of milliseconds to wait</param>
        protected void Delay(int milliseconds) => Thread.Sleep(milliseconds);

        /// <summary>
        /// Sets the pin mode of the specified pin
        /// </summary>
        /// <param name="pin">Pin to change</param>
        /// <param name="mode">Pin mode to set</param>
        protected void SetPinMode(Pins pin, PinMode mode) => Send(new PinModeRequest((byte)pin, mode));

        /// <summary>
        /// Shifts data out through the specified pins
        /// </summary>
        /// <param name="dataPin">Data pin</param>
        /// <param name="clockPin">Clock pin</param>
        /// <param name="data">Data to send</param>
        protected void ShiftOut(Pins dataPin, Pins clockPin, byte data) => Send(new ShiftOutRequest((byte)dataPin, (byte)clockPin, BitOrder.MSBFIRST, data));

        /// <summary>
        /// Writes high or low to the specified pin
        /// </summary>
        /// <param name="pin">Pin to write to</param>
        /// <param name="value">Value to write</param>
        protected void DigitalWrite(Pins pin, DigitalValue value) => Send(new DigitalWriteRequest((byte)pin, value));

        /// <summary>
        /// Reads the value on the specified pin
        /// </summary>
        /// <param name="pin">Pin to read from</param>
        /// <returns>The value on the pin, 0 for low, 1 for high</returns>
        protected int DigitalRead(Pins pin) => (int)Send(new DigitalReadRequest((byte)pin)).PinValue;
        #endregion
    }
}
