using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Messaging;
using Chetch.Utilities;

namespace Chetch.Arduino2
{
    abstract public class ArduinoDevice : ArduinoObject
    {
        public const int REPORT_INTERVAL_NONE = -1;

        public enum ErrorCode
        {
            INVALID_COMMAND = 1,
        }

        public enum DeviceCategory
        {
            NOT_SET,
            DIAGNOSTICS = 1,
            IR_TRANSMITTER = 2,
            IR_RECEIVER = 3,
            TEMPERATURE_SENSOR = 4,
            COUNTER = 5,
            RANGE_FINDER = 6,
            ALARM = 7,
            VAC_SENSOR = 8,
            SWITCH = 9,
            SERVO = 10,
            MOTOR = 11,
            LCD = 12,
        }

        public enum DeviceState
        {
            CREATED = 1,
            INITIALISING,
            INITIALISED,
            CONFIGURING,
            CONFIGURED,
        }

        public enum AnalogPin
        {
            A0 = 0,
            A1,
            A2,
            A3,
            A4,
            A5,
            A6,
            A7,
            A8,
            A9,
        }

        public enum InterruptMode
        {
            NONE = 0,
            CHANGE = 1,
            FALLING = 2,
            RISING = 3,
        }

        public ArduinoDeviceManager ADM { get; set; }

        [ArduinoProperty(PropertyAttribute.IDENTIFIER)]
        override public String UID => ADM.ID + ":" + ID;

        [ArduinoProperty(PropertyAttribute.DESCRIPTOR)]
        public String Name { get; internal set; }

        override public String Description => String.Format("{0} {1}", UID, Name);

        [ArduinoProperty(PropertyAttribute.IDENTIFIER)]
        public byte BoardID { get; internal set; }

        [ArduinoProperty(PropertyAttribute.DESCRIPTOR)]
        public DeviceCategory Category { get; protected set; }

        [ArduinoProperty(ArduinoPropertyAttribute.STATE, DeviceState.CREATED)]
        public DeviceState State
        {
            get { return Get<DeviceState>(); }
            internal set { Set(value, value > DeviceState.CREATED); }
        }

        [ArduinoProperty(ArduinoPropertyAttribute.STATE | PropertyAttribute.SERIALIZABLE, true)]
        public bool Enabled
        {
            get { return Get<bool>(); }
            internal set { Set(value, IsReady); }
        }

        [ArduinoProperty(ArduinoPropertyAttribute.STATE, -1)]
        public Int16 ReportInterval
        {
            get { return Get<Int16>(); }
            set { Set(value, IsReady); }
        }

        public bool IsReady => State == DeviceState.CONFIGURED;

        [ArduinoProperty(ArduinoPropertyAttribute.METADATA, PropertyAttribute.DATETIME_DEFAULT_VALUE_MIN)]
        public DateTime LastStatusResponseOn
        {
            get { return Get<DateTime>(); }
            set { Set(value, IsReady, IsReady); }
        }

        [ArduinoProperty(ArduinoPropertyAttribute.METADATA, PropertyAttribute.DATETIME_DEFAULT_VALUE_MIN)]
        public DateTime LastPingResponseOn
        {
            get { return Get<DateTime>(); }
            set { Set(value, IsReady, IsReady); }
        }

        [ArduinoProperty(ArduinoPropertyAttribute.METADATA, PropertyAttribute.DATETIME_DEFAULT_VALUE_MIN)]
        public DateTime LastCommandResponseOn
        {
            get { return Get<DateTime>(); }
            set { Set(value, IsReady, IsReady); }
        }

        private Dictionary<String, ArduinoCommand> _commands = new Dictionary<String, ArduinoCommand>();

        public List<ArduinoCommand> Commands => _commands.Values.ToList();

        public ArduinoDevice(String id, String name)
        {
            ID = id;
            Name = name.ToUpper();

            AddCommand(ArduinoCommand.DeviceCommand.ENABLE, ArduinoCommand.ParameterType.BOOL);
            AddCommand(ArduinoCommand.DeviceCommand.DISABLE, ArduinoCommand.ParameterType.BOOL);
            AddCommand(ArduinoCommand.DeviceCommand.SET_REPORT_INTERVAL, ArduinoCommand.ParameterType.INT);

            State = DeviceState.CREATED;
        }

