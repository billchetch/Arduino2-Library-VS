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
            cmd.AddParameterType(ParameterType.BOOL);
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
            SET_REPORT_INTERVAL,
            START,
            STOP,
            PAUSE,
            RESET,
            ON,
            OFF,
            MOVE,
            ROTATE,
            PRINT,
            SET_CURSOR,
            DISPLAY,
            CLEAR,
            SILENCE,
            SEND,
            TRANSMIT,
            SAVE,
            OTHER, //Use this if you want an alias based command only
        }

        public enum ParameterType
        {
            STRING,
            BOOL,
            INT,
            BYTE,
            FLOAT,
        }

        public DeviceCommand Command { get; internal set; }

        private String _alias;
        public String Alias
        {
            get { return _alias; }
            internal set
            {
                if (String.IsNullOrEmpty(value))
                {
                    throw new ArgumentException("Alias cannot be null or empty");
                }
                _alias = value.ToLower().Trim().Replace('_', '-');
            }
        }

        public List<Object> Parameters { get; internal set; } = new List<Object>();

        public List<ParameterType> ParameterTypes { get;  internal set; } = new List<ParameterType>();

        public int DelayInterval { get; internal set; } = 0;

        public List<ArduinoCommand> Commands { get; internal set; } = new List<ArduinoCommand>();

        public bool IsCompound => Commands != null && Commands.Count > 0;

        public bool IsDelay => Command == DeviceCommand.NONE && DelayInterval > 0;

        private int _repeat = 1;
        public int Repeat 
        { 
            get { return _repeat; }
            set
            {
                if (_repeat <= 0) throw new ArgumentException("Repeat value must be 1 or greater");
                _repeat = value;
                UpdateTotals();
            }
        }
        public int TotalDelayInterval { get; internal set; } = 0;
        public int TotalCommandCount { get; internal set; } = 0;
        public int TotalDelayCount { get; internal set; } = 0;

        public ArduinoCommand(DeviceCommand command, String alias = null, List<ParameterType> parameterTypes  = null)
        {
            Command = command;
            Alias = alias == null ? command.ToString() : alias;
            if(parameterTypes != null)AddParameterTypes(parameterTypes);
        }

        ArduinoCommand(int delay) : this(DeviceCommand.NONE)
        {
            DelayInterval = delay;
            Alias = "delay";
        }

        public void AddParameterType(ParameterType parameterType)
        {
            ParameterTypes.Add(parameterType);
        }

        public void AddParameterTypes(List<ParameterType> parameterTypes)
        {
            foreach (var parameterType in parameterTypes)
            {
                ParameterTypes.Add(parameterType);
            }
        }

        public void AddParameter(Object parameter)
        {
            if (IsDelay) throw new InvalidOperationException("Cannot add parameter to a delay command");
            int idx = Parameters.Count;
            if(idx >= ParameterTypes.Count)
            {
                throw new Exception(String.Format("Cannot add parameter at position {0} because no parameter type has been specified", idx));
            }

            Parameters.Add(parameter);
            try
            {
                ValidateParameters(Parameters); //this will also normalise the parameter values
                UpdateTotals();
            } catch (Exception e)
            {
                Parameters.RemoveAt(Parameters.Count - 1);
                throw e;
            }
        }

        public void AddParameter(ParameterType parameterType, Object parameter)
        {
            AddParameterType(parameterType);
            AddParameter(parameter);
        }

        public void AddParameters(params Object[] parameters)
        {
            foreach(var p in parameters)
            {
                AddParameter(p);
            }
        }

        public void ValidateParameters(List<Object> parameters)
        {
            if(parameters.Count > ParameterTypes.Count)
            {
                throw new Exception(String.Format("Cannot validate parameters as there are too many. Command {0} specifies {1} parameter types but {2} parameters passed", Alias, ParameterTypes.Count, parameters.Count ));
            }

            for(int i = 0; i < parameters.Count; i++)
            {
                Object paramValue = parameters[i];
                ParameterType paramType = ParameterTypes[i];
                bool valid = false;
                switch (paramType)
                {
                    case ParameterType.STRING:
                        valid = (paramValue is String);
                        break;

                    case ParameterType.BOOL:
                        parameters[i] = Chetch.Utilities.Convert.ToBoolean(paramValue);
                        valid = true;
                        break;

                    case ParameterType.INT:
                        if(paramValue is int)
                        {
                            valid = true;
                        }
                        else
                        {
                            parameters[i] = int.Parse(paramValue.ToString());
                            valid = true;
                        }
                        break;

                    case ParameterType.BYTE:
                        if (paramValue is byte)
                        {
                            valid = true;
                        }
                        else
                        {
                            parameters[i] = byte.Parse(paramValue.ToString());
                            valid = true;
                        }
                        break;

                    case ParameterType.FLOAT:
                        if (paramValue is float || paramValue is double)
                        {
                            valid = true;
                        }
                        else
                        {
                            parameters[i] = float.Parse(paramValue.ToString());
                            valid = true;
                        }
                        break;

                }
                if (!valid)
                {
                    throw new Exception(String.Format("Paramter at position {0} is not of type", i, paramType));
                }
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
            TotalCommandCount = 0;
            TotalDelayCount = 0;
            if (!IsCompound)
            {
                TotalDelayInterval = Repeat*DelayInterval;
                TotalCommandCount += Repeat;
                if (IsDelay) TotalDelayCount += Repeat;
            } else
            {
                foreach(var cmd in Commands)
                {
                    cmd.UpdateTotals();
                    TotalDelayInterval += cmd.TotalDelayInterval;
                    TotalCommandCount += cmd.TotalCommandCount;
                    TotalDelayCount += cmd.TotalDelayCount;
                }
                TotalDelayInterval *= Repeat;
                TotalCommandCount *= Repeat;
            }
        }
    }
}
