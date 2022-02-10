using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2.Devices.Infrared
{
    public class IRTransmitterArray : ArduinoDeviceGroup
    {
        const String DEFAULT_NAME = "IRTARRAY";


        private List<IRTransmitter> _transmitters = new List<IRTransmitter>();

        public IRTransmitterArray(String id, String name = DEFAULT_NAME) : base(id, name)
        {

        }

        public void AddTransmitters(params IRTransmitter[] transmitters)
        {

        }

        public void AddTransmitter(IRTransmitter transmitter)
        {
            _transmitters.Add(transmitter);
        }

        protected override void HandleDevicePropertyChange(ArduinoDevice device, PropertyInfo property)
        {
            throw new NotImplementedException();
        }
    }
}