        protected ADMMessage CreateMessage(MessageType messageType)
        {
            var message = new ADMMessage();
            message.Type = messageType;
            message.Target = BoardID;
            message.Sender = BoardID;
            
            return message;
        }
        
        

        override protected int GetArgumentIndex(String fieldName, ADMMessage message)
        {
            switch (fieldName)
            {
                case "DeviceCommand":
                    return 0;

                case "Enabled":
                    return message.IsCommandRelated ? 1 : 0;

                case "ReportInterval":
                    return message.IsCommandRelated ? 1 : 1;

                case "ErrorCode":
                    return 0;

                case "ErrorSubCode":
                    return 1;

                case "ErrorFromMessage": //should give the message type that resulted in the error
                    return 2;

                default:
                    throw new ArgumentException(String.Format("unrecognised message field {0}", fieldName));
            }
        }

        virtual public void SendMessage(ADMMessage message)
        {
            ADM.SendMessage(message);
        }
        
        virtual protected void AddInit(ADMMessage message)
        {
            message.AddArgument(Name == null ? "N/A" : Name);
            message.AddArgument((byte)Category);
        }

        public void Initialise()
        {
            State = DeviceState.INITIALISING;
            var message = CreateMessage(MessageType.INITIALISE);
            AddInit(message);
            SendMessage(message);
        }

        virtual protected void OnInitialised(ADMMessage message)
        {
            //a hook
        }

        virtual protected void AddConfig(ADMMessage message)
        {
            message.AddArgument(Enabled);
            message.AddArgument(ReportInterval);
        }

        public void Configure()
        {
            State = DeviceState.CONFIGURING;
            var message = CreateMessage(MessageType.CONFIGURE);
            AddConfig(message);
            SendMessage(message);
        }
        
        virtual protected void OnConfigured(ADMMessage message)
        {
            //a hook
        }

        virtual public ADMRequestManager.ADMRequest RequestStatus(String requester = null)
        {
            var message = CreateMessage(MessageType.STATUS_REQUEST);
            ADMRequestManager.ADMRequest req = null;
            if(requester != null)
            {
                req = ADM.Requests.AddRequest(requester);
                message.Tag = req.Tag;
            }

            SendMessage(message);

            return req;
        }

        public ADMRequestManager.ADMRequest Ping(String requester = null)
        {
            var message = CreateMessage(MessageType.PING);
            ADMRequestManager.ADMRequest req = null;
            if (requester != null)
            {
                req = ADM.Requests.AddRequest(requester);
                message.Tag = req.Tag;
            }

            SendMessage(message);

            return req;
        }

        virtual protected void OnStatusResponse(ADMMessage message)
        {
            //a hook
        }

