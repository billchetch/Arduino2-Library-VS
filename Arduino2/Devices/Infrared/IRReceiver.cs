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
        private bool _receiving = false;
        private String _commandName;
        private int _receivePin;
        private Dictionary<String, IRCode> _irCodes = new Dictionary<String, IRCode>();
        private Dictionary<long, IRCode> _unknownCodes = new Dictionary<long, IRCode>();
        private List<long> _ignoreCodes = new List<long>(); //codes we ignore


        public String IRCommandName
        {
            get { return _commandName;  }
        }

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

            ArduinoCommand cmd = DB.GetCommand(DeviceName, REPEAT_COMMAND);
            if (cmd != null)
            {
                long code = System.Convert.ToInt64(cmd.Parameters[0]);
                if(!_ignoreCodes.Contains(code))_ignoreCodes.Add(code);
            }
        }

        public void StartRecording()
        {
            ExecuteCommand(ArduinoCommand.DeviceCommand.START);
        }

        public void StopRecording()
        {
            ExecuteCommand(ArduinoCommand.DeviceCommand.STOP);
        }

        public void SaveRecording()
        {
            ExecuteCommand(ArduinoCommand.DeviceCommand.SAVE);
        }

        protected override void ExecuteCommand(ArduinoCommand cmd, ADMRequestManager.ADMRequest request, List<object> parameters = null)
        {
            switch (cmd.Command)
            {
                case ArduinoCommand.DeviceCommand.START:
                    if(_commandName == null)
                    {
                        throw new Exception("Cannot start recording without a command name");
                    }
                    _irCodes.Clear();
                    _unknownCodes.Clear();
                    _receiving = true;
                    break;

                case ArduinoCommand.DeviceCommand.STOP:
                    _receiving = false;
                    break;

                case ArduinoCommand.DeviceCommand.SAVE:
                    StopRecording();
                    _receiving = false;
                    WriteIRCodes();
                    return;
            }
            base.ExecuteCommand(cmd, request, parameters);
        }

        virtual public void processCode(long code, int protocol, int bits = 32)
        {
            processCode(_commandName, code, protocol, bits);
        }

        virtual public void processCode(String commandName, long code, int protocol, int bits)
        {
            if (commandName == null || commandName.Length == 0 || _ignoreCodes.Contains(code) || protocol == (int)IRProtocol.UNKNOWN) return;

            IRCode irc = new IRCode();
            if (_irCodes.ContainsKey(commandName))
            {
                //if there is already an ir code for this command then check if the actual code is different
                //from the original then store as an 'unkonwn' code for later inspection
                if(_irCodes[commandName].Code != code)
                {
                    irc.Code = code;
                    irc.Protocol = protocol;
                    irc.Bits = bits;
                    _unknownCodes[code] = irc;
                }
            } else
            {
                irc.Code = code;
                irc.Protocol = protocol;
                irc.Bits = bits;
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
                case "Code":
                    return 0;
                case "Protocol":
                    return 1;
                case "Bits":
                    return 2;

                default:
                    return base.GetArgumentIndex(fieldName, message);
            }
        }

        public override void HandleMessage(ADMMessage message)
        {
            switch (message.Type)
            {
                case Messaging.MessageType.DATA:
                    if(_receiving)
                    {
                        long ircode = GetMessageValue<long>("Code", message); 
                        int protocol = GetMessageValue<int>("Protocol", message);
                        int bits = GetMessageValue<int>("Bits", message);

                        processCode(ircode, protocol, bits);
                    }
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
                    DB.InsertCommand(DBID, caid, irc.Code, irc.Protocol, irc.Bits);
                } catch (Exception e)
                {
                    //can happen if ir code is a duplicate
                    //Console.WriteLine(e.Message);
                    var row = DB.SelectCommand(DeviceName, kv.Key);
                    if (row == null) throw e;
                    long cmdid = row.ID;
                    if (cmdid == 0) throw new Exception("No ir command code found in database");
                    DB.UpdateCommand(cmdid, DBID, caid, irc.Code, irc.Protocol, irc.Bits);
                }
            }
        }
    }
}
