using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Messaging;
using Chetch.Arduino;

namespace Chetch.Arduino2
{
    abstract public class ArduinoDevice
    {
        public const int REPORT_INTERVAL_NONE = -1;

        public ArduinoDeviceManager ADM { get; set; }

        
        public String ID { get; internal set; }
        public String Name { get; internal set; }
        public byte BoardID { get; set; }

        public DeviceCategory Category { get; protected set; }
        public bool Enabled { get; internal set; } = true;

        public int ReportInterval { get; set; }

        private bool _initialised = false;
        private bool _configured = false;

        public bool IsReady => _initialised && _configured;

        public ArduinoDevice(String id, String name)
        {
            ID = id;
            Name = name.ToUpper();
        }

        public ADMMessage CreateMessage(MessageType messageType)
        {
            var message = new ADMMessage();
            message.Type = messageType;
            message.TargetID = BoardID;
            message.SenderID = BoardID;

            return message;
        }

        virtual public ADMMessage Initialise()
        {
            _initialised = false;
            _configured = false;
            var message = CreateMessage(MessageType.INITIALISE);
            message.AddArgument(Name == null ? "N/A" : Name);
            message.AddArgument((byte)Category);
            return message;
        }

        virtual public ADMMessage Configure()
        {
            var message = CreateMessage(MessageType.CONFIGURE);
            message.AddArgument(Enabled ? (byte)1 : (byte)0);
            message.AddArgument(ReportInterval);
            return message;
        }

        virtual public ADMMessage HandleMessage(ADMMessage message)
        {
            switch (message.Type)
            {
                case MessageType.INITIALISE_RESPONSE:
                    _initialised = true;
                    return Configure();

                case MessageType.CONFIGURE_RESPONSE:
                    _configured = true;
                    break;


            }
            return null;
        }
    }

    public class TestDevice01 : ArduinoDevice
    {
        public TestDevice01(String id, String name = "TEST01") : base(id, name)
        {
            Category = DeviceCategory.DIAGNOSTICS;
        }

        override public ADMMessage Configure()
        {
            var message = base.Configure();
            return message;
        }
    }
}
