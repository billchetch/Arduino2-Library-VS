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

        private ArduinoCommand _lastSendCommand = null;
        private DateTime _lastSendCommandOn;

        //if last command is same as current command and time diff (millis) between last command and current command
        //is less than RepeatInterval then use _repeatCommand if it exists.
        private ArduinoCommand _repeatCommand = null;
        public int RepeatThreshold { get; set; } = 200; //set to 0 to disable use of repeat
        private System.Timers.Timer _repeatTimer = new System.Timers.Timer();
        public bool IsRepeating => _repeatTimer.Enabled;

        public IRTransmitter(String id, String name, int transmitPin = 0, IRDB db = null) : base(id, name, db)
        {
            Category = DeviceCategory.IR_TRANSMITTER;

            _transmitPin = transmitPin;

            _repeatCommand = AddCommand(ArduinoCommand.DeviceCommand.SEND, "-repeat",
                ArduinoCommand.ParameterType.INT, 
                ArduinoCommand.ParameterType.INT, 
                ArduinoCommand.ParameterType.INT, 
                ArduinoCommand.ParameterType.BOOL);

            _repeatTimer.Interval = 10;
            _repeatTimer.AutoReset = true;
            _repeatTimer.Enabled = false;
            _repeatTimer.Elapsed += (sender, eargs) =>
            {
                bool endRepeat = ((DateTime.Now.Ticks - _lastSendCommandOn.Ticks) / TimeSpan.TicksPerMillisecond) >= RepeatThreshold;
                if (endRepeat)
                {
                    EndRepeat();
                }
            };
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
                System.Threading.Thread.Sleep(RepeatThreshold * 2);
                base.ExecuteCommand(d2.ToString(), parameters);
                return null;
            }
            else
            {
                if (cmd.Command == ArduinoCommand.DeviceCommand.SEND && cmd != _repeatCommand)
                {
                    ArduinoCommand cmd2execute = cmd;
                    if (RepeatThreshold > 0 && _lastSendCommand == cmd && ((DateTime.Now.Ticks - _lastSendCommandOn.Ticks) / TimeSpan.TicksPerMillisecond < RepeatThreshold))
                    {
                        if (!_repeatTimer.Enabled)
                        {
                            _repeatCommand.SetParameter(0, cmd.Parameters[0]);
                            _repeatCommand.SetParameter(1, cmd.Parameters[1]);
                            _repeatCommand.SetParameter(2, cmd.Parameters[2]);
                            _repeatCommand.SetParameter(3, true);

                            _repeatTimer.Enabled = true;
                            cmd2execute = _repeatCommand;
                        } else
                        {
                            cmd2execute = null;
                        }
                    }
                    _lastSendCommand = cmd;
                    _lastSendCommandOn = DateTime.Now;

                    if (cmd2execute == null)
                    {
                        return null;
                    }
                    else
                    {
                        //Console.WriteLine("Sending {0}", cmd2execute.Alias);
                        return base.ExecuteCommand(cmd2execute, parameters);
                    }
                }
                else
                {
                    //Console.WriteLine("Send {0}", cmd.Alias);
                    return base.ExecuteCommand(cmd, parameters);
                }
            } 
        }


        
        /*protected override bool HandleCommandResponse(ArduinoCommand.DeviceCommand deviceCommand, ADMMessage message)
        {
            Console.WriteLine("{0} {1} {2} {3} {4}", message.GetArgument<UInt16>(1), message.GetArgument<UInt16>(2), message.GetArgument<UInt16>(3), message.GetArgument<bool>(4), message.GetArgument<bool>(5));
            return base.HandleCommandResponse(deviceCommand, message);
        }*/

        public ADMRequestManager.ADMRequest EndRepeat()
        {
            if (IsRepeating)
            {
                _repeatCommand.SetParameter(3, false);
                _repeatTimer.Enabled = false;
                return ExecuteCommand(_repeatCommand);
            }
            else
            {
                return null;
            }
        }
        public ADMRequestManager.ADMRequest TransmitWithRepeat(String commandAlias)
        {
            var cmd = GetCommand(commandAlias);
            _repeatCommand.SetParameter(0, cmd.Parameters[0]);
            _repeatCommand.SetParameter(1, cmd.Parameters[1]);
            _repeatCommand.SetParameter(2, cmd.Parameters[2]);
            _repeatCommand.SetParameter(3, true);

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
