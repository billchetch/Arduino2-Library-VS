using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2.Devices.Infrared
{
    public abstract class IRTransmitter : IRDevice
    {
        private bool _active = false;
        private int _enablePin; //HIGH output means the transmitter is disabled (as there is no voltage across it)
        private int _transmitPin;

        //if last command is same as current command and time diff (millis) between last command and current command
        //is less than RepeatInterval then use _repeatCommand if it exists.
        private ArduinoCommand _repeatCommand = null;
        public int RepeatInterval { get; set; } = 200;
        public bool UseRepeatCommand = true;
        
        public IRTransmitter(String id, String name, int enablePin, int transmitPin = 0, IRDB db = null) : base(id, name, db)
        {
            Category = DeviceCategory.IR_TRANSMITTER;

            _enablePin = enablePin;
            _transmitPin = transmitPin;
            
        }

        public IRTransmitter(int enablePin, int transmitPin = 0, IRDB db = null) : this("irt" + enablePin, "IRT", enablePin, transmitPin, db) { }

        public override void ReadDevice()
        {
            base.ReadDevice();
            if(DB != null)
            {
                AddCommands(DB.GetCommands(DeviceName), true);
                _repeatCommand = GetCommand(REPEAT_COMMAND);
            }
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
