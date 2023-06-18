using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Messaging;

namespace Chetch.Arduino2.Devices
{
    public class Ticker : ArduinoDevice
    {
        public const String DEFAULT_NAME = "TICKER";

        public byte Pin { get; internal set; }
        
        private int _pinHighDuration;
        private int _pinLowDuration;


        public int TickCount { get; internal set; }

        public Ticker(String id, byte pin, int interval, int tickDuration = 0, String name = DEFAULT_NAME) : base(id, name)
        {
            Pin = pin;
            Category = DeviceCategory.TICKER;
            
            if(tickDuration == 0 || tickDuration >= interval)
            {
                _pinHighDuration = interval / 2;
            }

            _pinLowDuration = interval - _pinHighDuration;
        }

        override protected int GetArgumentIndex(String fieldName, ADMMessage message)
        {
            switch (fieldName)
            {
                case "TickCount":
                    return 0;

                default:
                    return base.GetArgumentIndex(fieldName, message);
            }
        }

        protected override void AddConfig(ADMMessage message)
        {
            base.AddConfig(message);

            message.AddArgument(Pin);
            message.AddArgument(_pinHighDuration);
            message.AddArgument(_pinLowDuration);
        }

        public override void HandleMessage(ADMMessage message)
        {
            switch (message.Type)
            {
                case MessageType.DATA:
                    AssignMessageValues(message, "TickCount");
                    break;
            }

            base.HandleMessage(message);
        }

        
        protected override bool HandleCommandResponse(ArduinoCommand.DeviceCommand deviceCommand, ADMMessage message)
        {
            switch(deviceCommand)
            {
                default:
                    break;
            }
            return base.HandleCommandResponse(deviceCommand, message);
        }
    }
}
