using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2.Devices
{
    public class Button : SwitchDevice
    {
        const int DEFAULT_TOLERANCE = 100;

        public event EventHandler Pressed;
        public event EventHandler Released;


        public Button(String id, byte pin, int tolerance = DEFAULT_TOLERANCE) : base(id, SwitchMode.PASSIVE, pin, tolerance)
        {
            Switched += OnSwitched;
        }

        protected void OnSwitched(object sender, SwitchPosition position)
        {
            if (Position == SwitchPosition.OFF && position == SwitchPosition.ON)
            {
                OnPress();
            }

            if (Position == SwitchPosition.ON && position == SwitchPosition.OFF)
            {
                OnRelease();
            }
        }

        protected void OnPress()
        {
            Pressed?.Invoke(this, null);
        }

        protected void OnRelease()
        {
            Released?.Invoke(this, null);
        }
    }
}
