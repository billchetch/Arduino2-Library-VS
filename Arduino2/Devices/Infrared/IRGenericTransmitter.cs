using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2.Devices.Infrared
{
    public class IRGenericTransmitter : IRTransmitter
    {
        public IRGenericTransmitter(String id, String name, int enablePin, int transmitPin = 0, IRDB db = null) : base(id, name, enablePin, transmitPin, db){}

        public IRGenericTransmitter(int enablePin, int transmitPin = 0, IRDB db = null) : base(enablePin, transmitPin, db) { }

        public override void AddCommands(List<ArduinoCommand> commands, bool clear = false)
        {
            base.AddCommands(commands, clear);

            if (commands.Count > 0)
            {
                AddCompoundCommand("Unmute", 500, "Volume_Up", "Volume_Down");
                AddCompoundCommand("Mute", 500, "Unmute", "Mute/Unmute");
            }
        }
    }
}
