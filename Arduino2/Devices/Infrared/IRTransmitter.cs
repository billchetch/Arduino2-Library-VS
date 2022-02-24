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
        private int _transmitPin;

        //if last command is same as current command and time diff (millis) between last command and current command
        //is less than RepeatInterval then use _repeatCommand if it exists.
        private ArduinoCommand _repeatCommand = null;
        public int RepeatInterval { get; set; } = 200;
        public bool UseRepeatCommand = true;
        private ArduinoCommand _lastSendCommand;
        private DateTime _lastSendCommandOn;

        
        public IRTransmitter(String id, String name, int transmitPin = 0, IRDB db = null) : base(id, name, db)
        {
            Category = DeviceCategory.IR_TRANSMITTER;

            _transmitPin = transmitPin;
        }

        public IRTransmitter(int transmitPin, IRDB db = null) : this("irt" + transmitPin, "IRT", transmitPin, db) { }

        public IRTransmitter(IRDB db) : this(0, db) { }

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
            }
        }

        protected override void AddConfig(ADMMessage message)
        {
            base.AddConfig(message);

            message.AddArgument(_transmitPin);
        }

        public override ADMRequestManager.ADMRequest ExecuteCommand(ArduinoCommand cmd, List<object> parameters = null)
        {
            if (cmd.Alias.Length == 2 && uint.TryParse(cmd.Alias, out _))
            {
                //split a 2 digit number in to it's components ... this is so we can have commands like 62
                //without needing to build a new command from '6' and from '2'
                int d1 = (int)char.GetNumericValue(cmd.Alias[0]);
                int d2 = (int)char.GetNumericValue(cmd.Alias[1]);

                base.ExecuteCommand(d1.ToString(), parameters);
                System.Threading.Thread.Sleep(RepeatInterval * 2);
                base.ExecuteCommand(d2.ToString(), parameters);
                return null;
            }
            else if(_repeatCommand != null && UseRepeatCommand && cmd.Command == ArduinoCommand.DeviceCommand.SEND && !cmd.Equals(_repeatCommand))
            {
                var timeDiff = (DateTime.Now.Ticks - _lastSendCommandOn.Ticks) / TimeSpan.TicksPerMillisecond;
                ArduinoCommand cmd2send = cmd;
                if (_lastSendCommand != null && _lastSendCommand.Equals(cmd) && timeDiff < RepeatInterval)
                {
                    cmd2send = _repeatCommand;
                    Console.WriteLine("Using repeat command for {0}", cmd.Alias);
                }
                _lastSendCommand = cmd;
                _lastSendCommandOn = DateTime.Now;
                return base.ExecuteCommand(cmd2send, parameters);
            } else
            {
                return base.ExecuteCommand(cmd, parameters);
            }
        }

       
        protected override bool HandleCommandResponse(ArduinoCommand.DeviceCommand deviceCommand, ADMMessage message)
        {
            Console.WriteLine("{0} {1}", message.GetArgument<Int16>(2), message.GetArgument<Int16>(3));
            return base.HandleCommandResponse(deviceCommand, message);
        }

        public ADMRequestManager.ADMRequest Transmit(String commandAlias)
        {
            return ExecuteCommand(commandAlias);
        }


        public ADMRequestManager.ADMRequest TransmitRepeat()
        {
            if(_repeatCommand == null)
            {
                throw new Exception("Cannot transmit repeat as there is no repeat command");
            }
            return ExecuteCommand(_repeatCommand);
        }

        public ADMRequestManager.ADMRequest Activate()
        {
            return ExecuteCommand(ArduinoCommand.DeviceCommand.ACTIVATE);
        }

        public ADMRequestManager.ADMRequest Deactivate()
        {
            return ExecuteCommand(ArduinoCommand.DeviceCommand.DEACTIVATE);
        }

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
