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

        static ArduinoCommand Enable(bool enable)
        {
            var cmd = new ArduinoCommand(DeviceCommand.ENABLE);
            cmd.AddParameter(enable);
            return cmd;
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

        public List<ValueType> Parameters { get; internal set; } = new List<ValueType>();

        public int DelayInterval { get; internal set; } = 0;

        List<ArduinoCommand> Commands = new List<ArduinoCommand>();

        public bool IsCompound => Commands != null && Commands.Count > 0;

        public bool IsDelay => Command == DeviceCommand.NONE && DelayInterval > 0;

        public ArduinoCommand(DeviceCommand command, String alias = null)
        {
            Command = command;
            Alias = alias;
        }

        public ArduinoCommand(DeviceCommand command, ValueType parameter, String alias = null)
        {
            Command = command;
            Alias = alias;
            AddParameter(parameter);
        }
        ArduinoCommand(int delay) : this(DeviceCommand.NONE)
        {
            DelayInterval = delay;
        }

        public void AddParameter(ValueType parameter)
        {
            Parameters.Add(parameter);
        }

        public void AddParameters(params ValueType[] parameters)
        {
            foreach(var p in parameters)
            {
                AddParameter(p);
            }
        }
    }
}
