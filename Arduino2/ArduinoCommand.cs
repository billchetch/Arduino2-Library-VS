using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2
{
    public class ArduinoCommand
    {
        public static ArduinoCommand Delay(int delay)
        {
            return new ArduinoCommand(delay);
        }

        public static ArduinoCommand Enable(bool enable)
        {
            var cmd = new ArduinoCommand(DeviceCommand.ENABLE);
            cmd.AddParameter(enable);
            return cmd;
        }

        public enum DeviceCommand
        {
            NONE = 0,
            COMPOUND,
            TEST,
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

        public List<ArduinoCommand> Commands { get; internal set; } = new List<ArduinoCommand>();

        public bool IsCompound => Commands != null && Commands.Count > 0;

        public bool IsDelay => Command == DeviceCommand.NONE && DelayInterval > 0;

        public int TotalDelayInterval { get; internal set; } = 0;

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
            if (IsDelay) throw new InvalidOperationException("Cannot add parameter to a delay command");
            Parameters.Add(parameter);
            UpdateTotals();
        }

        public void AddParameters(params ValueType[] parameters)
        {
            foreach(var p in parameters)
            {
                AddParameter(p);
            }
        }

        public void AddCommand(ArduinoCommand cmd)
        {
            if (IsDelay) throw new InvalidOperationException("Cannot add sub command to a delay command");
            Commands.Add(cmd);
            UpdateTotals();
        }

        public void AddCommands(params ArduinoCommand[] cmds)
        {
            foreach(var cmd in cmds)
            {
                AddCommand(cmd);
            }
        }

        public void UpdateTotals()
        {
            TotalDelayInterval = 0;
            if (!IsCompound)
            {
                TotalDelayInterval = DelayInterval;
            } else
            {
                foreach(var cmd in Commands)
                {
                    cmd.UpdateTotals();
                    TotalDelayInterval += cmd.TotalDelayInterval;
                }
            }
        }
    }
}
