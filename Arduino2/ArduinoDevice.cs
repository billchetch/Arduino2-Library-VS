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
        public ArduinoDeviceManager ADM { get; set; }

        public String ID { get; internal set; }
        public String Name { get; internal set; }
        public byte BoardID { get; set; }

        public bool Enabled { get; internal set; }

        public ArduinoDevice(String id, String name)
        {
            ID = id;
            Name = name.ToUpper();
        }

        public ADMMessage CreateMessage(MessageType messageType)
        {
            var message = new ADMMessage();
            message.Type = messageType;
            message.SenderID = BoardID;

            return message;
        }

        public void Initialise()
        {
            var message = CreateMessage(MessageType.INITIALISE);
            message.AddArgument(3);

            ADM.SendMessage(message);
        }
    }

    public class TestDevice01 : ArduinoDevice
    {
        public TestDevice01(String id, String name = "TEST01") : base(id, name)
        {

        }
    }
}
