﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2.Devices.Infrared
{
    public abstract class IRTransmitter : IRDevice
    {
        private bool _enabled = false;
        private int _enablePin; //HIGH output means the transmitter is disabled (as there is no voltage across it)
        private int _transmitPin;

        //if last command is same as current command and time diff (millis) between last command and current command
        //is less than RepeatInterval then use _repeatCommand if it exists.
        private ArduinoCommand _repeatCommand = null;
        public int RepeatInterval { get; set; } = 200;
        public bool UseRepeatCommand = true;
        
        public IRTransmitter(String id, String name, int enablePin, int transmitPin, IRDB db = null) : base(id, name, db)
        {
            Category = DeviceCategory.IR_TRANSMITTER;

            _enablePin = enablePin;
            _transmitPin = transmitPin;
            
        }

        public IRTransmitter(int enablePin, int transmitPin, IRDB db = null) : this("irt" + enablePin, "IRT", enablePin, transmitPin, db) { }

        public override void ReadDevice()
        {
            base.ReadDevice();
            if(DB != null)
            {
                Commands.Clear();
                Commands.AddRange(DB.GetCommands(DeviceName));

                _repeatCommand = GetCommand(REPEAT_COMMAND);
            }
        }


        override public void ExecuteCommand(String commandAlias, ExecutionArguments xargs)
        {
            if(commandAlias.Length == 2 && uint.TryParse(commandAlias, out _))
            {
                //split a 2 digit number in to it's components ... this is so we can have commands like 62
                //without needing to build a new command from '6' and from '2'
                int d1 = (int)char.GetNumericValue(commandAlias[0]);
                int d2 = (int)char.GetNumericValue(commandAlias[1]);

                base.ExecuteCommand(d1.ToString(), xargs);
                System.Threading.Thread.Sleep(RepeatInterval * 2);
                base.ExecuteCommand(d2.ToString(), xargs);
            } else
            {
                base.ExecuteCommand(commandAlias, xargs);
            }
        }

        override protected void ExecuteCommand(ArduinoCommand command, ExecutionArguments xargs)
        {
            if(!_enabled){
                List<ArduinoDevice> devices = Mgr.GetDevicesByPin(_transmitPin);

                foreach (var device in devices)
                {
                    if (device is IRTransmitter && device != this)
                    {
                        ((IRTransmitter)device).Disable();
                    }
                }

                Enable();
            }

            base.ExecuteCommand(command, xargs);
        }

        override protected void SendCommand(ArduinoCommand command, ExecutionArguments xargs)
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
        }

        public override void HandleMessage(ADMMessage message)
        {
            //check if the transmit pin is viable
            if(_transmitPin == ArduinoPin.BOARD_SPECIFIED && message.HasValue("TP"))
            {
                int tp = message.GetInt("TP");
                ConfigurePin(tp, PinMode.PwmOutput);
                Mgr.UpdateDevice(this);
                _transmitPin = tp;
            }

            base.HandleMessage(message);
        }
    }
}
