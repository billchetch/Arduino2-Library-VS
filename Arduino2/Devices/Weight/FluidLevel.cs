using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Utilities;

namespace Chetch.Arduino2.Devices.Weight
{
    public class FluidLevel : Chetch.Arduino2.Devices.Weight.LoadCell
    {
        const double WATER_DENSITY = 1.0; //grapms per cm3
        const double DIESEL_DENSITY = 0.9;


        public enum FluidLevelState
        {
            ERROR_ZERO_WEIGHT = -1,
            EMPTY = 0,
            ALMOST_EMPTY = 5,
            OK = 10,
            ALMOST_FULL = 90,
            FULL = 95,
            OVERFLOW = 100
        }

        private double _fluiddDensity;
        private double _weightOfUnitHeight;
        private double _fullHeight; //height considered for level at 100% (nb. this can exceed 100% hence the overflow state) 
        private double _pipeWeight;
        
        public double Capacity { get; set; } = 0; //how much in Litres of fluid there is if Level is 1 (100%)
        public double Remaining { get { return Capacity * Level; } } //how many litres of fluid there currently are


        [ArduinoProperty(ArduinoPropertyAttribute.DATA, 0)]
        public double Height
        {
            get { return Get<double>(); }
            protected set { Set(value, IsReady, IsReady); } //Note: this will fire a property change even if no value change
        }

        [ArduinoProperty(ArduinoPropertyAttribute.DATA, 0)]
        public double Level
        {
            get { return Get<double>(); }
            protected set { Set(value, IsReady, IsReady); } //Note: this will fire a property change even if no value change
        }

        public FluidLevelState LevelState = FluidLevelState.EMPTY;

        public ThresholdMap<FluidLevelState, double> LevelThresholds = new ThresholdMap<FluidLevelState, double>();

        public event EventHandler<double> LevelUpdated;

        //TODO: level to volume markers
        public double Volume { get; internal set; } = -1.0;

        public FluidLevel(String id, byte doutPin, byte sckPin, double pipeDiameter, double pipeWeight, double fullHeight, double fluidDensity = WATER_DENSITY) : base(id, doutPin, sckPin)
        {
            _fluiddDensity = fluidDensity;
            double pipeRadius = pipeDiameter / 2.0;
            _weightOfUnitHeight = pipeRadius * pipeRadius * System.Math.PI * _fluiddDensity;

            if(pipeWeight <= _weightOfUnitHeight * fullHeight)
            {
                throw new ArgumentOutOfRangeException(String.Format("Pipe weight {0} is less than weight of displaed fluid {1} when full", pipeWeight, _weightOfUnitHeight * fullHeight));
            }

            _pipeWeight = pipeWeight;
            _fullHeight = fullHeight;

            MinWeight = 0;
            MaxWeight = (int)_pipeWeight;
        }

        protected override void OnSetWeight()
        {
            base.OnSetWeight();

            double weightDelta = _pipeWeight - Weight;
            Height = weightDelta / _weightOfUnitHeight;
            Level = 100.0 * (Height / _fullHeight);

            if (Weight <= 0)
            {
                LevelState = FluidLevelState.ERROR_ZERO_WEIGHT;
            }
            else
            {
                LevelState = LevelThresholds.GetValue(Level);
            }

            //TODO: add a calculate volume thingy

            LevelUpdated?.Invoke(this, Level);
        }
    }
}
