using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using System.Diagnostics;
using System.ComponentModel;
using Chetch.Messaging;
using Chetch.Services;
using Chetch.Database;
using Chetch.Utilities;

namespace Chetch.Arduino2
{
    [System.ComponentModel.DesignerCategory("Code")]
    abstract public class ADMService : TCPMessagingClient
    {
        public class MessageSchema : Chetch.Messaging.MessageSchema
        {
            public const String ADM_FIELD_NAME_PREFIX = "ADM";
            public const String DEVICE_FIELD_NAME_PREFIX = "Device";
            public const String DEVICE_GROUP_FIELD_NAME_PREFIX = "DeviceGroup";

            public const String COMMAND_STATUS = "status";
            public const String COMMAND_PING = "ping";
            public const String COMMAND_LIST_DEVICES = "list-devices";
            public const String COMMAND_LIST_GROUPS = "list-groups";
            public const String COMMAND_LIST_COMMANDS = "list-commands";
            public const String COMMAND_WAIT = "wait";
            public const String COMMAND_ENABLE = "enable";
            public const String COMMAND_DISABLE = "disable";
            public const String COMMAND_REPORT_INTERVAL = "report-interval";

            public const int AO_ATTRIBUTE_FLAGS = ArduinoObject.ArduinoPropertyAttribute.IDENTIFIER | ArduinoObject.ArduinoPropertyAttribute.DESCRIPTOR | ArduinoObject.ArduinoPropertyAttribute.STATE | ArduinoObject.ArduinoPropertyAttribute.DATA | DataSourceObject.PropertyAttribute.ERROR;
            public MessageSchema() { }

            public MessageSchema(Message message) : base(message) { }

            public MessageSchema(MessageType messageType) : base(messageType) { }

            public void AddADMs(Dictionary<String, ArduinoDeviceManager> adms)
            {
                if (adms != null && adms.Count > 0)
                {
                    foreach (ArduinoDeviceManager adm in adms.Values)
                    {
                        Dictionary<String, Object> vals = new Dictionary<String, Object>();
                        var properties = adm.GetProperties(AO_ATTRIBUTE_FLAGS);
                        foreach (var p in properties)
                        {
                            vals[p.Name] = p.GetValue(adm);
                        }
                        Message.AddValue("ADM:" + adm.ID, vals);
                    }
                }
                else
                {
                    Message.AddValue("ADMS", "No boards connected");
                }
            }

            protected void AddValues(Dictionary<String, Object> vals, String prefix = null)
            {
                foreach(KeyValuePair<String, Object> kvp in vals){
                    String key = prefix == null ? kvp.Key : prefix + ":" + kvp.Key;
                    Message.AddValue(key, kvp.Value);
                }
            }

            protected void AddArduinoObject(String prefix, ArduinoObject ao, bool changedPropertiesOnly = false)
            {
                List<System.Reflection.PropertyInfo> properties = ao.GetProperties(AO_ATTRIBUTE_FLAGS);
                Dictionary<String, Object> vals = new Dictionary<string, Object>();
                foreach (var p in properties)
                {
                    if (changedPropertiesOnly && !ao.ChangedProperties.Contains(p.Name)) continue;
                    vals[p.Name] = p.GetValue(ao);
                }

                AddValues(vals, prefix);
            }

            public void AddADM(ArduinoDeviceManager adm)
            {
                AddArduinoObject(ADM_FIELD_NAME_PREFIX, adm);
            }

            public void AddDevice(ArduinoDevice device, bool changedPropertiesOnly = false)
            {
                AddArduinoObject(DEVICE_FIELD_NAME_PREFIX, device, changedPropertiesOnly);
            }

            public void AddDeviceGroup(ArduinoDeviceGroup deviceGroup, bool changedPropertiesOnly = false)
            {
                AddArduinoObject(DEVICE_GROUP_FIELD_NAME_PREFIX, deviceGroup, changedPropertiesOnly);
            }
        }

        public class Request
        {
            public const int DEFAULT_TTL = 30000; //milliseconds before expired

