using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Utilities;

namespace Chetch.Arduino2
{
    abstract public class ArduinoObject : DataSourceObject
    {
        [AttributeUsage(AttributeTargets.Property)]
        public class ArduinoPropertyAttribute : DataSourceObject.PropertyAttribute
        {
            public const int STATE = 64;
            public const int DATA = 128;
            
            public bool IsState => HasAttribute(STATE);
            public bool IsData => HasAttribute(DATA);

            public ArduinoPropertyAttribute(int attributtes) : base(attributtes)
            {}

            public ArduinoPropertyAttribute(int attributtes, Object defaultValue) : base(attributtes, defaultValue)
            {}
        }

        [ArduinoProperty(PropertyAttribute.IDENTIFIER)]
        public String ID { get; internal set; }

        [ArduinoProperty(PropertyAttribute.IDENTIFIER)]
        abstract public String UID { get; }

        [ArduinoProperty(PropertyAttribute.ERROR, null)]
        public String Error
        {
            get { return Get<String>(); }
            internal set { Set(value, true); }
        }
    }
}
