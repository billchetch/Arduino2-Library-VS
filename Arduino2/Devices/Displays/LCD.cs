using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2.Devices.Displays
{
    public class LCD : ArduinoDevice
    {
        public const String DEFAULT_NAME = "LCD";

        public enum DisplayDimensions
        {
            D16x2 = 1,
        }

        public enum DataPinSequence
        {
            Pins_5_2 = 1,
            Pins_2_5 = 2,
        }

        public DataPinSequence DataPins { get; internal set; }
        public byte EnablePin { get; internal set; }
        public byte RegisterSelectPin { get; internal set; }

        public DisplayDimensions Dimensions { get; set; } = DisplayDimensions.D16x2;

        public LCD(String id, DataPinSequence dataPins, byte enablePin, byte regSelectPin, String name = DEFAULT_NAME) : base(id, name)
        {
            DataPins = dataPins;
            EnablePin = enablePin;
            RegisterSelectPin = regSelectPin;

            AddCommand(ArduinoCommand.DeviceCommand.PRINT, ArduinoCommand.ParameterType.STRING);
            AddCommand(ArduinoCommand.DeviceCommand.CLEAR);
            AddCommand(ArduinoCommand.DeviceCommand.SET_CURSOR, ArduinoCommand.ParameterType.INT, ArduinoCommand.ParameterType.INT);


            Category = DeviceCategory.DISPLAY;
        }

        protected override void AddConfig(ADMMessage message)
        {
            base.AddConfig(message);

            message.AddArgument((byte)DataPins);
            message.AddArgument(EnablePin);
            message.AddArgument(RegisterSelectPin);
            message.AddArgument((byte)Dimensions);
        }

        public void Print(String str)
        {
            ExecuteCommand(ArduinoCommand.DeviceCommand.PRINT, str);
        }

        public void Clear()
        {
            ExecuteCommand(ArduinoCommand.DeviceCommand.CLEAR);
        }

        public void SetCursor(int x, int y)
        {
            ExecuteCommand(ArduinoCommand.DeviceCommand.SET_CURSOR, x, y);
        }
    }
}