        override public void HandleMessage(ADMMessage message)
        {   
            switch (message.Type)
            {
                case Messaging.MessageType.ERROR:
                    //TODO: run this through GetArgumentIndex and then use GetMessageValue ... include subcode as well
                    String error;
                    String info;
                    try
                    {
                        ArduinoDeviceManager.ErrorCode errorCode = (ArduinoDeviceManager.ErrorCode)GetMessageValue<int>("ErrorCode", message);
                        ErrorCode errorSubCode = (ErrorCode)GetMessageValue<int>("ErrorSubCode", message);
                        error = String.Format("{0}-{1}", errorCode, errorSubCode);
                        if (message.Arguments.Count - 1 >= GetArgumentIndex("ErrorFromMessage", message))
                        {
                            info = "Originating message of type " + GetMessageValue<MessageType>("ErrorFromMessage", message);
                        }
                        else
                        {
                            info = "Originating message not supplied";
                        }
                    } 
                    catch (Exception e)
                    {
                        error = "Unknown error";
                        info = e.Message;
                    }
                    SetError(error, info); //This will trigger a property change event that can be listened to
                    break;

                case MessageType.INITIALISE_RESPONSE:
                    State = DeviceState.INITIALISED;
                    OnInitialised(message);
                    Configure();
                    break;

                case MessageType.CONFIGURE_RESPONSE:
                    State = DeviceState.CONFIGURED;
                    OnConfigured(message);
                    break;

                case MessageType.COMMAND_RESPONSE:
                    ArduinoCommand.DeviceCommand deviceCommand = GetMessageValue<ArduinoCommand.DeviceCommand>("DeviceCommand", message);
                    if (HandleCommandResponse(deviceCommand, message))
                    {
                        LastCommandResponseOn = DateTime.Now;
                    }
                    break;

                case MessageType.STATUS_RESPONSE:
                    AssignMessageValues(message, "Enabled", "ReportInterval");
                    if(ADM.AttachMode == ArduinoDeviceManager.AttachmentMode.OBSERVER_OBSERVED && State != DeviceState.CONFIGURED)
                    {
                        State = DeviceState.CONFIGURED;
                    }
                    OnStatusResponse(message);
                    LastStatusResponseOn = DateTime.Now;
                    break;

                case MessageType.PING_RESPONSE:
                    LastPingResponseOn = DateTime.Now;
                    break;
            }

            base.HandleMessage(message);
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

        public ArduinoCommand AddCommand(ArduinoCommand.DeviceCommand deviceCommand, String alias,  params ArduinoCommand.ParameterType[] parameterTypes)
        {
            ArduinoCommand cmd = new ArduinoCommand(deviceCommand, alias, parameterTypes?.ToList());
            return AddCommand(cmd);

        }

        public ArduinoCommand AddCommand(ArduinoCommand.DeviceCommand deviceCommand, params ArduinoCommand.ParameterType[] parameterTypes)
        {
            return AddCommand(deviceCommand, deviceCommand.ToString(), parameterTypes);
        }

        protected ArduinoCommand GetCommand(String alias)
        {
            alias = alias.Trim().ToLower().Replace('_','-');
            return _commands.ContainsKey(alias) ? _commands[alias] : null;
        }

        protected ArduinoCommand GetCommand(ArduinoCommand.DeviceCommand command)
        {
            return GetCommand(command.ToString());
        }

        private Task _executeCommandTask = null;
        public ADMRequestManager.ADMRequest ExecuteCommand(String commandAlias, List<Object> parameters = null)
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

            int ttl = System.Math.Max(ADMRequestManager.DEFAULT_TTL, cmd.TotalDelayInterval + 1000);
            ADMRequestManager.ADMRequest req = ADM.Requests.AddRequest(ttl);
            Action action = () =>
            {
                try
                {
                    if (!req.Proceed) 
                    {
                        DateTime started = DateTime.Now;
                        while (!req.Proceed || ((DateTime.Now.Ticks - started.Ticks) / TimeSpan.TicksPerMillisecond) < 100) ;
                    }
                    if(req.Owner == null)
                    {
                        ADM.Requests.Release(req);
                        req = null;
                    }
                    ExecuteCommand(cmd, req, parameters);
                } catch  (Exception e)
                {
                    Error = e.Message;
                }
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
            return req;
        }

        public ADMRequestManager.ADMRequest ExecuteCommand(String commandAlias, params Object[] parameters)
        {
            return ExecuteCommand(commandAlias, parameters.ToList());
        }

        public ADMRequestManager.ADMRequest ExecuteCommand(ArduinoCommand.DeviceCommand deviceCommand, params Object[] parameters)
        {
            return ExecuteCommand(deviceCommand.ToString().ToLower(), parameters.ToList());
        }

        virtual protected void ExecuteCommand(ArduinoCommand cmd, ADMRequestManager.ADMRequest request, List<Object> parameters = null)
        {
            for (int i = 0; i < cmd.Repeat; i++)
            {
                if (cmd.IsCompound)
                {
                    //TODO: how to handle parameters in the case of a compound command???
                    foreach (var c in cmd.Commands)
                    {
                        ExecuteCommand(c, request, parameters);
                    }
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

                        List<Object> allParams = new List<Object>();
                        if (cmd.Parameters != null) allParams.AddRange(cmd.Parameters);
                        if (parameters != null) allParams.AddRange(parameters);
                        try
                        {
                            cmd.ValidateParameters(allParams);
                            if (HandleCommand(cmd, allParams))
                            {
                                //assume the command is a message
                                var cm = CreateMessage(MessageType.COMMAND);
                                byte tag = 0;
                                if (request != null)
                                {
                                    if (!request.Proceed)
                                    {
                                        tag = request.Tag;
                                        request.Proceed = true;
                                    }
                                    else
                                    {
                                        var req = ADM.Requests.AddRequest();
                                        tag = req.Tag;
                                        req.Owner = request.Owner;
                                    }
                                }
                                cm.Tag = tag;
                                cm.AddArgument((byte)cmd.Command);
                                foreach (var p in allParams)
                                {
                                    cm.AddArgument(Chetch.Utilities.Convert.ToBytes(p));
                                }
                                //Console.WriteLine("Sending command {0} to {1}", cmd.Alias, UID);
                                SendMessage(cm);
                            }
                        } catch (Exception e)
                        {
                            ADM?.Tracing.TraceEvent(System.Diagnostics.TraceEventType.Error, 1000, e.Message);
                        }
                    } //if delay
                } //if compound
            } //end repeat
        }

