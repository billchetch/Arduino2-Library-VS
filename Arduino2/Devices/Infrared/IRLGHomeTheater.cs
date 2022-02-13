﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2.Devices.Infrared
{
    public class IRLGHomeTheater : IRTransmitter
    {
        public const String DEVICE_NAME = "LG Home Theater";

        public IRLGHomeTheater(String id, int enablePin, int transmitPin, IRDB db) : base(id, "LGHT", enablePin, transmitPin, db)
        {
            DeviceName = DEVICE_NAME;

            AddCompoundCommand("Unmute", "Volume_up", "Volume_down");
            AddCompoundCommand("Mute", "Unmute", "Mute/Unmute");
        }
    }
}
