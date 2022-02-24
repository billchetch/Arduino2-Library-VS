using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Utilities;

namespace Chetch.Arduino2.Devices.Infrared
{
    // <summary>
    /// Arduino board Works with KY-022 IR receiver
    /// </summary>
    /// 
    public abstract class IRReceiver : IRDevice
    {
        private bool _recording = false;
        private int _receivePin;
        private Dictionary<String, IRCode> _irCodes = new Dictionary<String, IRCode>();
        private Dictionary<long, IRCode> _unknownCodes = new Dictionary<long, IRCode>();
        private List<long> _ignoreCodes = new List<long>(); //codes we ignore

        public bool IsRecording => _recording;

        public event EventHandler<IRCode> ReceivedIRCode;

        public IRCode LastIRCodeReceived
        {
            get { return Get<IRCode>(); }
            internal set 
            {
                ReceivedIRCode?.Invoke(this, value);
                Set(value, true, IsReady); 
            }
        }

        public String IRCommandName { get; set; }

        public Dictionary<String, IRCode> IRCodes
        {
            get { return _irCodes;  }
        }
        public Dictionary<long, IRCode> UnknownIRCodes
        {
            get { return _unknownCodes; }
        }

        public IRReceiver(String id, String name, int receivePin, IRDB db = null) : base(id, name, db)
        {
            Category = DeviceCategory.IR_RECEIVER;

            _receivePin = receivePin;
            
            ArduinoCommand cmd = new ArduinoCommand(ArduinoCommand.DeviceCommand.START);
            AddCommand(cmd);

            cmd = new ArduinoCommand(ArduinoCommand.DeviceCommand.STOP); 
            AddCommand(cmd);

            cmd = new ArduinoCommand(ArduinoCommand.DeviceCommand.SAVE);
            AddCommand(cmd);

        }

        public IRReceiver(int receivePin, IRDB db = null) : this("irr" + receivePin, "IRR", receivePin, db) { }

        protected override void AddConfig(ADMMessage message)
        {
            base.AddConfig(message);

            message.AddArgument(_receivePin);
        }

        override public void ReadDevice()
        {
            base.ReadDevice();

        }

        public ADMRequestManager.ADMRequest StartRecording()
        {
            return ExecuteCommand(ArduinoCommand.DeviceCommand.START);
        }

        public ADMRequestManager.ADMRequest StopRecording()
        {
            return ExecuteCommand(ArduinoCommand.DeviceCommand.STOP);
        }

        public ADMRequestManager.ADMRequest SaveRecording()
        {
            return ExecuteCommand (ArduinoCommand.DeviceCommand.SAVE);
        }

        public ADMRequestManager.ADMRequest Resume()
        {
            return ExecuteCommand(ArduinoCommand.DeviceCommand.RESUME);
        }

        protected override bool HandleCommand(ArduinoCommand cmd, List<object> parameters = null)
        {
            switch (cmd.Command)
            {
                case ArduinoCommand.DeviceCommand.START:
                    if(IRCommandName == null)
                    {
                        throw new Exception("Cannot start recording without a command name");
                    }
                    _irCodes.Clear();
                    _unknownCodes.Clear();
                    _recording = true;
                    break;

                case ArduinoCommand.DeviceCommand.STOP:
                    _recording = false;
                    break;

                case ArduinoCommand.DeviceCommand.SAVE:
                    StopRecording();
                    _recording = false;
                    WriteIRCodes();
                    return false;
            }
            return base.HandleCommand(cmd, parameters);
        }

        private void processCode(IRProtocol protocol, UInt16 address, UInt16 command)
        {
            processCode(IRCommandName, protocol, address, command);
        }

        private void processCode(String commandName, IRProtocol protocol, UInt16 address, UInt16 command)
        {
            if (commandName == null || commandName.Length == 0 || protocol == IRProtocol.UNKNOWN) return;

            IRCode irc = new IRCode();
            if (!_irCodes.ContainsKey(commandName))
            {
                irc.Protocol = protocol;
                irc.Address = address;
                irc.Command = command;
                _irCodes[commandName] = irc;
            }
        }

        virtual public void processUnknownCode(String commandName, IRCode irc)
        {
            if (commandName == null || commandName.Length == 0) return;

            if (!_irCodes.ContainsKey(commandName))
            {
                _irCodes[commandName] = irc;
            } else
            {
                throw new Exception(commandName + " is not unknown");
            }
        }

        public void processUnknownCode(String commandName)
        {
            if(_unknownCodes.Count != 1)
            {
                throw new Exception(String.Format("Cannot process unknown code because there are {0} unknown codes", _unknownCodes.Count));
            }

            processUnknownCode(commandName, _unknownCodes.Values.First());
        }


        protected override int GetArgumentIndex(string fieldName, ADMMessage message)
        {
            switch (fieldName)
            {
                case "Protocol":
                    return 0;
                case "Address":
                    return 1;
                case "Command":
                    return 2;
                default:
                    return base.GetArgumentIndex(fieldName, message);
            }
        }

        public override void HandleMessage(ADMMessage message)
        {
            IRCode irc;
            switch (message.Type)
            {
                case Messaging.MessageType.DATA:
                    irc = new IRCode();
                    irc.Protocol = (IRProtocol)GetMessageValue<Int16>("Protocol", message);
                    irc.Address = GetMessageValue<UInt16>("Address", message);
                    irc.Command = GetMessageValue<UInt16>("Command", message);
                    

                    if (_recording)
                    {
                        processCode(irc.Protocol, irc.Address, irc.Command);
                    }
                    LastIRCodeReceived = irc;
                    break;
            }

            base.HandleMessage(message);
        }

        public void WriteIRCodes()
        {
            if (DB == null) throw new Exception("No database available");
            WriteDevice();
            if (DBID == 0) throw new Exception("No database ID value for device");

            var commandAliases = Chetch.Database.IDMap<String>.Create(DB.SelectCommandAliases(), "command_alias");
            foreach (var kv in _irCodes)
            {
                IRCode irc = kv.Value;

                long caid;
                if (!commandAliases.ContainsKey(kv.Key))
                {
                    caid = DB.InsertCommandAlias(kv.Key);
                }
                else
                {
                    caid = commandAliases[kv.Key].ID;
                }

                try { 
                    DB.InsertCommand(DBID, caid, (int)irc.Protocol, irc.Address, irc.Command);
                } catch (Exception e)
                {
                    //can happen if ir code is a duplicate
                    //Console.WriteLine(e.Message);
                    var row = DB.SelectCommand(DeviceName, kv.Key);
                    if (row == null) throw e;
                    long cmdid = row.ID;
                    if (cmdid == 0) throw new Exception("No ir command code found in database");
                    DB.UpdateCommand(cmdid, DBID, caid, (int)irc.Protocol, irc.Address, irc.Command);
                }
            }
        }
    }
}