        //Called before sending command (for instance to update state) return value determines whether command is executed.
        virtual protected bool HandleCommand(ArduinoCommand cmd, List<Object> parameters)
        {
            switch (cmd.Command)
            {
                case ArduinoCommand.DeviceCommand.ENABLE:
                    if (parameters.Count == 0) parameters.Add(true);
                    Enabled = (bool)parameters[0];
                    break;

                case ArduinoCommand.DeviceCommand.DISABLE:
                    Enabled = false;
                    break;

                case ArduinoCommand.DeviceCommand.SET_REPORT_INTERVAL:
                    ReportInterval = (Int16)parameters[0];
                    break;
            }
            return true;
        }

        //Called when a command response is received
        virtual protected bool HandleCommandResponse(ArduinoCommand.DeviceCommand deviceCommand, ADMMessage message)
        {
            switch (deviceCommand)
            {
                case ArduinoCommand.DeviceCommand.ENABLE:
                case ArduinoCommand.DeviceCommand.DISABLE:
                    AssignMessageValues(message, "Enabled");
                    break;

                case ArduinoCommand.DeviceCommand.SET_REPORT_INTERVAL:
                    AssignMessageValues(message, "ReportInterval");
                    break;
            }
            return true;
        }

        public ADMRequestManager.ADMRequest Enable(bool enabled = true)
        {
            return ExecuteCommand(ArduinoCommand.DeviceCommand.ENABLE, enabled);
        }

        public ADMRequestManager.ADMRequest Disable()
        {
            return Enable(false);
        }

        public ADMRequestManager.ADMRequest SetReportInterval(int interval)
        {
            return ExecuteCommand(ArduinoCommand.DeviceCommand.SET_REPORT_INTERVAL, interval);
        }
    }

    public class TestDevice01 : ArduinoDevice
    {
        List<int> valueHistory = new List<int>();
        List<int> timestampHistory = new List<int>();

        [ArduinoProperty(ArduinoPropertyAttribute.DATA, 0)]
        public int TestValue 
        {
            get { return Get<int>(); }
            internal set { Set(value, IsReady); }
        }

        public int PrevTestValue { get; internal set; } = -1;

        
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

        override protected int GetArgumentIndex(String fieldName, ADMMessage message)
        {
            switch (fieldName)
            {
                case "TestValue":
                    return 0;

                default:
                    return base.GetArgumentIndex(fieldName, message);
            }
        }
        
        override public void HandleMessage(ADMMessage message)
        {
            switch (message.Type)
            {
                case MessageType.DATA:
                    PrevTestValue = TestValue;
                    //valueHistory.Add(message.GetArgument<int>(0));
                    //timestampHistory.Add(message.GetArgument<int>(1));
                    AssignMessageValues(message, "TestValue");
                    break;
            }

            base.HandleMessage(message);
        }
    }
}
