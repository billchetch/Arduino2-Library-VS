﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2.Devices.Infrared
{
    public class IRGenericReceiver : IRReceiver
    {
        public IRGenericReceiver(String id, String name, int receivePin, IRDB db = null) : base(id, name, receivePin, db)
        {
            if (DB != null) ReadDevice();
        }

        public IRGenericReceiver(int receivePin, IRDB db = null) : base(receivePin, db) 
        {
            if (DB != null) ReadDevice();
        }


        override public void WriteDevice()
        {
            ReadDevice(); //incase the name has changed (normally derived classes define the name, this class is 'Generic')
            base.WriteDevice();
        }
    }
}
