using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2.Devices.Electricity
{
    public class ZMPT101B : ArduinoDevice
    {
        public ZMPT101B(String id, String name) : base(id, "ZMPT101B")
        {

            Category = DeviceCategory.VAC_SENSOR;
        }
    }
}
