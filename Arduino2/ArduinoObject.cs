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
            public const int STATE = 64; //Tells of the fundamental state of the device and in this regard a change of value would be an event to record
            public const int DATA = 128; //Tells of the data produced by the device and in this regard would be logged in periodic intervals
            public const int METADATA = 256; //Tells of something about the data e.g. when data was last updated ... something to be broadcast perhaps

            public bool IsState => HasAttribute(STATE);
            public bool IsData => HasAttribute(DATA);
            public bool IsMetaData => HasAttribute(METADATA);

            public ArduinoPropertyAttribute(int attributtes) : base(attributtes)
            {}

            public ArduinoPropertyAttribute(int attributtes, Object defaultValue) : base(attributtes, defaultValue)
            {}
        }

        public class MessageReceivedArgs : EventArgs
        {
            public ADMMessage Message { get; internal set; }

            public MessageReceivedArgs(ADMMessage message)
            {
                Message = message;
            }
        }

        

        public event EventHandler<MessageReceivedArgs> MessageReceived;

        public DateTime LastMessagedHandledOn { get; internal set; }

        public ADMMessage LastMessagedHandled { get; internal set; }


        [ArduinoProperty(PropertyAttribute.IDENTIFIER)]
        public String ID { get; internal set; }

        [ArduinoProperty(PropertyAttribute.IDENTIFIER)]
        abstract public String UID { get; }

        [ArduinoProperty(PropertyAttribute.ERROR, null)]
        public String Error
        {
            get { return Get<String>(); }
            internal set { Set(value, true, true); }
        }

        [ArduinoProperty(PropertyAttribute.ERROR)]
        public int ErrorContextCode { get; internal set; }

        [ArduinoProperty(PropertyAttribute.ERROR)]
        public int ErrorCode { get; internal set; }

        [ArduinoProperty(PropertyAttribute.ERROR)]
        public int ErrorSubCode { get; internal set; }

        public String ErrorInfo { get; internal set; }

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
                if(prop == null)
                {
                    throw new ArgumentException(String.Format("Cannot assign message value as property {0} does not exist", fieldName));
                }
                prop.SetValue(this, GetMessageValue(prop.Name, prop.PropertyType, message));
            }
        }

        virtual public void HandleMessage(ADMMessage message)
        {
            LastMessagedHandledOn = DateTime.Now;
            LastMessagedHandled = message;

            if(message != null && MessageReceived != null)
            {
                MessageReceived(this, new MessageReceivedArgs(message));
            }
        }

        protected void SetError(String error, String info = "N/A", int errorContextCode = 0, int errorCode = 0, int errorSubCode = 0)
        {
            ErrorContextCode = errorContextCode;
            ErrorCode = errorCode;
            ErrorSubCode = errorSubCode;
            ErrorInfo = info;

            //this will trigger a property changed event to listen to
            Error = error;
        }
    }
}