            public String RequestID { get; internal set; }

            public String Requester  { get; internal set; }

            public DateTime Created { get; internal set; }

            private int _ttl = -1;

            public Request(String requestID, String requester, int ttl = DEFAULT_TTL)
            {
                RequestID = requestID;
                Requester = requester;
                _ttl = ttl;
            }

            virtual public bool HasExpired => _ttl <= 0 ? false : (DateTime.Now.Ticks - Created.Ticks / TimeSpan.TicksPerSecond) > _ttl;
        }

        public class ADMRequest : Request
        {
            public static String CreateRequestID(ArduinoDeviceManager adm, byte tag)
            {
                return adm.UID + "-" + tag;
            }

            private ArduinoDeviceManager _adm;
            private byte _tag;

            public ADMRequest(ArduinoDeviceManager adm, byte tag, String requester) : base(CreateRequestID(adm, tag), requester, -1)
            {
                _adm = adm;
                _tag = tag;
            }

            public override bool HasExpired => _adm.Requests.IsAvailable(_tag);
        }

        protected const int BEGIN_ADMS_TIMER_INTERVAL = 2 * 60 * 1000;
        protected const int BEGIN_TIMEOUT = 8000;
        protected const int MAX_BEGIN_ATTEMPTS = 3;
        protected const int DEFAULT_LOG_SNAPSHOPT_TIMER_INTERVAL = 30 * 1000;
        
        protected ADMServiceDB ServiceDB { get; set; }

        protected bool ServiceIsStopping { get; private set; } = false;

        private System.Timers.Timer _beginADMsTimer;
        private Dictionary<String, ArduinoDeviceManager> _adms  = new Dictionary<String, ArduinoDeviceManager>();
        
        private System.Timers.Timer _logSnapshotTimer;
        protected int LogSnapshotTimerInterval { get; set; } = DEFAULT_LOG_SNAPSHOPT_TIMER_INTERVAL;

        private Dictionary<ArduinoObject, Message> _messagesToDispatch = new Dictionary<ArduinoObject, Message>();
        private Object _dispatchMessageLock = new object();

        private Dictionary<String, Request> _requests = new Dictionary<String, Request>();

        public ADMService(String clientName, String clientManagerSource, String serviceSource, String eventLog) : base(clientName, clientManagerSource, serviceSource, eventLog)
        {
            //empty 
        }

        public override void AddCommandHelp()
        {
            base.AddCommandHelp();

            //general commands related to a service or hardware
            AddCommandHelp(MessageSchema.COMMAND_STATUS, "Get status info about this service and the ADMs");
            
            //adm specific commands related to a board and device
            AddCommandHelp("adm/<board>:" + MessageSchema.COMMAND_STATUS, "Request board status and add additional information");
            AddCommandHelp("adm/<board>:" + MessageSchema.COMMAND_PING, "Ping board");
            AddCommandHelp("adm/<board>:" + MessageSchema.COMMAND_LIST_DEVICES, "List devices added to ADM");
            AddCommandHelp("adm/<board>:" + MessageSchema.COMMAND_LIST_GROUPS, "List device groups added to ADM");
            AddCommandHelp("adm/<board>:<device/group>:" + MessageSchema.COMMAND_WAIT, "Pause for a short while, useful if interspersed with other, comma-seperated, commands");
            AddCommandHelp("adm/<board>:<device/group>:" + MessageSchema.COMMAND_LIST_COMMANDS, "List device commands");
            AddCommandHelp("adm/<board>:<device/group>:" + MessageSchema.COMMAND_STATUS, "Status of device(s)");
            AddCommandHelp("adm/<board>:<device/group>:" + MessageSchema.COMMAND_ENABLE, "Enable device(s)");
            AddCommandHelp("adm/<board>:<device/group>:" + MessageSchema.COMMAND_DISABLE, "Disable device(s)");
            AddCommandHelp("adm/<board>:<device/group>:" + MessageSchema.COMMAND_REPORT_INTERVAL, "Set device report interval");
        }

