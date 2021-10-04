using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Messaging;

namespace Chetch.Arduino2.Devices.Electricity
{
    public class ZMPT101B : ArduinoDevice
    {
        public const String DEFAULT_NAME = "ZMPT101B";
        public const int DEFAULT_SAMPLE_SIZE = 500;
        public const int DEFAULT_SAMPLE_INTERVAL = 1000; //Note: IN MICROS!
        
        public byte Pin { get; internal set; }

        public int SampleSize { get; set; } = DEFAULT_SAMPLE_SIZE;

        public int SampleInterval { get; set; } = DEFAULT_SAMPLE_INTERVAL;

        private int _targetVoltage = -1; //if < 0 then no stabalising is required
        private int _targetTolerance = 0;
        private int _voltageLowerBound = 0;
        private int _voltageUpperBound = -1;

        [ArduinoProperty(ArduinoPropertyAttribute.DATA, 0)]
        public float Voltage
        {
            get { return Get<float>(); }
            internal set { Set(value, IsReady); }
        }

        [ArduinoProperty(ArduinoPropertyAttribute.DATA, 0)]
        public float Hz
        {
            get { return Get<float>(); }
            internal set { Set(value, IsReady); }
        }

        public ZMPT101B(String id, byte pin, String name = DEFAULT_NAME) : base(id,name)
        {
            Pin = pin;
            Category = DeviceCategory.VAC_SENSOR;
        }

        public void SetTargetVoltge(int targetVoltage, int targetTolerance, int voltageLowerBound = 0, int voltageUpperBound = -1)
        {
            _targetVoltage = targetVoltage;
            _targetTolerance = targetTolerance;
            _voltageLowerBound = voltageLowerBound;
            _voltageUpperBound = voltageUpperBound;
        }

        protected override void AddConfig(ADMMessage message)
        {
            base.AddConfig(message);

            message.AddArgument(Pin);
            message.AddArgument(SampleSize);
            message.AddArgument(SampleInterval);
            /*message.AddArgument(_targetVoltage);
            message.AddArgument(_targetTolerance);
            message.AddArgument(_voltageLowerBound);
            message.AddArgument(_voltageUpperBound);*/
            //message.AddArgument(); //Scale wave form:
            //message.AddArgument(); //Final offset:

        }

        override protected int GetArgumentIndex(String fieldName, ADMMessage message)
        {
            switch (fieldName)
            {
                case "Voltage":
                    return 0;

                case "Hz":
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
                    AssignMessageValues(message, "Voltage", "Hz");
                    break;
            }

            base.HandleMessage(message);
        }
    }
}
