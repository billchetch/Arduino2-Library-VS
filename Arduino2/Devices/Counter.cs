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

        public InterruptMode IMode { get; internal set; }

        public uint Tolerance { get; set; } = 0;

        [ArduinoProperty(ArduinoPropertyAttribute.DATA, 0)]
        public uint Count
        {
            get { return Get<uint>(); }
            internal set { Set(value, IsReady); }
        }

        public double CountPerSecond
        {
            get { return Get<double>(); }
            internal set { Set(value, IsReady); }
        }


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
                    uint count = GetMessageValue<uint>("Count", message);
                    ulong duration = GetMessageValue<ulong>("Duration", message); //in micros
                    if(Tolerance > 0)
                    {
                        duration = (ulong)Math.Round((double)duration / (double)Tolerance) * Tolerance;
                    }

                    //so we set standard rate here
                    CountPerSecond = (1000000.0 / duration) * count;
                    
                    AssignMessageValues(message, "Count");
                    Console.WriteLine("{0}: Count {1}, Duration {2}, CPS {3}", UID, Count, duration, CountPerSecond);
                    break;
            }

            base.HandleMessage(message);
        }
    }
}
