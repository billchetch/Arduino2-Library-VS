using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2.Devices.Motors
{
    public class ServoController : ArduinoDevice
    {
        public const String DEFAULT_NAME = "SERVO";
        
        public byte Pin { get; internal set; }

        [ArduinoProperty(ArduinoPropertyAttribute.STATE | PropertyAttribute.SERIALIZABLE)]
        public int Position { get; set; } = 90;
        public int LowerBound { get; set; } = 0;
        public int UpperBound { get; set; } = 180;

        public int TrimFactor { get; set; } = 0;
        public int RotationalSpeed { get; set; } = 300; //in degrees per second

        public ServoController(String id, byte pin, String name = DEFAULT_NAME) : base(id, name)
        {
            Pin = pin;
            Category = DeviceCategory.SERVO;

            AddCommand(ArduinoCommand.DeviceCommand.MOVE, ArduinoCommand.ParameterType.INT);
            AddCommand(ArduinoCommand.DeviceCommand.ROTATE, ArduinoCommand.ParameterType.INT);
        }
    

        protected override void AddConfig(ADMMessage message)
        {
            base.AddConfig(message);

            message.AddArgument(Pin);
            message.AddArgument(Position);
            message.AddArgument(LowerBound);
            message.AddArgument(UpperBound);
            message.AddArgument(TrimFactor);
            message.AddArgument(RotationalSpeed);
        }

        override protected int GetArgumentIndex(String fieldName, ADMMessage message)
        {
            switch (fieldName)
            {
                case "Position":
                    return message.IsCommandRelated ? 1 : base.GetArgumentIndex(fieldName, message);

                default:
                    return base.GetArgumentIndex(fieldName, message);
            }
        }

        public void MoveTo(int pos)
        {
            ExecuteCommand(ArduinoCommand.DeviceCommand.MOVE, pos);
        }

        public void RotateBy(int increment)
        {
            ExecuteCommand(ArduinoCommand.DeviceCommand.ROTATE, increment);
        }

        public override void HandleMessage(ADMMessage message)
        {
            base.HandleMessage(message);
        }

        protected override bool HandleCommandResponse(ArduinoCommand.DeviceCommand deviceCommand, ADMMessage message)
        {
            switch (deviceCommand)
            {
                case ArduinoCommand.DeviceCommand.MOVE:
                case ArduinoCommand.DeviceCommand.ROTATE:
                    AssignMessageValues(message, "Position");
                    break;
            }
            return base.HandleCommandResponse(deviceCommand, message);
        }

    }
}
