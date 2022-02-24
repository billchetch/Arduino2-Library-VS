using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2.Devices.Infrared
{
    public struct IRCode
    {
        public IRProtocol Protocol;
        public UInt16 Address;
        public UInt16 Command;
        
        public override string ToString()
        {
            return Address.ToString("X") + "," + Command.ToString("X") + " - Protocol: " + Protocol.ToString();
        }
    }

    public enum IRProtocol{
        UNKNOWN = 0,
        PULSE_DISTANCE,
        PULSE_WIDTH,
        DENON,
        DISH,
        JVC,
        LG,
        LG2,
        NEC,
        PANASONIC,
        KASEIKYO,
        KASEIKYO_JVC,
        KASEIKYO_DENON,
        KASEIKYO_SHARP,
        KASEIKYO_MITSUBISHI,
        RC5,
        RC6,
        SAMSUNG,
        SHARP,
        SONY,
        ONKYO,
        APPLE,
        BOSEWAVE,
        LEGO_PF,
        MAGIQUEST,
        WHYNTER,
    }

    public class IRDevice : ArduinoDevice
    {
        
        protected IRDB DB { get; set;  }
        protected long DBID { get; set;  }  = 0;
        public bool IsInDB
        {
            get
            {
                return DBID > 0;
            }
        }

        private String _deviceName;
        virtual public String DeviceName {
            get
            {
                return _deviceName;
            }
            set
            {
                _deviceName = value;
                if (DB != null && _deviceName != null) ReadDevice();
            }
        }
        public String DeviceType { get; set; } = null;
        public String Manufacturer { get; set; } = null;
        public IRProtocol Protocol { get; set; } = IRProtocol.UNKNOWN; //TODO: set this to force sending/recording in a different protocol

        public IRDevice(String id, String name, IRDB db = null) : base(id, name)
        {
            DB = db;
        }
        
        virtual public void ReadDevice()
        {
            if (DB == null) throw new Exception("No database available");
            DBID = 0;
            if (DeviceName != null)
            {
                Database.DBRow dev = DB.GetDevice(DeviceName);

                //TODO: make it so that you can choose to overwrite data or not
                if (dev != null)
                {
                    DBID = dev.ID;
                    DeviceType = (String)dev["device_type"];
                    Manufacturer = (String)dev["manufacturer"];
                }
            }
        }

        virtual public void WriteDevice()
        {
            if (DB == null) throw new Exception("No database supplied");

            if(DeviceType == null || DeviceType.Length == 0)
            {
                throw new Exception("Cannot write to DB as device does not have a type");
            }

            if(DBID > 0)
            {
                //DB.
            } else
            {
                DBID = DB.InsertDevice(Name, DeviceType, Manufacturer == null ? "Unknown" : Manufacturer);
            }
        }
    }
}
