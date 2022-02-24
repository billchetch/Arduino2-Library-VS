using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2.Devices.Infrared
{
    public class IRGenericTransmitter : IRTransmitter
    {
        public IRGenericTransmitter(String id, String name, int transmitPin, IRDB db = null) : base(id, name, transmitPin, db)
        {}

        public IRGenericTransmitter(int transmitPin = 0, IRDB db = null) : base(transmitPin, db) 
        {}

        public IRGenericTransmitter(IRDB db) : base(db) 
        {}

        public override void ReadDevice()
        {
            base.ReadDevice();

            RemoveCommand("Unmute");
            RemoveCommand("Mute");

            AddCompoundCommand("Unmute", 500, "Volume_Up", "Volume_Down");
            AddCompoundCommand("Mute", 500, "Unmute", "Mute/Unmute");
        }
    }
}
