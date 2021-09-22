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

        protected ADMMessage.MessageTags MessageTags { get; } = new ADMMessage.MessageTags();

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
        abstract protected int GetArgumentIndex(String fieldName, ADMMessage message);

        protected dynamic GetMessageValue(String fieldName, Type type, ADMMessage message)
        {
            int argIdx = GetArgumentIndex(fieldName, message);
            return message.GetArgument(argIdx, type);
        }

        protected T GetMessageValue<T>(String fieldName, ADMMessage message)
        {
            int argIdx = GetArgumentIndex(fieldName, message);
            return message.GetArgument<T>(argIdx);
        }

        protected void AssignMessageValues(ADMMessage message, params String[] fieldNames)
        {
            Type type = GetType();
            foreach (var fieldName in fieldNames)
            {
                var prop = type.GetProperty(fieldName);
                prop.SetValue(this, GetMessageValue(prop.Name, prop.PropertyType, message));
            }
        }

        virtual public void HandleMessage(ADMMessage message)
        {
            if (message.Tag > 0)
            {
                message.Tag = MessageTags.Release(message.Tag);
            }
        }
    }
}
