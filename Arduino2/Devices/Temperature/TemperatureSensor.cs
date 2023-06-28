using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2.Devices.Temperature
{
    public class TemperatureSensor : DS18B20Array
    {
        public event EventHandler<float> TemperatureUpdated;

        public float Temperature => Sensors[0].Temperature;

        public TemperatureSensor(String id, byte pin, BitResolution resolution) : base(id, pin, resolution)
        {}

        override protected void OnData(ADMMessage message)
        {
            base.OnData(message);

            TemperatureUpdated?.Invoke(this, Temperature);
        }
    }
}
