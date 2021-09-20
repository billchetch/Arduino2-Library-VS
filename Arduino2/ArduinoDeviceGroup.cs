﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Utilities;

namespace Chetch.Arduino2
{
    public class ArduinoDeviceGroup : ArduinoObject
    {
        public ArduinoDeviceManager ADM { get; set; }

        [ArduinoProperty(PropertyAttribute.IDENTIFIER)]

        override public String UID => ADM.ID + ":" + ID;

        [ArduinoProperty(PropertyAttribute.IDENTIFIER)]
        public String Name { get; internal set; }

        public List<ArduinoDevice> Devices { get; internal set; } = new List<ArduinoDevice>();

        //use the Enable method to set this value
        public bool Enabled { get; private set; } = false;


        public ArduinoDeviceGroup(String id, String name)
        {
            ID = id;
            Name = name;
        }

        public void AddDevice(ArduinoDevice dev)
        {
            if (dev == null) return;

            if (!Devices.Contains(dev))
            {
                Devices.Add(dev);
            }
        }

        public ArduinoDevice GetDevice(String deviceID)
        {
            foreach (var dev in Devices)
            {
                if (dev.ID != null && dev.ID.Equals(deviceID)) return dev;
            }
            return null;
        }

        virtual public void Enable(bool enable = true, bool setDevices = true)
        {
            Enabled = enable;
            if (setDevices)
            {
                foreach (var dev in Devices)
                {
                    dev.Enable(enable);
                }
            }
        }

        virtual public void RequestStatus()
        {
            foreach (var dev in Devices)
            {
                dev.RequestStatus();
            }
        }

        virtual public void ExecuteCommand(String commandAlias, List<ValueType> parameters = null)
        {
            foreach(var dev in Devices)
            {
                dev.ExecuteCommand(commandAlias, parameters);
            }
            
        }
    }
}
