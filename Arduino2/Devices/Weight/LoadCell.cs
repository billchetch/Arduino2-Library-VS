using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Messaging;

namespace Chetch.Arduino2.Devices.Weight
{
    public class LoadCell : ArduinoDevice
    {
        public const String DEFAULT_NAME = "LOADCELL";

        private int _doutPin;

        private int _sckPin;

        public int ReadInterval { get; set; } = 500; //in millis (less than 100 and the device does not normally have time reset)

        public int SampleSize { get; set; } = 1; //how man samples to take before averaging the read value


        public double Scale { get; set; } = 1.0;

        public long Offset { get; set; } = 0;

        public long RawValue { get; internal set; } = 0;

        [ArduinoProperty(ArduinoPropertyAttribute.DATA, 0)]
        public double Weight
        {
            get { return Get<double>(); }
            internal set { Set(value, IsReady, IsReady); } //Note: this will fire a property change even if no value change
        }

        public LoadCell(String id, int doutPin, int sckPin, String name = DEFAULT_NAME) : base(id, name)
        {
            _doutPin = doutPin;
            _sckPin = sckPin;

            Category = DeviceCategory.WEIGHT_SENSOR;
        }

        protected override void AddConfig(ADMMessage message)
        {
            base.AddConfig(message);

            message.AddArgument(_doutPin);
            message.AddArgument(_sckPin);
            message.AddArgument(ReadInterval);
            message.AddArgument(SampleSize);
        }


        override protected int GetArgumentIndex(String fieldName, ADMMessage message)
        {
            switch (fieldName)
            {
                case "Reading":
                    return 0;

                case "MaxDiff":
                    return 1;

                case "ReadCount":
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
                    RawValue = GetMessageValue<long>("Reading", message);
                    //this triggers listeners
                    double w = (RawValue - Offset) / Scale;
                    Weight = Math.Max(w, 0);
                    break;

                case MessageType.WARNING:
                    break;

                case MessageType.CONFIGURE_RESPONSE:
                    break;
            }

            base.HandleMessage(message);
        }
    }
}
