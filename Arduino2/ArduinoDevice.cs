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

        public enum DeviceState
        {
            CREATED = 1,
            INITIALISING,
            INITIALISED,
            CONFIGURING,
            CONFIGURED,
        }

        public DeviceState State { get; internal set; } = DeviceState.CREATED;

        public enum MessageField
        {
            ENABLED = 0,
            REPORT_INTERVAL,
            DEVICE_NAME,
            CATEGORY
        }

        public ArduinoDeviceManager ADM { get; set; }

        
        public String ID { get; internal set; }
        public String Name { get; internal set; }
        public byte BoardID { get; set; }

        public DeviceCategory Category { get; protected set; }
        public bool Enabled { get; internal set; } = true;

        public int ReportInterval { get; set; }

        
        public bool IsReady => State == DeviceState.CONFIGURED;

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

        public int GetArgumentIndex(ADMMessage message, MessageField field)
        {
            switch (field)
            {
                default:
                    return (int)field;
            }
        }

        virtual public ADMMessage Initialise()
        {
            State = DeviceState.INITIALISING;
            var message = CreateMessage(MessageType.INITIALISE);
            message.AddArgument(Name == null ? "N/A" : Name);
            message.AddArgument((byte)Category);
            return message;
        }

        virtual public ADMMessage Configure()
        {
            State = DeviceState.CONFIGURING;
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
                    State = DeviceState.INITIALISED;
                    return Configure();

                case MessageType.CONFIGURE_RESPONSE:
                    State = DeviceState.CONFIGURED;
                    break;

                case MessageType.STATUS_RESPONSE:
                    break;
            }
            return null;
        }
    }

    public class TestDevice01 : ArduinoDevice
    {

        new public enum MessageField
        {
            TEST_VALUE = 0
        }

        public TestDevice01(String id, String name = "TEST01") : base(id, name)
        {
            Category = DeviceCategory.DIAGNOSTICS;
            Enabled = false;
        }

        public int GetArgumentIndex(ADMMessage message, MessageField field)
        {
            switch (field)
            {
                default:
                    return (int)field;
            }
        }

        override public ADMMessage Configure()
        {
            var message = base.Configure();

            return message;
        }
    }
}
