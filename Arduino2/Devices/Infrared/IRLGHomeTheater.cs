using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2.Devices.Infrared
{
    public class IRLGHomeTheater : IRTransmitter
    {
        public const String DEVICE_NAME = "LG Home Theater";

        public IRLGHomeTheater(String id, int transmitPin, IRDB db) : base(id, "LGHT", transmitPin, db)
        {
            DeviceName = DEVICE_NAME;

            AddCompoundCommand("Unmute", "Volume_up", "Volume_down");
            AddCompoundCommand("Mute", "Unmute", "Mute/Unmute");
            AddCompoundCommand("Optical", 500, "AuxOpt", "Function", "AuxOpt");
            AddCompoundCommand("Bluetooth", 500, "Optical", "Function");
            AddCompoundCommand("Aux", 500, "Optical", "Function", "Function");
        }
    }
}