        protected List<ArduinoObject> GetArduinoObjects()
        {
            List<ArduinoObject> aos = new List<ArduinoObject>();
            foreach (var adm in _adms.Values)
            {
                aos.Add(adm);
                aos.AddRange(adm.GetDevices());
                aos.AddRange(adm.GetDeviceGroups());
            }
            return aos;
        }

        protected ArduinoDeviceManager GetADMFromArduinoObject(ArduinoObject ao)
        {
            if(ao is ArduinoDeviceManager)
            {
                return (ArduinoDeviceManager)ao;
            }
            if(ao is ArduinoDevice)
            {
                return ((ArduinoDevice)ao).ADM;
            }
            if (ao is ArduinoDeviceGroup)
            {
                return ((ArduinoDeviceGroup)ao).ADM;
            }
            return null;
        }

        protected override void OnStart(string[] args)
        {
            if (ServiceDB != null)
            {
                List<ArduinoObject> aoToInitialise = GetArduinoObjects();
                foreach (var ao in aoToInitialise)
                {
                    //Add handler so we can respond to property changes
                    ao.PropertyChanged += HandleADMPropertyChange;

                    //deserialize if there is 
                    SysInfo si = ServiceDB.GetSysInfo(ao.UID);
                    if (si != null)
                    {
                        ao.Deserialize(si.DataValue);
                    }
                }

                if(_logSnapshotTimer == null)
                {
                    _logSnapshotTimer = new System.Timers.Timer(LogSnapshotTimerInterval);
                    _logSnapshotTimer.Elapsed += OnLogSnapshotTimer;
                }
                _logSnapshotTimer.Start();
            }

            base.OnStart(args);
        }

        protected override void OnStop()
        {
            ServiceIsStopping = true;
            if(_beginADMsTimer != null)
            {
                _beginADMsTimer.Stop();
            }

            if (_logSnapshotTimer != null)
            {
                _logSnapshotTimer.Stop();
            }

            lock (_dispatchMessageLock)
            {
                _messagesToDispatch.Clear();
            }

            foreach (var adm in _adms.Values)
            {
                adm.End();
            }

            if(ServiceDB != null)
            {
                List<ArduinoObject> aoToSerialize = GetArduinoObjects();
                
                foreach(var ao in aoToSerialize)
                {
                    //Remove property change handler
                    ao.PropertyChanged -= HandleADMPropertyChange;

                    //serialize if required
                    try
                    {
                        Dictionary<String, Object> vals = new Dictionary<String, Object>();
                        ao.Serialize(vals);
                        if (vals.Count > 0)
                        {
                            SysInfo si = new SysInfo(ao.UID, vals);
                            ServiceDB.SaveSysInfo(si);
                        }
                    }
                    catch (Exception e)
                    {
                        Tracing?.TraceEvent(TraceEventType.Error, 0, "Error serializing {0} to DB: {1}", ao.UID, e.Message);
                    }
                }
            }

            base.OnStop();
        }

        virtual protected bool CanLogToSnapshot(ArduinoObject ao, String propertyName)
        {
            return true;
        }

        virtual protected void OnLogSnapshotTimer(Object sender, EventArgs earg)
        {
            foreach (var adm in _adms.Values)
            {
                if (!adm.IsReady) continue;

                List<ArduinoObject> aoToSnapshot = GetArduinoObjects();
                foreach (var ao in aoToSnapshot)
                {
                    /*var properties = ao.GetPropertyNames(ArduinoObject.ArduinoPropertyAttribute.DATA);
                    foreach(var p in properties)
                    {
                        if(CanLogToSnapshot(ao, p))
                        {
                            ServiceDB.LogSnapshot(ao.UID, p, ao.Get<Object>(p));
                        }
                    }*/
                }
            }
        }

        virtual protected bool CanLogEvent(ArduinoObject ao, String eventName)
        {
            return true;
        }

        virtual protected bool CanDispatch(ArduinoObject ao, String propertyName)
        {
            return true;
        }

