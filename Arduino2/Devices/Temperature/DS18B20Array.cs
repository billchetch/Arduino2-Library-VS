using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Messaging;

namespace Chetch.Arduino2.Devices.Temperature
{
    public class DS18B20Array : ArduinoDevice
    {
        public const String DEFAULT_NAME = "DS18B20";

        public enum SensorState
        {
            OK = 0,
            NO_SENSOR = -127, //set by dallas
            BAD_READING = 85, //set by dallas
            SENSOR_FAULT = 1,
        } 

        public enum BitResolution
        {
            HIGH = 12, // 0.0625°C
            MEDIUM = 11, //0.125
            LOW = 10, //0.25
            VERY_LOW = 9, //0.5 degrees
        }

        public class Sensor
        {
            const int BAD_READING_THRESHOLD = 3;

            public String ID;

            private float _temperature = 0.0f;
            public float Temperature 
            {
                get { return _temperature; }
                internal set 
                {
                    _temperature = value;
                    switch ((int)_temperature)
                    {
                        case (int)SensorState.NO_SENSOR:
                            State = SensorState.NO_SENSOR; break;

                        case (int)SensorState.BAD_READING:
                            _badReadingCount++;
                            if (_badReadingCount > BAD_READING_THRESHOLD)
                            {
                                State = SensorState.SENSOR_FAULT;
                            }
                            else
                            {
                                State = SensorState.BAD_READING;
                            }
                            break;

                        default:
                            _badReadingCount = 0;
                            State = SensorState.OK; break;
                    }
                } 
            }
            public SensorState State { get; internal set; } = SensorState.OK;

            private int _badReadingCount = 0;

            public Sensor(String id)
            {
                ID = id;
            }
        }

        public byte Pin { get; internal set; }

        public BitResolution Resolution { get; internal set; }

        public List<Sensor> Sensors = new List<Sensor>();

        public event EventHandler<List<Sensor>> TemperaturesUpdated;

        public DS18B20Array(String id, byte pin, BitResolution resolution, String name = DEFAULT_NAME) : base(id, name)
        {
            Pin = pin;
            Resolution = resolution;

            Category = DeviceCategory.TEMPERATURE_SENSOR;
        }

        public void AddSensor(String id)
        {
            Sensor sensor = new Sensor(id);
            Sensors.Add(sensor);
        }

        public void AddSensors(params String[] sensorIDs)
        {
            foreach(String id in sensorIDs)
            {
                AddSensor(id);
            }
        }

        protected override void AddConfig(ADMMessage message)
        {
            base.AddConfig(message);

            message.AddArgument(Pin);
            message.AddArgument((byte)Resolution);
        }

        protected override void OnConfigured(ADMMessage message)
        {
            base.OnConfigured(message);

            byte sensorCount = GetMessageValue<byte>("SensorCount", message);
            for(byte i = 0; i < sensorCount; i++)
            {
                if(i >= Sensors.Count)
                {
                    AddSensor("s" + (i + 1));
                }
            }
            //Console.WriteLine("------> Temp sensors: {0}", sensorCount);
        }

        protected override void OnData(ADMMessage message)
        {
            base.OnData(message);

            for (int i = 0; i < Sensors.Count; i++)
            {
                int idx = i;
                float temp = message.GetArgument<float>(idx);
                //Console.WriteLine("T: {0}", temp);
                Sensors[i].Temperature = temp;
            }
            TemperaturesUpdated?.Invoke(this, Sensors);
        }

        override protected int GetArgumentIndex(String fieldName, ADMMessage message)
        {
            switch (fieldName)
            {
                case "SensorCount":
                    return 2;

                default:
                    return base.GetArgumentIndex(fieldName, message);
            }
        }
    }
}
