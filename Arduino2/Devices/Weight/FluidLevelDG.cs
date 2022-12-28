using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2.Devices.Weight
{
    public class FluidLevelDG : ArduinoDeviceGroup
    {
        private FluidLevel _fluidLevel;
        private Button _tareButton;
        private Button _resetButton;
        private long _originalOffset;

        public FluidLevelDG(String id, FluidLevel fluidLevel, byte tarePin, byte resetPin, String name = "FLDG") : base(id, name)
        {
            _fluidLevel = fluidLevel;
            _originalOffset = fluidLevel.Offset;

            _tareButton = new Button(id + "_tare", tarePin);
            _resetButton = new Button(id + "_rst", resetPin);


            _tareButton.Released += (sender, eargs) =>
            {
                _fluidLevel.Tare();
            };

            _resetButton.Released += (sender, eargs) =>
            {
                _fluidLevel.Offset = _originalOffset;
            };

            AddDevices(_fluidLevel, _tareButton, _resetButton);
        }


        protected override void HandleDevicePropertyChange(ArduinoDevice device, System.Reflection.PropertyInfo property)
        {
            //empty
        }
    }
}
