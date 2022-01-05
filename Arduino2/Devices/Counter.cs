using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Messaging;

namespace Chetch.Arduino2.Devices
{
    public class Counter : ArduinoDevice
    {
        public const String DEFAULT_NAME = "Counter";

        public byte Pin { get; internal set; }

        [ArduinoProperty(ArduinoPropertyAttribute.DATA, 0)]
        public uint Count
        {
            get { return Get<uint>(); }
            internal set { Set(value, IsReady); }
        }

        public InterruptMode IMode { get; internal set; }

        public uint Tolerance { get; set; } = 0;

        public Counter(String id, byte pin, InterruptMode imode, String name = DEFAULT_NAME) : base(id, name)
        {
            Pin = pin;
            IMode = imode;

            Category = DeviceCategory.COUNTER;
        }

        protected override void AddConfig(ADMMessage message)
        {
            base.AddConfig(message);

            message.AddArgument(Pin);
            message.AddArgument((byte)IMode);
            message.AddArgument(Tolerance);
        }

        override protected int GetArgumentIndex(String fieldName, ADMMessage message)
        {
            switch (fieldName)
            {
                case "Count":
                    return 0;

                case "Duration":
                    return 1;

                default:
                    return base.GetArgumentIndex(fieldName, message);
            }
        }

        override public void HandleMessage(ADMMessage message)
        {
            switch (message.Type)
            {
                case MessageType.DATA:
                    AssignMessageValues(message, "Count");
                    ulong duration = GetMessageValue<ulong>("Duration", message);
                    Console.WriteLine("Count {0}, Duration {1}", Count, duration);
                    break;

                case MessageType.CONFIGURE_RESPONSE:
                    break;
            }

            base.HandleMessage(message);
        }
    }
}
