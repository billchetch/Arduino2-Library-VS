﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Messaging;

namespace Chetch.Arduino2.Devices
{
    public class SwitchDevice : ArduinoDevice
    {
        public const String DEFAULT_NAME = "SWITCH";

        public enum SwitchMode
        {
            ACTIVE = 1,
            PASSIVE
        }

        public enum SwitchPosition
        {
            OFF = 0,
            ON = 1,
        }

        public SwitchMode Mode { get; internal set; }

        public bool IsActive => Mode == SwitchMode.ACTIVE;
        public bool IsPassive => Mode == SwitchMode.PASSIVE;

        [ArduinoProperty(ArduinoPropertyAttribute.STATE | ArduinoPropertyAttribute.DATA)]
        public SwitchPosition Position 
        {
            get { return Get<SwitchPosition>(); }
            internal set { Set(value, IsReady); }
        }

        public bool IsOn => Position == SwitchPosition.ON;
        public bool IsOff => Position == SwitchPosition.OFF;

        private bool _pinState;
        public bool PinState 
        { 
            get { return _pinState; } 
            internal set 
            {
                _pinState = value;
                Position = _pinState ? SwitchPosition.ON : SwitchPosition.OFF;
            } 
        }

        private System.Timers.Timer durationTimer = null;
        
        public byte Pin { get; internal set; }

        public int Tolerance { get; internal set; } = 0;

        public SwitchDevice(String id, SwitchMode mode, byte pin, SwitchPosition intialPosition = SwitchPosition.OFF, int tolerance = 0, String name = DEFAULT_NAME) : base(id, name)
        {
            Mode = mode;
            Pin = pin;
            PinState = (intialPosition == SwitchPosition.ON);
            Tolerance = tolerance;
            Category = DeviceCategory.SWITCH;

            if (Mode == SwitchMode.ACTIVE)
            { 
                AddCommand(ArduinoCommand.DeviceCommand.ON);
                AddCommand(ArduinoCommand.DeviceCommand.OFF);
            }
        }

        public SwitchDevice(String id, SwitchMode mode, byte pin, int tolerance, String name = DEFAULT_NAME) : this(id, mode, pin, SwitchPosition.OFF, tolerance, name)
        {}

        override protected int GetArgumentIndex(String fieldName, ADMMessage message)
        {
            switch (fieldName)
            {
                case "PinState":
                    return message.IsConfigRelated ? 4 : (message.IsCommandRelated? 1 : 0);
               
                default:
                    return base.GetArgumentIndex(fieldName, message);
            }
        }

        protected override void AddConfig(ADMMessage message)
        {
            base.AddConfig(message);

            message.AddArgument((byte)Mode);
            message.AddArgument(Pin);
            message.AddArgument(PinState);
            message.AddArgument(Tolerance);
        }

        public override void HandleMessage(ADMMessage message)
        {
            switch (message.Type)
            {
                case MessageType.DATA:
                    AssignMessageValues(message, "PinState");
                    break;
            }

            base.HandleMessage(message);
        }

        public void SetPosition(SwitchPosition newPosition, int duration = 0)
        {
            if (IsPassive)
            {
                throw new InvalidOperationException(String.Format("Cannot set position of switch device {0} because it not an active switch", ID));
            }

            if(duration > 0 && newPosition != Position)
            {
                if(durationTimer == null)
                {
                    durationTimer = new System.Timers.Timer();
                    durationTimer.AutoReset = false;
                    durationTimer.Interval = duration;
                    SwitchPosition set2position = Position;
                    durationTimer.Elapsed += (object sender, System.Timers.ElapsedEventArgs e) => { 
                        SetPosition(set2position);
                    };
                } else
                {
                    durationTimer.Stop();
                }
                //durationTimerElapsedPosition = Position;
                durationTimer.Start();
            }
            switch (newPosition)
            {
                case SwitchPosition.ON:
                    ExecuteCommand(ArduinoCommand.DeviceCommand.ON);
                    break;
                case SwitchPosition.OFF:
                    ExecuteCommand(ArduinoCommand.DeviceCommand.OFF);
                    break;
            }
            
        }

        public void TurnOn(int duration = 0)
        {
            SetPosition(SwitchPosition.ON, duration);
        }

        public void TurnOff(int duration = 0)
        {
            SetPosition(SwitchPosition.OFF, duration);
        }

        
        protected override void HandleCommandResponse(ArduinoCommand.DeviceCommand deviceCommand, ADMMessage message)
        {
            switch(deviceCommand)
            {
                case ArduinoCommand.DeviceCommand.ON:
                case ArduinoCommand.DeviceCommand.OFF:
                    AssignMessageValues(message, "PinState");
                    break;
            }
            base.HandleCommandResponse(deviceCommand, message);
        }
    }
}
