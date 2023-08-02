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

            private void addArduinoObject(String prefix, ArduinoObject ao, bool changedPropertiesOnly = false)
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
                addArduinoObject(ADM_FIELD_NAME_PREFIX, adm);
            }

            public void AddDevice(ArduinoDevice device, bool changedPropertiesOnly = false)
            {
                addArduinoObject(DEVICE_FIELD_NAME_PREFIX, device, changedPropertiesOnly);
            }

            public void AddDeviceGroup(ArduinoDeviceGroup deviceGroup, bool changedPropertiesOnly = false)
            {
                addArduinoObject(DEVICE_GROUP_FIELD_NAME_PREFIX, deviceGroup, changedPropertiesOnly);
            }

            public void AddAO(ArduinoObject ao)
            {
                if (ao is ArduinoDevice)
                {
                    AddDevice((ArduinoDevice)ao);
                }
                else if (ao is ArduinoDeviceGroup)
                {
                    AddDeviceGroup((ArduinoDeviceGroup)ao);
                }
                else if (ao is ArduinoDeviceManager)
                {
                    AddADM((ArduinoDeviceManager)ao);
                }
            }
        }

        protected const int BEGIN_ADMS_TIMER_INTERVAL = 30 * 1000;
        protected const int BEGIN_TIMEOUT = 8000;
        protected const int MAX_BEGIN_ATTEMPTS = 3;
        protected const int DEFAULT_LOG_SNAPSHOPT_TIMER_INTERVAL = 30 * 1000;
        
        protected ADMServiceDB ServiceDB { get; set; }

        protected bool ServiceIsStopping { get; private set; } = false;

        private System.Timers.Timer _beginADMsTimer;
        private bool _admsCreated = false;
        private Dictionary<String, ArduinoDeviceManager> _adms  = new Dictionary<String, ArduinoDeviceManager>();
        public List<ArduinoDeviceManager> ADMs => _adms.Values.ToList();

        private List<ArduinoObject> _aos = new List<ArduinoObject>(); //list of arduino objects created, serialized and event handler attached
        
        private System.Timers.Timer _logSnapshotTimer;
        protected int LogSnapshotTimerInterval { get; set; } = DEFAULT_LOG_SNAPSHOPT_TIMER_INTERVAL;

        private Dictionary<ArduinoObject, Message> _messagesToDispatch = new Dictionary<ArduinoObject, Message>();
        private Object _dispatchMessageLock = new object();

        
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
                if(_logSnapshotTimer == null)
                {
                    _logSnapshotTimer = new System.Timers.Timer(LogSnapshotTimerInterval);
                    _logSnapshotTimer.Elapsed += OnLogSnapshotTimer;
                }

                if (!_logSnapshotTimer.Enabled)
                {
                    _logSnapshotTimer.Start();
                }
            }

            base.OnStart(args);
        }

        protected override void OnStop()
        {
            ServiceIsStopping = true;

            if(_beginADMsTimer != null)
            {
                _beginADMsTimer.Stop();
                _beginADMsTimer = null;
            }
            
            if (_logSnapshotTimer != null)
            {
                _logSnapshotTimer.Stop();
                _logSnapshotTimer = null;
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
                    ao.PropertyChanged -= HandleAOPropertyChange;

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


            _admsCreated = false;
            _adms.Clear();

            base.OnStop();

            ServiceIsStopping = false;
        }

        virtual protected bool CanLogToSnapshot(ArduinoObject ao)
        {
            return true;
        }


        virtual protected List<ADMServiceDB.SnapshotLogEntry> GetSnapshotLogEntries(ArduinoObject ao)
        {
            return null;
        }

        virtual protected void OnLogSnapshotTimer(Object sender, EventArgs earg)
        {
            foreach (var adm in _adms.Values)
            {
                if (!adm.IsReady) continue;

                List<ArduinoObject> aoToSnapshot = GetArduinoObjects();
                foreach (var ao in aoToSnapshot)
                {
                    if (CanLogToSnapshot(ao))
                    {
                        var entries = GetSnapshotLogEntries(ao);
                        if (entries != null && entries.Count > 0)
                        {
                            ServiceDB.LogSnapshot(entries);
                        }
                    }
                } //end loop through aos of this adm
            } //end loop through adms
        }

        virtual protected bool CanLogEvent(ArduinoObject ao, String eventName)
        {
            return false;
        }

        virtual protected ADMServiceDB.EventLogEntry GetEventLogEntry(ArduinoObject ao, DSOPropertyChangedEventArgs dsoArgs)
        {
            var entry = new ADMServiceDB.EventLogEntry();
            ArduinoObject.ArduinoPropertyAttribute propertyAttribute = (ArduinoObject.ArduinoPropertyAttribute)ao.GetPropertyAttribute(dsoArgs.PropertyName);

            entry.Name = dsoArgs.PropertyName;
            entry.Source = ao.UID;

            if (propertyAttribute.IsError)
            {
                entry.Info = ao.ErrorInfo;
            }
            else
            {
                entry.Info = String.Format("{0} changed from {1} to {2}", entry.Name, dsoArgs.OldValue, dsoArgs.NewValue);
            }
            entry.Data = dsoArgs.NewValue;
            return entry;
        }

        virtual protected bool CanDispatch(ArduinoObject ao, String propertyName)
        {
            return false;
        }

        //loggging events
        virtual protected void HandleAOPropertyChange(Object sender, PropertyChangedEventArgs eargs)
        {
            //Get Event data
            DSOPropertyChangedEventArgs dsoArgs = (DSOPropertyChangedEventArgs)eargs;
            ArduinoObject ao = ((ArduinoObject)sender);
            ArduinoObject.ArduinoPropertyAttribute propertyAttribute = (ArduinoObject.ArduinoPropertyAttribute)ao.GetPropertyAttribute(dsoArgs.PropertyName);
            
            if(propertyAttribute == null)
            {
                //this can happen if an AO uses Set DSO method but doesn't declare an explicity ArduinoProperty
                ///in which case there's little we can do with this in terms of logging so return
                return;
            }

            //First we deal with a state change (i.e. an Event)
            if ((propertyAttribute.IsState || propertyAttribute.IsError) && CanLogEvent(ao, dsoArgs.PropertyName))
            {
                try
                {
                    ADMServiceDB.EventLogEntry entry = GetEventLogEntry(ao, dsoArgs);
                    ServiceDB.LogEvent(entry);
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
                var adm = GetADMFromArduinoObject(ao);
                if (adm.ProcessingRequest != null)
                {
                    message.Target = adm.ProcessingRequest.Owner;
                }

                schema.AddAO((ArduinoObject)sender);
                
                DispatchMessage(ao, message);
            }
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
                    foreach(var msg in _messagesToDispatch.Values)
                    {
                        //this will send to all subscribers and if the message has a target will send to that target as well
                        Broadcast(msg);
                    }

                    _messagesToDispatch.Clear();
                }
            });
        }

        public override void HandleClientError(Connection cnn, Exception e)
        {
            //TODO: something here   
        }

        protected override void OnClientConnect(ClientConnection cnn)
        {
            base.OnClientConnect(cnn);

            if(_beginADMsTimer == null)
            {
                _beginADMsTimer = new System.Timers.Timer();
                _beginADMsTimer.Elapsed += OnBeginADMsTimer;
                _beginADMsTimer.Interval = 1000; //so we don't wait long ... the event handler will then set it properly
                Tracing?.TraceEvent(TraceEventType.Information, 0, "Client {0} has connected so starting up timer to create and begin ADMs", cnn.Name);
                _beginADMsTimer.Start();
            }
        }

        //create ADMs here ...if all the required adms have been created return true otherwise return false or throw an exception
        abstract protected bool CreateADMs();

        virtual protected void OnADMsReady()
        {
            //a hook .. this fires when all the adms created IsReady property will be true for the first time
            ServiceDB?.LogEvent("ADMs Ready", ServiceName, null, String.Format("All {0} adms are ready to use", ADMs.Count));
        }

        private void OnBeginADMsTimer(Object sender, EventArgs earg)
        {
            _beginADMsTimer.Stop();
            if (ServiceIsStopping) return;

            if (!_admsCreated)
            {
                try
                {
                    _admsCreated = CreateADMs();
                    _aos.Clear();

                    List<ArduinoObject> aoToInitialise = GetArduinoObjects();
                    foreach (var ao in aoToInitialise)
                    {
                        if (_aos.Contains(ao)) continue;

                        //Add handler so we can respond to property changes
                        ao.PropertyChanged += HandleAOPropertyChange;

                        if (ServiceDB != null)
                        {
                            //deserialize
                            SysInfo si = ServiceDB.GetSysInfo(ao.UID);
                            if (si != null)
                            {
                                ao.Deserialize(si.DataValue);
                            }
                        }

                        _aos.Add(ao);
                    }
                }
                catch (Exception e)
                {
                    _beginADMsTimer.Start();
                    Tracing?.TraceEvent(TraceEventType.Error, 0, "Exception: {0}", e.Message);
                    return;
                }
            }

            //begin the adms that are ready to begin
            int admsReadyCount = 0;
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
                        admsReadyCount++;
                        if(admsReadyCount == _adms.Count)
                        {
                            OnADMsReady();
                        }
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

            //ensure we using the correct timer interval as first timer event is only 1 sec after creation
            _beginADMsTimer.Interval = BEGIN_ADMS_TIMER_INTERVAL;
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
                                adm.RequestStatus(message.Sender);
                                break;

                            case MessageSchema.COMMAND_PING:
                                adm.Ping(message.Sender);
                                break;

                            case MessageSchema.COMMAND_LIST_DEVICES:
                                var devs = adm.GetDevices();
                                List<String> devDescs = new List<String>();
                                foreach (var dev in devs) 
                                {
                                    devDescs.Add(dev.ToString());
                                }
                                response.AddValue("ADM", adm.ToString());
                                response.AddValue("Devices", devDescs);
                                break;

                            case MessageSchema.COMMAND_LIST_GROUPS:
                                var dgs = adm.GetDeviceGroups();
                                List<String> dgDescs = new List<String>();
                                response.AddValue("ADM", adm.ToString());
                                foreach (var dg in dgs)
                                {
                                    dgDescs.Add(dg.ToString());
                                }
                                response.AddValue("DeviceGroups", dgDescs);
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
                                        device.RequestStatus(message.Sender);
                                        break;

                                    case MessageSchema.COMMAND_PING:
                                        device.Ping(message.Sender);
                                        break;

                                    case MessageSchema.COMMAND_LIST_COMMANDS:
                                        response.AddValue("Device", device.ToString());
                                        List<String> commandDescs = new List<String>();
                                        foreach(var c in device.Commands)
                                        {
                                            commandDescs.Add(String.Format("{0} {1}", c.Alias, c.IsCompound ? "Compound" : "Atomic"));
                                        }
                                        response.AddValue("Commands", commandDescs);
                                        break;

                                    default:
                                        var req = device.ExecuteCommand(cmd, args);
                                        if (req != null)
                                        {
                                            req.Owner = Client.Name;
                                        }
                                        break;
                                }
                            }
                            else if (deviceGroup != null)
                            {
                                switch (cmd)
                                {
                                    case MessageSchema.COMMAND_STATUS:
                                        deviceGroup.RequestStatus(message.Sender);
                                        break;

                                    default:
                                        deviceGroup.ExecuteCommand(cmd, Client.Name, args);
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