        //loggging events
        protected void HandleADMPropertyChange(Object sender, PropertyChangedEventArgs eargs)
        {
            //Get Event data
            DSOPropertyChangedEventArgs dsoArgs = (DSOPropertyChangedEventArgs)eargs;
            ArduinoObject ao = ((ArduinoObject)sender);
            ArduinoObject.ArduinoPropertyAttribute propertyAttribute = (ArduinoObject.ArduinoPropertyAttribute)ao.GetPropertyAttribute(dsoArgs.PropertyName);
            
            //First we deal with a state change (i.e. an Event)
            if ((propertyAttribute.IsState || propertyAttribute.IsError) && CanLogEvent(ao, dsoArgs.PropertyName))
            {
                String eventName = dsoArgs.PropertyName;
                String eventInfo;
                if (propertyAttribute.IsError)
                {
                    eventInfo = ao.ErrorInfo; 
                } else
                {
                    eventInfo = String.Format("{0} changed from {1} to {2}", eventName, dsoArgs.OldValue, dsoArgs.NewValue);
                }
                Object eventData = dsoArgs.NewValue;
                String eventSource = ao.UID;

                //log the event to the db
                try
                {
                    ServiceDB.LogEvent(eventName, eventSource, eventData, eventInfo);
                }
                catch (Exception e)
                {
                    Tracing?.TraceEvent(TraceEventType.Error, 0, e.Message);
                }
            }

            //now we dispatch a message to any subscribers to the service
            if ((propertyAttribute.IsState || propertyAttribute.IsData || propertyAttribute.IsMetaData || propertyAttribute.IsError) && CanDispatch(ao, dsoArgs.PropertyName))
            {
                var message = new Message(propertyAttribute.IsError ? MessageType.ERROR : MessageType.DATA);
                var schema = new MessageSchema(message);
                if (sender is ArduinoDevice)
                {
                    schema.AddDevice((ArduinoDevice)sender);
                }
                else if (sender is ArduinoDeviceGroup)
                {
                    schema.AddDeviceGroup((ArduinoDeviceGroup)sender);
                }
                else if (sender is ArduinoDeviceManager)
                {
                    schema.AddADM((ArduinoDeviceManager)sender);
                }

                DispatchMessage(ao, message);
            }
        }

        protected void AddRequest(Request request)
        {
            _requests[request.RequestID] = request;
        }

        protected void AddRequest(String requestID, Message request, int ttl = Request.DEFAULT_TTL)
        {
            AddRequest(new Request(requestID, request.Sender, ttl));
        }

        protected Request GetRequest(String requestID, bool removeIfExpired = true)
        {
            if (_requests.ContainsKey(requestID))
            {
                var req = _requests[requestID];
                if (removeIfExpired && req.HasExpired)
                {
                    _requests.Remove(requestID);
                }
                return req;
            } else
            {
                return null;
            }
        }

        protected void PurgeExpiredRequests()
        {
            List<String> toPurge = new List<String>();
            foreach(var r in _requests.Values)
            {
                if (r.HasExpired) toPurge.Add(r.RequestID);
            }
            foreach(var rid in toPurge)
            {
                _requests.Remove(rid);
            }
        }

        protected void AddRequest(ArduinoDeviceManager adm, byte tag, Message request)
        {
            AddRequest(new ADMRequest(adm, tag, request.Sender));
        }

        protected ADMRequest GetRequest(ArduinoDeviceManager adm, byte tag)
        {
            return (ADMRequest)GetRequest(ADMRequest.CreateRequestID(adm, tag));
        }

        protected void DispatchMessage(ArduinoObject ao, Message message)
        {
            lock (_dispatchMessageLock)
            {
                _messagesToDispatch[ao] = message;
            }

            Task.Run(() =>
            {
                Thread.Sleep(1);
                lock (_dispatchMessageLock)
                {
                    foreach(var kv in _messagesToDispatch)
                    {
                        byte tag = kv.Key.LastMessagedHandled == null ? (byte)0 : kv.Key.LastMessagedHandled.Tag;
                        ADMRequest req = null;
                        if (tag > 0) 
                        {
                            var adm = GetADMFromArduinoObject(kv.Key);
                            req = GetRequest(adm, tag);
                        }
                        if (req == null || req.HasExpired)
                        {
                            Broadcast(kv.Value);
                        } else
                        {
                            kv.Value.Target = req.Requester;
                            SendMessage(kv.Value);
                        }
                    }

                    _messagesToDispatch.Clear();
                }
            });
        }

