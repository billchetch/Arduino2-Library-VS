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

        public static String FormatAlias(String alias)
        {
            return alias.ToLower().Trim().Replace('_', '-').Replace(' ', '-');
        }

        public static String FormatAlias(DeviceCommand deviceCommand)
        {
            return FormatAlias(deviceCommand.ToString());
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
            ACTIVATE,
            DEACTIVATE,
            RESUME,
            ZERO,
        }

        public enum ParameterType
        {
            STRING,
            BOOL,
            INT,
            BYTE,
            FLOAT,
            BYTE_ARRAY,
            INT_ARRAY,
        }



        public long ID { get; internal set; } = 0;

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
                _alias = FormatAlias(value);
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

        public ArduinoCommand(long id, DeviceCommand command, String alias = null, List<ParameterType> parameterTypes  = null)
        {
            ID = id;
            Command = command;
            Alias = alias == null ? command.ToString() : alias;
            if(parameterTypes != null)AddParameterTypes(parameterTypes);
        }

        public ArduinoCommand(DeviceCommand command, String alias = null, List<ParameterType> parameterTypes = null) : this(0, command, alias, parameterTypes) { }
        
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

        public void SetParameter(int idx, Object paramValue)
        {
            if (idx >= ParameterTypes.Count)
            {
                throw new Exception(String.Format("Cannot set parameter at position {0} because no parameter type has been specified", idx));
            }
            ValidateParameter(paramValue, ParameterTypes[idx]);
            if (idx == Parameters.Count)
            {
                Parameters.Add(paramValue);
            }
            else if (idx < Parameters.Count)
            {
                Parameters[idx] = paramValue;
            }
            else
            {
                throw new Exception(String.Format("{0} is not a valid parameter index", idx));
            }
        }

        public void ValidateParameter(Object paramValue, ParameterType paramType)
        {
            switch (paramType)
            {
                case ParameterType.STRING:
                    if(paramValue != null && !(paramValue is String))
                    {
                        throw new Exception(String.Format("Parameter {0} is not a string", paramValue));
                    }
                    break;

                case ParameterType.BOOL:
                    Chetch.Utilities.Convert.ToBoolean(paramValue);
                    break;
                
                case ParameterType.INT:
                    break;

                case ParameterType.BYTE:
                    if (!(paramValue is byte))
                    {
                        throw new Exception(String.Format("Parameter {0} is not a boolean", paramValue));
                    }
                    break;

                case ParameterType.FLOAT:
                    if (!(paramValue is float || paramValue is double))
                    {
                        throw new Exception(String.Format("Parameter {0} is not a float", paramValue));
                    }
                    break;

                default:
                    throw new Exception(String.Format("Cannot validate parameter type {0}", paramType));
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
                try
                {
                    switch (paramType)
                    {
                        case ParameterType.BYTE_ARRAY:         
                        case ParameterType.INT_ARRAY:
                            if (paramValue == null) continue;

                            if (paramValue.GetType().IsArray)
                            {
                                ParameterType elementType = 0;
                                switch (paramType)
                                {
                                    case ParameterType.BYTE_ARRAY:
                                        elementType = ParameterType.BYTE; break;
                                    case ParameterType.INT_ARRAY:
                                        elementType = ParameterType.INT; break;
                                }

                                var ar = (System.Collections.IEnumerable)parameters[i];
                                foreach (var item in ar)
                                {
                                    ValidateParameter(item, elementType);
                                }
                            }
                            else
                            {
                                throw new Exception(String.Format("Not an array"));
                            }
                            break;

                        default:
                            ValidateParameter(paramValue, paramType);
                            break;
                    }
                } catch (Exception e)
                {
                    throw new Exception(String.Format("Parameter in position {0}: ", i, e.Message));
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
