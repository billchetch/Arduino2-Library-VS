using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2
{
    public class ArduinoCommand
    {
        static ArduinoCommand Delay(int delay)
        {
            return new ArduinoCommand(delay);
        }

        public enum DeviceCommand
        {
            NONE = 0,
            TEST = 1,
            ENABLE,
            DISABLE,
            START,
            STOP,
            PAUSE,
            RESET,
        }

        public DeviceCommand Command { get; internal set; }

        public String Alias { get; internal set; }

        public List<UInt32> Parameters { get; internal set; } = new List<UInt32>();

        private int _delay = 0;

        List<ArduinoCommand> Commands = new List<ArduinoCommand>();

        public bool IsCompound => Commands != null && Commands.Count > 0;

        ArduinoCommand(DeviceCommand command, String alias = null)
        {
            Command = command;
            Alias = alias;
        }


        ArduinoCommand(DeviceCommand command, UInt32 parameter, String alias = null)
        {
            Command = command;
            Alias = alias;
            AddParameter(parameter);
        }
        ArduinoCommand(int delay) : this(DeviceCommand.NONE)
        {
            _delay = delay;
        }

        public void AddParameter(UInt32 parameter)
        {
            Parameters.Add(parameter);
        }

        public void AddParameters(params UInt32[] parameters)
        {
            foreach(var p in parameters)
            {
                AddParameter(p);
            }
        }
    }
}
