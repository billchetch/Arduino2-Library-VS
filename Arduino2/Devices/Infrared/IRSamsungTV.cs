﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2.Devices.Infrared
{
    public class IRSamsungTV : IRTransmitter
    {
        public const String DEVICE_NAME = "Samsung TV";

        public IRSamsungTV(String id, int enablePin, int transmitPin, IRDB db) : base(id, "SSTV", enablePin, transmitPin, db)
        {
            DeviceName = DEVICE_NAME;
        }
    }
}
