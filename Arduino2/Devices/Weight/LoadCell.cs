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

        private byte _doutPin;

        private byte _sckPin;


        public int ReadInterval { get; set; } = 500; //in millis (less than 100 and the device does not normally have time reset)

        public int SampleSize { get; set; } = 1; //how man samples to take before averaging the read value


        public double Scale { get; set; } = 1.0;

        public long Offset { get; set; } = 0;

        public Int32 RawValue { get; internal set; } = 0;

        public int MinWeight { get; set; } = 0;

        public int MaxWeight { get; set; } = 1000;

        [ArduinoProperty(ArduinoPropertyAttribute.DATA, 0)]
        virtual public double Weight
        {
            get { return Get<double>(); }
            protected set { Set(value, IsReady, IsReady); } //Note: this will fire a property change even if no value change
        }

        public event EventHandler<double> WeightUpdated;

        public LoadCell(String id, byte doutPin, byte sckPin, String name = DEFAULT_NAME) : base(id, name)
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
                case "RawValue":
                    return message.Type == MessageType.DATA ? 0 : 1;

                case "MessageID":
                    return 0;

                default:
                    return base.GetArgumentIndex(fieldName, message);
            }
        }

        virtual protected void OnSetWeight()
        {
            WeightUpdated?.Invoke(this, Weight);
        }

        override public void HandleMessage(ADMMessage message)
        {
            switch (message.Type)
            {
                case MessageType.DATA:
                    RawValue = GetMessageValue<Int32>("RawValue", message);

                    double w = (RawValue - Offset) / Scale;
                    Weight = Math.Min(Math.Max(w, MinWeight), MaxWeight);
                    OnSetWeight();
                    break;
            }

            base.HandleMessage(message);
        }

        virtual public void Tare()
        {
            Offset = RawValue;
        }

        /*protected override bool HandleCommandResponse(ArduinoCommand.DeviceCommand deviceCommand, ADMMessage message)
        {
            switch (deviceCommand)
            {
                case ArduinoCommand.DeviceCommand.ZERO:
                    break;
            }
            return base.HandleCommandResponse(deviceCommand, message);
        }*/
    }
}
