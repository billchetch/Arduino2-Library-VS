using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using Chetch.Utilities;

namespace Chetch.Arduino2
{
    abstract public class ArduinoDeviceGroup : ArduinoObject
    {
        public ArduinoDeviceManager ADM { get; set; }

        [ArduinoProperty(PropertyAttribute.IDENTIFIER)]

        override public String UID => ADM.ID + ":" + ID;

        [ArduinoProperty(PropertyAttribute.IDENTIFIER)]
        public String Name { get; internal set; }

        public List<ArduinoDevice> Devices { get; internal set; } = new List<ArduinoDevice>();

        [ArduinoProperty(ArduinoPropertyAttribute.STATE, true)]
        public bool Enabled
        {
            get { return Get<bool>(); }
            internal set { Set(value); }
        }

        [ArduinoProperty(ArduinoPropertyAttribute.METADATA, PropertyAttribute.DATETIME_DEFAULT_VALUE_MIN)]
        public DateTime LastStatusResponseOn
        {
            get { return Get<DateTime>(); }
            set { Set(value); }
        }

        private Object _handlePropertyChangeLock = new Object();

        public ArduinoDeviceGroup(String id, String name)
        {
            ID = id;
            Name = name;
        }

        override public String ToString()
        {
            return String.Format("{0} {1}, {2} devices", UID, Name, Devices.Count);
        }

        protected String CreateDeviceID(String deviceID)
        {
            return String.Format("{0}-{1}", ID, deviceID.ToLower());
        }

        public void AddDevice(ArduinoDevice dev)
        {
            if (dev == null) return;

            if (!Devices.Contains(dev))
            {
                Devices.Add(dev);
                dev.PropertyChanged += HandlePropertyChange;
            }
        }

        public void AddDevices(params ArduinoDevice[] devices)
        {
            AddDevices(devices.ToList());
        }

        public void AddDevices(List<ArduinoDevice> devices)
        {
            foreach(var dev in devices)
            {
                AddDevice(dev);
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

        

        protected void HandlePropertyChange(Object sender, PropertyChangedEventArgs eargs)
        {
            DSOPropertyChangedEventArgs dsoArgs = (DSOPropertyChangedEventArgs)eargs;
            ArduinoDevice device = ((ArduinoDevice)sender);
            var prop = device.GetProperty(dsoArgs.PropertyName, -1);
            if (prop != null && device.IsReady)
            {
                try
                {
                    lock (_handlePropertyChangeLock)
                    {
                        if(prop.Name == "State" && device.IsReady)
                        {
                            OnDeviceReady(device);
                        }

                        HandleDevicePropertyChange(device, prop);
                    }
                } catch (Exception e)
                {
                    ADM.Tracing?.TraceEvent(System.Diagnostics.TraceEventType.Error, 900, "DG {0} Error: {1}", UID, e.Message);
                }
            }
        }

        virtual protected void OnDeviceReady(ArduinoDevice device)
        {
            //a hook for convenience
        }

        virtual protected void HandleDevicePropertyChange(ArduinoDevice device, System.Reflection.PropertyInfo property)
        {
            switch (property.Name)
            {
                case "Enabled":
                    Enabled = (bool)property.GetValue(device);
                    break;

                case "LastStatusResponseOn":
                    LastStatusResponseOn = device.LastStatusResponseOn;
                    break;
            }
        }

        virtual public void Enable(bool enable = true, bool setDevices = true, String requester = null)
        {
            Enabled = enable;
            if (setDevices)
            {
                foreach (var dev in Devices)
                {
                    var req = dev.Enable(enable);
                    req.Owner = requester;
                }
            }
        }

        virtual public void RequestStatus(String requester = null)
        {
            foreach (var dev in Devices)
            {
                dev.RequestStatus(requester);
            }
        }

        virtual public void ExecuteCommand(String commandAlias, String requester, List<Object> parameters = null)
        {
            switch (commandAlias.Trim().ToLower())
            {
                case "enable":
                    bool enable = parameters != null && parameters.Count > 0 ? Chetch.Utilities.Convert.ToBoolean(parameters[0]) : true;
                    Enable(enable);
                    return;

                case "status":
                    RequestStatus();
                    return;
            }

            foreach(var dev in Devices)
            {
                var req = dev.ExecuteCommand(commandAlias, parameters);
                req.Owner = requester;
            }
            
        }

        protected override int GetArgumentIndex(string fieldName, ADMMessage message)
        {
            return 0;
        }
    }
}
