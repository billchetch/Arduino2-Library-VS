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

        public bool PinStateToCount { get; set; }

        [ArduinoProperty(ArduinoPropertyAttribute.DATA, 0)]
        public uint Count
        {
            get { return Get<uint>(); }
            internal set { Set(value, IsReady, ReportInterval > 0); }
        }

        public uint CountDuration { get; internal set; } = 0;

        public uint IntervalDuration { get; internal set; } = 0;

        public double AverageInterval { get; internal set; } = 0;

        [ArduinoProperty(ArduinoPropertyAttribute.DATA, 0)]
        public double CountPerSecond
        {
            get { return Get<double>(); }
            internal set { Set(value, IsReady, ReportInterval > 0); }
        }

        public double IntervalsPerSecond { get; internal set; } = 0;

        public ulong TotalCount { get; internal set; } = 0;
        
        public Counter(String id, byte pin, InterruptMode imode, int pinStateToCount = -1, String name = DEFAULT_NAME) : base(id, name)
        {
            Pin = pin;
            IMode = imode;

            if (pinStateToCount == -1) {
                switch (IMode)
                {
                    case InterruptMode.FALLING:
                        PinStateToCount = false; break;

                    case InterruptMode.RISING:
                        PinStateToCount = true; break;
                }
            } 
            else 
            {
                PinStateToCount = pinStateToCount > 0;
            }

            Category = DeviceCategory.COUNTER;
        }

        protected override void AddConfig(ADMMessage message)
        {
            base.AddConfig(message);

            message.AddArgument(Pin);
            message.AddArgument((byte)IMode);
            message.AddArgument(Tolerance);
            message.AddArgument(PinStateToCount);

        }

        override protected int GetArgumentIndex(String fieldName, ADMMessage message)
        {
            switch (fieldName)
            {
                case "Count":
                    return 0;

                case "CountDuration": //this is the time (micros) between starting counting and ending counting 
                    return 1;

                case "IntervalDuration": //this is the time (micros) between the first and last count
                    return 2; 

                default:
                    return base.GetArgumentIndex(fieldName, message);
            }
        }

        override public void HandleMessage(ADMMessage message)
        {
            switch (message.Type)
            {
                case MessageType.DATA:
                    AssignMessageValues(message, "Count", "CountDuration", "IntervalDuration");
                    if(Tolerance > 0)
                    {
                        CountDuration = (uint)Math.Round((double)CountDuration / (double)Tolerance) * Tolerance;
                    }
                    try
                    {
                        TotalCount += Count;
                    } catch (Exception)
                    {
                        //overflow
                    }
                    
                    //so we set standard rate here
                    CountPerSecond = CountDuration == 0 ? 0 : (1000000.0 / (double)CountDuration) * Count;

                    AverageInterval = Count > 1 ? (double)IntervalDuration / (double)(Count - 1) : 0;

                    IntervalsPerSecond = AverageInterval > 0 ? (1000000.0 / AverageInterval) : 0;

                    //Console.WriteLine("{0}: Count {1}, Duration {2}, CPS {3}", UID, Count, duration, CountPerSecond);
                    break;
            }

            base.HandleMessage(message);
        }
    }
}
