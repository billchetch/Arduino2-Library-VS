using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2.Devices.Weight
{
    public class FluidLevel : Chetch.Arduino2.Devices.Weight.LoadCell
    {
        const double WATER_DENSITY = 1.0; //grapms per cm3
        const double DIESEL_DENSITY = 0.9;

        private double _fluiddDensity;
        private double _weightOfUnitHeight;
        private double _maxHeight;
        private double _pipeWeight;
        private List<double> _levelMarkers = new List<double>();

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


        public event EventHandler<double> LevelUpdated;

        public FluidLevel(String id, byte doutPin, byte sckPin, double pipeDiameter, double pipeWeight, double maxHeight, double fluidDensity = WATER_DENSITY) : base(id, doutPin, sckPin)
        {
            _fluiddDensity = fluidDensity;
            double pipeRadius = pipeDiameter / 2.0;
            _weightOfUnitHeight = pipeRadius * pipeRadius * System.Math.PI * _fluiddDensity;
            _pipeWeight = pipeWeight;
            _maxHeight = maxHeight;

            MinWeight = 0;
            MaxWeight = (int)_pipeWeight;
        }

        public void SetLevelMarkers(params double[] markers)
        {
            _levelMarkers.Clear();
            _levelMarkers.Add(0);
            foreach (var m in markers)
            {
                if (m <= 0 || m >= _maxHeight) throw new ArgumentOutOfRangeException(String.Format("{0} is out of range", m));
                _levelMarkers.Add(m);
            }
            _levelMarkers.Add(_maxHeight);
            _levelMarkers.Sort();
        }

        protected override void OnSetWeight()
        {
            base.OnSetWeight();

            double weightDelta = _pipeWeight - Weight;
            Height = System.Math.Min(_maxHeight, weightDelta / _weightOfUnitHeight);
            if (Height <= 0 || Height >= _maxHeight || _levelMarkers.Count == 0)
            {
                Level = Height / _maxHeight;
            }
            else
            {
                //we linearly interpolate
                double interval = 1 / (_levelMarkers.Count - 1);
                for (int i = 1; i < _levelMarkers.Count; i++)
                {
                    double marker = _levelMarkers[i];
                    if (marker >= Height)
                    {
                        //TODO: make the linear interpolation calculation
                        //marker - _levelMarkers[i - 1];
                    }
                }
            }
            LevelUpdated?.Invoke(this, Level);
        }

        public override void Tare()
        {
            Offset = RawValue - (Int32)(_pipeWeight * Scale);
        }
    }
}
