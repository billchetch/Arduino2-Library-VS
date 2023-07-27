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

        public float Temperature => Sensors.Count == 1 ? Sensors[0].Temperature : (float)DS18B20Array.ErrorTemps.NO_SENSOR;

        public TemperatureSensor(String id, byte pin, BitResolution resolution) : base(id, pin, resolution)
        {}

        override protected void OnData(ADMMessage message)
        {
            base.OnData(message);

            TemperatureUpdated?.Invoke(this, Temperature);
        }
    }
}
