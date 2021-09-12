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

       
        protected ADMMessage.MessageTags MessageTags { get; } = new ADMMessage.MessageTags();

        private Dictionary<byte, List<byte>> _tagMappings = new Dictionary<byte, List<byte>>();

        public ArduinoDeviceManager ADM { get; set; }

        
        public String ID { get; internal set; }

        public String FullID => ADM.ID + "-" + ID;

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

        virtual public void Serialize(Dictionary<String, Object> vals)
        {
            vals["ID"] = ID;
            vals["Name"] = Name;
            vals["Category"] = Category.ToString();
            vals["Enabled"] = Enabled;
        }

        public ADMMessage CreateMessage(MessageType messageType, bool tag = false)
        {
            var message = new ADMMessage();
            message.Type = messageType;
            message.TargetID = BoardID;
            message.SenderID = BoardID;
            if (tag)
            {
                message.Tag = MessageTags.CreateTag();
            }

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

            if(message.Tag > 0)
            {
                message.Tag = MessageTags.Release(message.Tag);
            }
        }

        public ArduinoCommand AddCommand(ArduinoCommand cmd)
        {
            String alias = cmd.Alias;
            if (_commands.ContainsKey(alias))
            {
                throw new Exception(String.Format("Cannot add command {0} as it already is present", alias));
            }
            _commands[alias] = cmd;
            return cmd;
        }

        public ArduinoCommand AddCommand(ArduinoCommand.DeviceCommand deviceCommand, String alias, params ValueType[] parameters)
        {
            ArduinoCommand cmd = new ArduinoCommand(deviceCommand, alias);
            if (parameters != null) cmd.AddParameters(parameters);
            return AddCommand(cmd);

        }

        public ArduinoCommand AddCommand(ArduinoCommand.DeviceCommand deviceCommand)
        {
            return AddCommand(deviceCommand, deviceCommand.ToString());
        }

        protected ArduinoCommand GetCommand(String alias)
        {
            alias = alias.Trim().ToLower();
            return _commands.ContainsKey(alias) ? _commands[alias] : null;
        }


        
        private Task _executeCommandTask = null;
        public byte ExecuteCommand(String commandAlias, List<ValueType> parameters = null)
        {
            if (!IsReady)
            {
                throw new InvalidOperationException(String.Format("Device {0} Cannot execute command {1} as device is not ready", ID, commandAlias));
            }
            ArduinoCommand cmd = GetCommand(commandAlias);
            if(cmd == null)
            {
                throw new Exception(String.Format("Device {0} doesnot have command with alias {1}", ID, commandAlias));
            }

            int ttl = Math.Max(ADMMessage.MessageTags.DEFAULT_TTL, cmd.TotalDelayInterval + 1000);
            Console.WriteLine("Executing command {0} with tag set ttl {1}", commandAlias, ttl);
            byte tag = MessageTags.CreateTagSet(ttl);
            Action action = () =>
            {
                ExecuteCommand(cmd, tag, parameters);
            };
            
            if (_executeCommandTask == null || _executeCommandTask.IsCompleted)
            {
                _executeCommandTask = new Task(action);
                _executeCommandTask.Start();
            }
            else
            {
                _executeCommandTask = _executeCommandTask.ContinueWith(antecedent => action());
            }
            return tag;
        }

        public byte ExecuteCommand(String commandAlias, params ValueType[] parameters)
        {
            return ExecuteCommand(commandAlias, parameters.ToList());
        }

        public byte ExecuteCommand(ArduinoCommand.DeviceCommand deviceCommand, params ValueType[] parameters)
        {
            return ExecuteCommand(deviceCommand.ToString().ToLower(), parameters.ToList());
        }

        protected void ExecuteCommand(ArduinoCommand cmd, byte tag, List<ValueType> parameters = null)
        {
            for (int i = 0; i < cmd.Repeat; i++)
            {
                if (cmd.IsCompound)
                {
                    //TODO: how to handle parameters in the case of a compound command???
                    foreach (var c in cmd.Commands)
                    {
                        ExecuteCommand(c, tag, parameters);
                    }
                }
                else
                {
                    if (cmd.IsDelay)
                    {
                        Console.WriteLine("------> Delaing execution for {0} ms", cmd.DelayInterval);
                        Thread.Sleep(cmd.DelayInterval);
                    }
                    else
                    {
                        if (cmd.Command == ArduinoCommand.DeviceCommand.NONE)
                        {
                            throw new Exception("DeviceCommand is NONE but ths is not a delay");
                        }


                        List<ValueType> allParams = new List<ValueType>();
                        if (cmd.Parameters != null) allParams.AddRange(cmd.Parameters);
                        if (parameters != null) allParams.AddRange(parameters);
                        if (HandleCommand(cmd, allParams))
                        {
                            //assume the command is a message
                            var cm = CreateMessage(MessageType.COMMAND);
                            cm.Tag = MessageTags.CreateTagInSet(tag);
                            cm.AddArgument((byte)cmd.Command);
                            foreach (var p in allParams)
                            {
                                cm.AddArgument(Chetch.Utilities.Convert.ToBytes(p));
                            }
                            Console.WriteLine("------> Sending command {0}", cmd.Command);
                            ADM.SendMessage(cm);
                        }
                    } //if delay
                } //if compound
            } //end repeat
        }

        //Called before sending command (for instance to update state) return value determines whether command is executed.
        virtual protected bool HandleCommand(ArduinoCommand cmd, List<ValueType> parameters)
        {
            switch (cmd.Command)
            {
                case ArduinoCommand.DeviceCommand.ENABLE:
                    Enabled = (bool)parameters[0];
                    break;
            }
            return true;
        }

        //Called when a command response is received
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

        public void Enable(bool enabled = true)
        {
            ExecuteCommand(ArduinoCommand.DeviceCommand.ENABLE, enabled);
        }

        public void Disable()
        {
            Enable(false);
        }
    }

    public class TestDevice01 : ArduinoDevice
    {

        new public enum MessageField
        {
            TEST_VALUE = 0
        }

        public int TestValue { get; internal set; }

        public TestDevice01(String id, String name = "TEST01") : base(id, name)
        {
            Category = DeviceCategory.DIAGNOSTICS;
            Enabled = true;

            var enable = ArduinoCommand.Enable(true);
            var disable = ArduinoCommand.Enable(false);
            var d1 = ArduinoCommand.Delay(1000);
            var d5 = ArduinoCommand.Delay(5000);
            var cmd = AddCommand(ArduinoCommand.DeviceCommand.COMPOUND, "test");
            cmd.Repeat = 4;
            cmd.AddCommands(enable, d5, disable, d1, enable, d1, disable);
        }

        public override void Serialize(Dictionary<string, object> vals)
        {
            base.Serialize(vals);
            vals["TestValue"] = TestValue;
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

        public override void HandleMessage(ADMMessage message)
        {
            base.HandleMessage(message);
            int argIdx;
            switch (message.Type)
            {
                case MessageType.DATA:
                    argIdx = GetArgumentIndex(message, MessageField.TEST_VALUE);
                    TestValue = message.ArgumentAsInt(argIdx);
                    break;
            }
        }
    }
}
