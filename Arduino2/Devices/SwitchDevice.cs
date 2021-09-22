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

        public enum Mode
        {
            ACTIVE = 1,
            PASSIVE
        }

        private Mode _mode;
        private bool _state;

        [ArduinoProperty(PropertyAttribute.SERIALIZABLE, 0)]
        public byte Pin { get; internal set; }

        public SwitchDevice(String id, Mode mode, byte pin, bool initialState = false, String name = DEFAULT_NAME) : base(id, name)
        {
            _mode = mode;
            _state = initialState;
            Pin = pin;
            Category = DeviceCategory.SWITCH;
        }

        override protected int GetArgumentIndex(String fieldName, ADMMessage message)
        {
            switch (fieldName)
            {
                case "Pin":
                    return (message.Type == MessageType.COMMAND_RESPONSE || message.Type == MessageType.COMMAND) ? 1 : 0;

                default:
                    return base.GetArgumentIndex(fieldName, message);
            }
        }

        protected override void AddConfig(ADMMessage message)
        {
            base.AddConfig(message);

            message.AddArgument((byte)_mode);
            message.AddArgument(Pin);
            message.AddArgument(_state);
        }
    }
}
