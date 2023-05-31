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

        private int silencedFor = 0;
        private DateTime silencedOn;

        public bool IsSilenced => (silencedFor > 0 && (DateTime.Now - silencedOn).TotalMilliseconds <= silencedFor);

        public Buzzer(String id, byte pin, int silenceDuration = 0) : base(id, SwitchMode.ACTIVE, pin)
        {
            if (silenceDuration > 0) DefaultSilenceDuration = silenceDuration;
            AddCommand(ArduinoCommand.DeviceCommand.SILENCE);
        }

        public override ADMRequestManager.ADMRequest ExecuteCommand(ArduinoCommand cmd, List<object> parameters = null)
        {
            if (cmd.Command == ArduinoCommand.DeviceCommand.SILENCE)
            {
                if (IsOn && silencedFor > 0)
                {
                    TurnOff(silencedFor);
                }
                return null;
            }
            else
            {
                return base.ExecuteCommand(cmd, parameters);
            }
        }

        public void Silence(int duration = 0)
        {
            silencedOn = DateTime.Now;
            silencedFor = duration <= 0 ? DefaultSilenceDuration : duration;
            ExecuteCommand(ArduinoCommand.DeviceCommand.SILENCE);
        }

        public void Unsilence()
        {
            if (IsSilenced)
            {
                silencedFor = 0;
                TurnOn();
            }
        }
    }
}
