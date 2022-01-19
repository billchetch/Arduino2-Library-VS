using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2.Devices.Buzzers
{
    public class Buzzer : SwitchDevice
    {

        public int DefaultSilenceDuration { get; set; } = 3000; //in millis

        private bool silenced = true;

        public bool IsSilenced => silenced;

        public Buzzer(String id, byte pin, int silenceDuration = 0) : base(id, SwitchMode.ACTIVE, pin)
        {
            if (silenceDuration > 0) DefaultSilenceDuration = silenceDuration;
            AddCommand(ArduinoCommand.DeviceCommand.SILENCE);
        }

        protected override void ExecuteCommand(ArduinoCommand cmd, byte tag, List<object> parameters = null)
        {
            if(cmd.Command == ArduinoCommand.DeviceCommand.SILENCE)
            {
                if (IsOff)
                {
                    return;
                } else
                {
                    int duration = parameters != null && parameters.Count == 1 ? (int)parameters[0] : DefaultSilenceDuration;
                    if (duration > 0)
                    {
                        silenced = true;
                        TurnOff(duration);
                    }
                }
            } 
            else
            {
                base.ExecuteCommand(cmd, tag, parameters);
            }
        }

        public override void SetPosition(SwitchPosition newPosition, int duration = 0)
        {
            base.SetPosition(newPosition, duration);
            if (newPosition == SwitchPosition.ON) silenced = false;
        }
    }
}
