using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Messaging;
using Chetch.Arduino;

namespace Chetch.Arduino2
{
    abstract public class ArduinoDevice
    {
        public const int REPORT_INTERVAL_NONE = -1;

        public enum DeviceState
        {
            CREATED = 1,
            INITIALISING,
            INITIALISED,
            CONFIGURING,
            CONFIGURED,
        }

        public DeviceState State { get; internal set; } = DeviceState.CREATED;

        public enum MessageField
        {
            ENABLED = 0,
            REPORT_INTERVAL,
            DEVICE_NAME,
            CATEGORY,
            DEVICE_COMMAND,
        }

        public ArduinoDeviceManager ADM { get; set; }

        
        public String ID { get; internal set; }
        public String Name { get; internal set; }
        public byte BoardID { get; set; }

        public DeviceCategory Category { get; protected set; }
        public bool Enabled { get; internal set; } = true;

        public int ReportInterval { get; set; }

        public bool IsReady => State == DeviceState.CONFIGURED;

        private Dictionary<String, ArduinoCommand> _commands = new Dictionary<String, ArduinoCommand>();


        public ArduinoDevice(String id, String name)
        {
            ID = id;
            Name = name.ToUpper();

            AddCommand(ArduinoCommand.DeviceCommand.ENABLE);
        }

        public ADMMessage CreateMessage(MessageType messageType)
        {
            var message = new ADMMessage();
            message.Type = messageType;
            message.TargetID = BoardID;
            message.SenderID = BoardID;

            return message;
        }

        public int GetArgumentIndex(ADMMessage message, MessageField field)
        {
            switch (field)
            {
                case MessageField.DEVICE_COMMAND:
                    return 0;

                case MessageField.ENABLED:
                    return (message.Type == MessageType.COMMAND_RESPONSE || message.Type == MessageType.COMMAND) ? 1 : (int)field;

                default:
                    return (int)field;
            }
        }

        virtual public void Initialise()
        {
            State = DeviceState.INITIALISING;
            var message = CreateMessage(MessageType.INITIALISE);
            message.AddArgument(Name == null ? "N/A" : Name);
            message.AddArgument((byte)Category);
            ADM.SendMessage(message);
        }

        virtual public void Configure()
        {
            State = DeviceState.CONFIGURING;
            var message = CreateMessage(MessageType.CONFIGURE);
            message.AddArgument(Enabled ? (byte)1 : (byte)0);
            message.AddArgument(ReportInterval);
            ADM.SendMessage(message);
        }

        virtual public void HandleMessage(ADMMessage message)
        {
            int argIdx = 0;
            switch (message.Type)
            {
                case MessageType.INITIALISE_RESPONSE:
                    State = DeviceState.INITIALISED;
                    Configure();
                    break;

                case MessageType.CONFIGURE_RESPONSE:
                    State = DeviceState.CONFIGURED;
                    break;

                case MessageType.STATUS_RESPONSE:
                    break;

                case MessageType.COMMAND_RESPONSE:
                    argIdx = GetArgumentIndex(message, MessageField.DEVICE_COMMAND);
                    ArduinoCommand.DeviceCommand deviceCommand = (ArduinoCommand.DeviceCommand)message.ArgumentAsByte(argIdx);
                    HandleCommandResponse(deviceCommand, message);
                    break;
            }
        }

        public void AddCommand(ArduinoCommand cmd)
        {
            String alias = cmd.Alias == null ? cmd.Command.ToString() : cmd.Alias;
            alias = alias.ToLower();
            if (_commands.ContainsKey(alias))
            {
                throw new Exception(String.Format("Cannot add command {0} as it already is present", alias));
            }
            _commands[alias] = cmd;
        }

        public void AddCommand(ArduinoCommand.DeviceCommand deviceCommand, String alias, params ValueType[] parameters)
        {
            ArduinoCommand cmd = new ArduinoCommand(deviceCommand, alias);
            if (parameters != null) cmd.AddParameters(parameters);
            AddCommand(cmd);

        }

        public void AddCommand(ArduinoCommand.DeviceCommand deviceCommand)
        {
            AddCommand(deviceCommand, deviceCommand.ToString().ToLower());
        }

        ArduinoCommand GetCommand(String alias)
        {
            alias = alias.ToLower();
            return _commands.ContainsKey(alias) ? _commands[alias] : null;
        }

        virtual public void ExecuteCommand(String commandAlias, List<ValueType> parameters = null)
        {
            ArduinoCommand cmd = GetCommand(commandAlias);
            if(cmd == null)
            {
                throw new Exception(String.Format("Device {0} doesnot have command with alias {1}", ID, commandAlias));
            }

            switch (cmd.Command)
            {
                case ArduinoCommand.DeviceCommand.ENABLE:
                    Enabled = (bool)parameters[0];
                    break;
            }


            ExecuteCommand(cmd, parameters);
        }

        public void ExecuteCommand(String commandAlias, params ValueType[] parameters)
        {
            ExecuteCommand(commandAlias, parameters.ToList());
        }

        protected void ExecuteCommand(ArduinoCommand cmd, List<ValueType> parameters = null)
        {
            if (cmd.IsCompound)
            {
                if (parameters != null) throw new Exception("Cannot add temporary parameters to compound command");
            }
            else
            {
                if (cmd.IsDelay)
                {
                    Thread.Sleep(cmd.DelayInterval);
                }
                else
                {
                    if (cmd.Command == ArduinoCommand.DeviceCommand.NONE)
                    {
                        throw new Exception("DeviceCommand is NONE but ths is not a delay");
                    }

                    //assume the command is a message
                    var cm = CreateMessage(MessageType.COMMAND);
                    cm.AddArgument((byte)cmd.Command);
                    foreach (var p in cmd.Parameters)
                    {
                        cm.AddArgument(Chetch.Utilities.Convert.ToBytes(p));
                    }
                    if (parameters != null)
                    {
                        foreach (var p in parameters)
                        {
                            cm.AddArgument(Chetch.Utilities.Convert.ToBytes(p));
                        }
                    }
                    ADM.SendMessage(cm);
                }
            }
        }

        virtual protected void HandleCommandResponse(ArduinoCommand.DeviceCommand deviceCommand, ADMMessage message)
        {
            int argIdx = 0;
            switch (deviceCommand)
            {
                case ArduinoCommand.DeviceCommand.ENABLE:
                    argIdx = GetArgumentIndex(message, MessageField.ENABLED);
                    Enabled = message.ArgumentAsBool(argIdx);
                    break;
            }
        }
    }

    public class TestDevice01 : ArduinoDevice
    {

        new public enum MessageField
        {
            TEST_VALUE = 0
        }

        public TestDevice01(String id, String name = "TEST01") : base(id, name)
        {
            Category = DeviceCategory.DIAGNOSTICS;
            Enabled = false;
        }

        public int GetArgumentIndex(ADMMessage message, MessageField field)
        {
            switch (field)
            {
                default:
                    return (int)field;
            }
        }

        override public void Configure()
        {
            base.Configure();

        }
    }
}
