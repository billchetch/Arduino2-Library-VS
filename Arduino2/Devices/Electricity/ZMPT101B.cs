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
        public const int DEFAULT_SAMPLE_SIZE = 2000;
        
        public enum Target
        {
            NONE = 0,
            VOLTAGE = 1,
            HZ = 2,
        }

        public AnalogPin Pin { get; internal set; }

        public int SampleSize { get; set; } = DEFAULT_SAMPLE_SIZE;

        public Target Targeting { get; internal set; }  = Target.NONE;
        private int _targetValue = -1; //if < 0 then no stabalising is required
        private int _targetTolerance = 0;
        private int _targetLowerBound = 0;
        private int _targetUpperBound = -1;

        [ArduinoProperty(ArduinoPropertyAttribute.DATA, 0)]
        public float Voltage
        {
            get { return Get<float>(); }
            internal set { Set(value, IsReady, IsReady); } //Note: this will fire a property change even if no value change
        }

        [ArduinoProperty(ArduinoPropertyAttribute.DATA, 0)]
        public float Hz
        {
            get { return Get<float>(); }
            internal set { Set(value, IsReady); }
        }

        [ArduinoProperty(ArduinoPropertyAttribute.STATE, 0)]
        public float Adjustment
        {
            get { return Get<float>(); }
            internal set { Set(value, IsReady); }
        }

        public ZMPT101B(String id, AnalogPin pin, String name = DEFAULT_NAME) : base(id,name)
        {
            Pin = pin;
            Category = DeviceCategory.VAC_SENSOR;
        }

        public void SetTargetParameterse(Target target, int targetValue, int targetTolerance, int targetLowerBound = 0, int targetUpperBound = -1)
        {
            Targeting = target;
            _targetValue = targetValue;
            _targetTolerance = targetTolerance;
            _targetLowerBound = targetLowerBound;
            _targetUpperBound = targetUpperBound;
        }

        protected override void AddConfig(ADMMessage message)
        {
            base.AddConfig(message);

            message.AddArgument((byte)Pin);
            message.AddArgument(SampleSize);
            message.AddArgument((byte)Targeting);
            message.AddArgument(_targetValue);
            message.AddArgument(_targetTolerance);
            message.AddArgument(_targetLowerBound);
            message.AddArgument(_targetUpperBound);
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

                case "Adjustment":
                    return 0;

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

                case MessageType.WARNING:
                    AssignMessageValues(message, "Adjustment");
                    break;
            }

            base.HandleMessage(message);
        }
    }
}
