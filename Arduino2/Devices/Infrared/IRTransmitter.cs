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
        public int RepeatThresholdLower { get; set; } = 50;
        public int RepeatThresholdUpper { get; set; } = 120; //set to 0 to disable use of repeat

        public int RepeatInterval => RepeatThresholdUpper - RepeatThresholdLower / 2;
        
        
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
            message.AddArgument(RepeatThresholdLower);
            message.AddArgument(RepeatThresholdUpper);
        }

        /*public override ADMRequestManager.ADMRequest ExecuteCommand(ArduinoCommand cmd, List<object> parameters = null)
        {
            if (cmd.Alias.Length == 2 && uint.TryParse(cmd.Alias, out _))
            {
                //split a 2 digit number in to it's components ... this is so we can have commands like 62
                //without needing to build a new command from '6' and from '2'
                int d1 = (int)char.GetNumericValue(cmd.Alias[0]);
                int d2 = (int)char.GetNumericValue(cmd.Alias[1]);

                base.ExecuteCommand(d1.ToString(), parameters);
                System.Threading.Thread.Sleep(RepeatThresholdUpper * 2);
                base.ExecuteCommand(d2.ToString(), parameters);
                return null;
            }
            else
            {
                return base.ExecuteCommand(cmd, parameters);
            }
        }*/


        protected override bool HandleCommand(ArduinoCommand cmd, List<object> parameters)
        {
            switch (cmd.Command)
            {
                case ArduinoCommand.DeviceCommand.SEND:
                    if (parameters.Count == 0) parameters.Add(0);  //ensure we send a value for repeat
                    break;
            }

            return base.HandleCommand(cmd, parameters);
        }

        /*protected override bool HandleCommandResponse(ArduinoCommand.DeviceCommand deviceCommand, ADMMessage message)
        {
            Console.WriteLine("{0} {1} {2}", message.GetArgument<UInt16>(1), message.GetArgument<UInt16>(2), message.GetArgument<UInt16>(3));
            return base.HandleCommandResponse(deviceCommand, message);
        }*/

        public ADMRequestManager.ADMRequest Transmit(String commandAlias, int repeat = 0)
        {
            return ExecuteCommand(commandAlias, repeat);
        }


        public ADMRequestManager.ADMRequest TransmitRepeat(int protocol)
        {
            if(_repeatCommand == null)
            {
                throw new Exception("Cannot transmit repeat as there is no repeat command");
            }
            _repeatCommand.SetParameter(0, protocol);
            return ExecuteCommand(_repeatCommand);
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
