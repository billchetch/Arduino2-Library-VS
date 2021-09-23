using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Messaging;

namespace Chetch.Arduino2.Devices
{
    public class SwitchDevice : ArduinoDevice
    {
        public const String DEFAULT_NAME = "SWITCH";

        public enum SwitchMode
        {
            ACTIVE = 1,
            PASSIVE
        }

        public enum SwitchPosition
        {
            OFF = 0,
            ON = 1,
        }

        public SwitchMode Mode { get; internal set; }

        [ArduinoProperty(ArduinoPropertyAttribute.STATE | ArduinoPropertyAttribute.DATA)]
        public SwitchPosition Position 
        {
            get { return Get<SwitchPosition>(); }
            internal set { Set(value, IsReady); }
        }

        private bool _pinState;
        public bool PinState 
        { 
            get { return _pinState; } 
            internal set 
            {
                _pinState = value;
                Position = _pinState ? SwitchPosition.ON : SwitchPosition.OFF;
            } 
        }

        public byte Pin { get; internal set; }

        public int Tolerance { get; internal set; } = 0;

        public SwitchDevice(String id, SwitchMode mode, byte pin, SwitchPosition intialPosition = SwitchPosition.OFF, int tolerance = 0, String name = DEFAULT_NAME) : base(id, name)
        {
            Mode = mode;
            Pin = pin;
            PinState = (intialPosition == SwitchPosition.ON);
            Tolerance = tolerance;
            Category = DeviceCategory.SWITCH;

            if (Mode == SwitchMode.ACTIVE)
            { 
                AddCommand(ArduinoCommand.DeviceCommand.ON);
                AddCommand(ArduinoCommand.DeviceCommand.OFF);
            }
        }

        override protected int GetArgumentIndex(String fieldName, ADMMessage message)
        {
            switch (fieldName)
            {
                case "PinState":
                    return message.IsConfigRelated ? 4 : (message.IsCommandRelated? 1 : 0);
               
                default:
                    return base.GetArgumentIndex(fieldName, message);
            }
        }

        protected override void AddConfig(ADMMessage message)
        {
            base.AddConfig(message);

            message.AddArgument((byte)Mode);
            message.AddArgument(Pin);
            message.AddArgument(PinState);
            message.AddArgument(Tolerance);
        }

        public override void HandleMessage(ADMMessage message)
        {
            switch (message.Type)
            {
                case MessageType.DATA:
                    AssignMessageValues(message, "PinState");
                    break;
            }

            base.HandleMessage(message);
        }



        protected override void HandleCommandResponse(ArduinoCommand.DeviceCommand deviceCommand, ADMMessage message)
        {
            switch(deviceCommand)
            {
                case ArduinoCommand.DeviceCommand.ON:
                case ArduinoCommand.DeviceCommand.OFF:
                    AssignMessageValues(message, "PinState");
                    break;
            }
            base.HandleCommandResponse(deviceCommand, message);
        }
    }
}
