using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2.Devices.Infrared
{
    /// <summary>
    /// Note: Arduino board Works with KY-005 transmitter
    /// </summary>
    /// 
    public abstract class IRTransmitter : IRDevice
    {
        private bool _active = false;
        private int _activatePin; //HIGH output means the transmitter is disabled (as there is no voltage across it)
        private int _transmitPin;

        public bool IsActive => _active;

        //if last command is same as current command and time diff (millis) between last command and current command
        //is less than RepeatInterval then use _repeatCommand if it exists.
        private ArduinoCommand _repeatCommand = null;
        public int RepeatInterval { get; set; } = 200;
        public bool UseRepeatCommand = true;

        
        public IRTransmitter(String id, String name, int activatePin = 0, int transmitPin = 0, IRDB db = null) : base(id, name, db)
        {
            Category = DeviceCategory.IR_TRANSMITTER;

            _activatePin = activatePin;
            _transmitPin = transmitPin;

            //command for sending raw data
            var cmd = AddCommand(ArduinoCommand.DeviceCommand.SEND, "send-raw");
            cmd.AddParameter(ArduinoCommand.ParameterType.INT, 0); //ircommand
            cmd.AddParameter(ArduinoCommand.ParameterType.INT, 0); //bits
            cmd.AddParameter(ArduinoCommand.ParameterType.INT, -1); //protocol
            cmd.AddParameterType(ArduinoCommand.ParameterType.INT); //Raw Length
            cmd.AddParameterType(ArduinoCommand.ParameterType.INT_ARRAY); //Raw

            //activate stuff
            if (_activatePin > 0)
            {
                AddCommand(ArduinoCommand.DeviceCommand.ACTIVATE);
                AddCommand(ArduinoCommand.DeviceCommand.DEACTIVATE);
            }
        }

        public IRTransmitter(int activatePin = 0, int transmitPin = 0, IRDB db = null) : this("irt" + activatePin, "IRT", activatePin, transmitPin, db) { }

        public IRTransmitter(IRDB db) : this(0, 0, db) { }

        public override void ReadDevice()
        {
            base.ReadDevice();
            if(DB != null)
            {
                //remove commands added from database (ID>0)
                foreach(var cmd in Commands)
                {
                    if (cmd.ID > 0) RemoveCommand(cmd);
                }
                //add commands from database
                AddCommands(DB.GetCommands(DeviceName));
                _repeatCommand = GetCommand(REPEAT_COMMAND);
            }
        }

        protected override void AddConfig(ADMMessage message)
        {
            base.AddConfig(message);

            message.AddArgument(_activatePin);
            message.AddArgument(_transmitPin);
        }

        public override ADMRequestManager.ADMRequest ExecuteCommand(string commandAlias, List<object> parameters = null)
        {
            if (commandAlias.Length == 2 && uint.TryParse(commandAlias, out _))
            {
                //split a 2 digit number in to it's components ... this is so we can have commands like 62
                //without needing to build a new command from '6' and from '2'
                int d1 = (int)char.GetNumericValue(commandAlias[0]);
                int d2 = (int)char.GetNumericValue(commandAlias[1]);

                base.ExecuteCommand(d1.ToString(), parameters);
                System.Threading.Thread.Sleep(RepeatInterval * 2);
                base.ExecuteCommand(d2.ToString(), parameters);
                return null;
            }
            else
            {
                return base.ExecuteCommand(commandAlias, parameters);
            }
        }

        protected override bool HandleCommand(ArduinoCommand cmd, List<object> parameters)
        {
            switch (cmd.Command)
            {
                case ArduinoCommand.DeviceCommand.ACTIVATE:
                    _active = true;
                    break;

                case ArduinoCommand.DeviceCommand.DEACTIVATE:
                    _active = false;
                    break;
            }
            return base.HandleCommand(cmd, parameters);
        }

        public ADMRequestManager.ADMRequest Transmit(String commandAlias)
        {
            return ExecuteCommand(commandAlias);
        }

        public ADMRequestManager.ADMRequest TransmitRaw(UInt16[] raw)
        {
            return ExecuteCommand("send-raw", raw.Length, raw);
        }

        public ADMRequestManager.ADMRequest TransmitRaw(String rawString)
        {
            var items = rawString.Split(',');
            UInt16[] raw = new ushort[items.Length];
            for(int i = 0; i < items.Length; i++)
            {
                raw[i] = System.Convert.ToUInt16(items[i].Trim());
            }
            return TransmitRaw(raw);
        }

        public ADMRequestManager.ADMRequest Activate()
        {
            return ExecuteCommand(ArduinoCommand.DeviceCommand.ACTIVATE);
        }

        public ADMRequestManager.ADMRequest Deactivate()
        {
            return ExecuteCommand(ArduinoCommand.DeviceCommand.DEACTIVATE);
        }

        /*override protected void SendCommand(ArduinoCommand command, ExecutionArguments xargs)
        {
            if(command.Type == ArduinoCommand.CommandType.SEND && Protocol != IRProtocol.UNKNOWN && command.Arguments.Count == 3)
            {
                command.Arguments[2] = (int)Protocol;
            }

            var timeDiff = (DateTime.Now.Ticks - LastCommandSentOn) / TimeSpan.TicksPerMillisecond;
            if (UseRepeatCommand && _repeatCommand != null && LastCommandSent != null && LastCommandSent.Equals(command) && timeDiff < RepeatInterval)
            {
                base.SendCommand(_repeatCommand, xargs);
                LastCommandSent = command;
            }
            else
            {
                base.SendCommand(command, xargs);
            }
        }*/
        protected override int GetArgumentIndex(string fieldName, ADMMessage message)
        {
            switch(fieldName)
            {
                case "TransmitPin":
                    return 2;

                default:
                    return base.GetArgumentIndex(fieldName, message);
            }
        }

        protected override void OnConfigured(ADMMessage message)
        {
            base.OnConfigured(message);

            if (_transmitPin == 0)
            {
                int tp = GetMessageValue<int>("TransmitPin", message);
                _transmitPin = tp;
            }
        }
    }
}