        public override void HandleClientError(Connection cnn, Exception e)
        {
            throw new NotImplementedException();
        }

        protected override void OnClientConnect(ClientConnection cnn)
        {
            base.OnClientConnect(cnn);

            if(_adms.Count == 0)
            {
                Tracing?.TraceEvent(TraceEventType.Error, 0, "There are no ADMs in this service {0}!", ServiceName);
                return;
            }

            if(_beginADMsTimer == null)
            {
                _beginADMsTimer = new System.Timers.Timer();
                _beginADMsTimer.Elapsed += OnBeginADMsTimer;
                _beginADMsTimer.Interval = BEGIN_ADMS_TIMER_INTERVAL;
                Tracing?.TraceEvent(TraceEventType.Information, 0, "Client {0} has connected so starting up {1} ADMs ...", cnn.Name, _adms.Count);
                OnBeginADMsTimer(null, null);

            }
        }

        private void OnBeginADMsTimer(Object sender, EventArgs earg)
        {
            _beginADMsTimer.Stop();

            foreach (var adm in _adms.Values)
            {
                if (adm.State >= ArduinoDeviceManager.ADMState.BEGUN) continue;

                bool admReadyToUse = false;
                int beginAttempts = 1;
                do
                {
                    if (ServiceIsStopping) return;

                    try
                    {
                        Tracing?.TraceEvent(TraceEventType.Information, 0, "ADM {0} is starting up (attempt {1})...", adm.ID, beginAttempts);
                        adm.Begin(BEGIN_TIMEOUT);
                        admReadyToUse = true;
                        Tracing?.TraceEvent(TraceEventType.Information, 0, "ADM {0} is ready for use", adm.ID);
                    }
                    catch (Exception e)
                    {
                        Tracing?.TraceEvent(TraceEventType.Error, 0, "Exception: ADM {0} {1}", adm.ID, e.Message);
                        admReadyToUse = false;
                        beginAttempts++;
                        if(beginAttempts >= MAX_BEGIN_ATTEMPTS)
                        {
                            Tracing?.TraceEvent(TraceEventType.Error, 0, "ADM {0} failed to start after {1} attempts so abandoning for now", adm.ID, beginAttempts);
                            break;
                        }
                    }
                } while (!admReadyToUse);
            }
            if (!ServiceIsStopping)
            {
                _beginADMsTimer.Start();
            }
        }

        protected ArduinoDeviceManager AddADM(ArduinoDeviceManager adm)
        {
            if (adm == null) throw new ArgumentNullException("ADM cannot be null");
            if(String.IsNullOrEmpty(adm.ID)) throw new ArgumentNullException("ADM ID cannot be null or empty");
            if (_adms.ContainsKey(adm.ID)) throw new InvalidOperationException(String.Format("Cannot add ADM with ID {0} as one is alread added", adm.ID)); ;


            _adms[adm.ID] = adm;
            adm.Tracing = Tracing;
            return adm;
        }

        protected ArduinoDeviceManager GetADM(String id)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException("ADM ID cannot be null or empty");
            if (_adms.Count == 1 && id.ToLower() == "adm")
            {
                return _adms.First().Value;
            } else
            {
                if (!_adms.ContainsKey(id)) throw new Exception(String.Format("Cannot find ADM with ID {0}", id));
                return _adms[id];
            }
        }


        //Comms between chetch messaging client and this service
        
        /// <summary>
        /// Incoming command from a client.  This command has three possible destinations: 1) The ADM board, 2) A particular device 3) a
        /// device group. Once the command is handled formulate a response (i.e. write data to the response message) and return true for the response
        /// to be sent (false for the response NOT to be sent).  Depending on the command the destination will update in some way which will be caught
        /// by the property change event handler which in turn may broadcast a message to any service subscribers
        /// </summary>
        /// <param name="cnn"></param>
        /// <param name="message"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        public override bool HandleCommand(Connection cnn, Message message, string command, List<Object> args, Message response)
        {
            bool respond = true;
            MessageSchema schema = new MessageSchema(response);

            switch (command)
            {
                case MessageSchema.COMMAND_STATUS:
                    schema.AddADMs(_adms);
                    break;

                default:
                    ArduinoDeviceManager adm;
                    var tgtcmd = command.Split(':');
                    if (tgtcmd.Length < 2)
                    {
                        throw new Exception(String.Format("Unrecognised command {0}", command));
                    }

                    //so this is an ADM command, find the board first
                    adm = GetADM(tgtcmd[0].Trim());
                    if (!adm.IsConnected)
                    {
                        throw new Exception(String.Format("{0} is not conntected", adm.ID));
                    }

                    //handle commands related to the board (i.e. not to a specific added device)
                    if (tgtcmd.Length == 2) //arguments: the adm then the command
                    {
                        switch (tgtcmd[1].Trim().ToLower())
                        {
                            case MessageSchema.COMMAND_STATUS:
                                ADMRequestManager.ADMRequest req = adm.RequestStatus(true);
                                req.Owner = message.Sender;
                                schema.AddADM(adm);
                                break;

                            case MessageSchema.COMMAND_PING:
                                adm.Ping();
                                break;

                            case MessageSchema.COMMAND_LIST_DEVICES:
                                break;

                            default:
                                throw new Exception(String.Format("Unrecognised command: {0}", tgtcmd[1]));
                        }
                    }
                    else
                    {
                        String deviceOrGroupID = tgtcmd[1].Trim();
                        ArduinoDevice device = adm.GetDevice(deviceOrGroupID);
                        ArduinoDeviceGroup deviceGroup = device == null ? adm.GetDeviceGroup(deviceOrGroupID) : null;
                        if (device == null && deviceGroup == null)
                        {
                            throw new Exception(String.Format("Cannot find device OR device group {0}", deviceOrGroupID));
                        }
                        List<String> commands = tgtcmd[2].Split(',').ToList();
                        foreach (var cmd in commands)
                        {
                            if (cmd == MessageSchema.COMMAND_WAIT)
                            {
                                int delay = cmd.Length > 4 ? System.Convert.ToInt16(cmd.Substring(4, cmd.Length - 4)) : 200;
                                System.Threading.Thread.Sleep(delay);
                                continue;
                            }

                            if (device != null)
                            {
                                switch (cmd)
                                {
                                    case MessageSchema.COMMAND_STATUS:
                                        device.RequestStatus();
                                        schema.AddDevice(device);
                                        break;

                                    case MessageSchema.COMMAND_LIST_COMMANDS:
                                        break;

                                    default:
                                        device.ExecuteCommand(cmd, args);
                                        break;
                                }
                            }
                            else if (deviceGroup != null)
                            {
                                switch (cmd)
                                {
                                    case MessageSchema.COMMAND_STATUS:
                                        deviceGroup.RequestStatus();
                                        break;

                                    default:
                                        deviceGroup.ExecuteCommand(cmd, args);
                                        break;
                                }
                            }
                        } //end loop throgh commands
                    }
                    break;
            }
            return respond;
        }
    
        /// <summary>
        /// Sends a command in the correct format based on command line input
        /// </summary>
        /// <param name="cnn"></param>
        /// <param name="commandLine"></param>
        static public void FormatAndSendCommand(ClientConnection cnn, String target, String commandLine)
        {
            String[] parts = commandLine.Split(' ');
            String command = parts[0];
            List<Object> args = new List<object>();
            for(int i = 1; i < parts.Length; i++)
            {
                args.Add(parts[i]);
            }
            cnn.SendCommand(target, command, args.Count == 0 ? null : args);
        }
    }
}
