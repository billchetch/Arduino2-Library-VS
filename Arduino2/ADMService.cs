using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Chetch.Messaging;
using Chetch.Arduino;
using Chetch.Services;

namespace Chetch.Arduino2
{
    abstract public class ADMService : TCPMessagingClient
    {
        class MessageSchema : Chetch.Messaging.MessageSchema
        {

            public const String ADM_FIELD_NAME_PREFIX = "ADM";
            public const String DEVICE_FIELD_NAME_PREFIX = "Device";

            public const String COMMAND_STATUS = "status";
            public const String COMMAND_PING = "ping";
            public const String COMMAND_LIST_DEVICES = "list-devices";
            public const String COMMAND_LIST_COMMANDS = "list-commands";
            public const String COMMAND_WAIT = "wait";
            
            public MessageSchema() { }

            public MessageSchema(Message message) : base(message) { }

            public void AddADMs(Dictionary<String, ArduinoDeviceManager> adms)
            {
                if (adms != null && adms.Count > 0)
                {
                    foreach (ArduinoDeviceManager adm in adms.Values)
                    {
                        Dictionary<String, Object> vals = new Dictionary<String, Object>();
                        adm.Serialize(vals);
                        Message.AddValue("ADM:" + adm.ID, vals);
                    }
                }
                else
                {
                    Message.AddValue("ADMS", "No boards connected");
                }
            }

            protected void AddValuesWithPrefix(String prefix, Dictionary<String, Object> vals)
            {
                foreach(KeyValuePair<String, Object> kvp in vals){
                    String key = prefix + ":" + kvp.Key;
                    Message.AddValue(key, kvp.Value);
                }
            }

            public void AddADM(ArduinoDeviceManager adm)
            {
                Dictionary<String, Object> vals = new Dictionary<String, Object>();
                adm.Serialize(vals);
                AddValuesWithPrefix(ADM_FIELD_NAME_PREFIX, vals);
            }

            public void AddDevice(ArduinoDevice device)
            {
                Dictionary<String, Object> vals = new Dictionary<String, Object>();
                device.Serialize(vals);
                AddValuesWithPrefix(DEVICE_FIELD_NAME_PREFIX, vals);
            }
        }

        class ADMRequest
        {
            public String RequesterID;
            public byte Tag;
            public String Target;
            public long Requested;
            private int _ttl;
            public ADMRequest(String requesterID, byte tag, String target, int ttl = 60 * 1000)
            {
                RequesterID = requesterID;
                Tag = tag;
                Target = target;
                Requested = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                _ttl = ttl;
            }

            public bool Expired {
                get
                {
                    long nowInMillis = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    return nowInMillis - Requested > _ttl;
                }
            }
        }
    

        private Dictionary<String, ArduinoDeviceManager> _adms  = new Dictionary<String, ArduinoDeviceManager>();
        private List<ADMRequest> _admRequests = new List<ADMRequest>();

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
            AddCommandHelp("adm/<board>:<device>:" + MessageSchema.COMMAND_WAIT, "Pause for a short while, useful if interspersed with other, comma-seperated, commands");
            AddCommandHelp("adm/<board>:<device>:" + MessageSchema.COMMAND_LIST_COMMANDS, "List device commands");
            AddCommandHelp("adm/<board>:<device>:" + MessageSchema.COMMAND_STATUS, "Status of device");
        }

        protected override void OnStop()
        {
            foreach (var adm in _adms.Values)
            {
                adm.Disconnect();
            }
            base.OnStop();
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

            foreach (var adm in _adms.Values)
            {
                do
                {
                    try
                    {
                        Tracing?.TraceEvent(TraceEventType.Information, 0, "ADM {0} is starting up...", adm.ID);
                        adm.Begin(5000);
                        Tracing?.TraceEvent(TraceEventType.Information, 0, "ADM {0} is ready for use", adm.ID);
                    }
                    catch (Exception e)
                    {
                        Tracing?.TraceEvent(TraceEventType.Error, 0, "Exception: ADM {0} {1}", adm.ID, e.Message);
                    }
                } while (!adm.IsReady);
            }
        }

        protected ArduinoDeviceManager AddADM(ArduinoDeviceManager adm)
        {
            if (adm == null) throw new ArgumentNullException("ADM cannot be null");
            if(String.IsNullOrEmpty(adm.ID)) throw new ArgumentNullException("ADM ID cannot be null or empty");
            if (_adms.ContainsKey(adm.ID)) throw new InvalidOperationException(String.Format("Cannot add ADM with ID {0} as one is alread added", adm.ID)); ;


            adm.MessageReceived += TryHandleADMMessage;
            _adms[adm.ID] = adm;
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
        protected void AddADMRequest(ArduinoDeviceManager adm, byte messageTag, Message response)
        {
            AddADMRequest(adm.ID, messageTag, response.Target);
        }

        protected void AddADMRequest(ArduinoDevice device, byte messageTag, Message response)
        {
            AddADMRequest(device.FullID, messageTag, response.Target);
        }

        protected void AddADMRequest(String requesterID, byte messageTag, String responseTarget)
        {
            if (messageTag == 0)
            {
                throw new ArgumentException("ADMRequest: Message tag cannot be 0");
            }

            //an opportunity to prune
            List<ADMRequest> expired = new List<ADMRequest>();
            foreach (var req in _admRequests)
            {
                if (req.Expired)
                {
                    expired.Add(req);
                }
            }
            foreach(var req in expired)
            {
                _admRequests.Remove(req);
            }

            _admRequests.Add(new ADMRequest(requesterID, messageTag, responseTarget));

        }

        protected String GetADMRequestTarget(String requesterID, byte messageTag)
        {
            if (messageTag == 0) return null;

            String target = null;
            ADMRequest req2remove = null;
            foreach(var req in _admRequests)
            {
                if(req.RequesterID == requesterID && req.Tag == messageTag)
                {
                    req2remove = req;
                    target = req.Expired ? null : req.Target;
                    break;
                }
            }
            if (req2remove != null)
            {
                _admRequests.Remove(req2remove);
            }
            return target;
        }

        protected String GetADMRequestTarget(ArduinoDeviceManager adm, byte messageTag)
        {
            return GetADMRequestTarget(adm.ID, messageTag);
        }

        protected String GetADMRequestTarget(ArduinoDevice device, byte messageTag)
        {
            return GetADMRequestTarget(device.FullID, messageTag);
        }

        /// <summary>
        /// Incoming command from a client.  This command has two possible destinations: 1) The ADM board, 2) A particular device.
        /// Once the command is handled formulate a response (i.e. write data to the response message) and return true for the response
        /// to be sent (false for the response NOT to be sent)
        /// </summary>
        /// <param name="cnn"></param>
        /// <param name="message"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        public override bool HandleCommand(Connection cnn, Message message, string command, List<ValueType> args, Message response)
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
                                break;

                            case MessageSchema.COMMAND_PING:
                                break;

                            case MessageSchema.COMMAND_LIST_DEVICES:
                                break;
                        }
                    } else
                    {
                        if(tgtcmd.Length < 4)
                        {
                            throw new Exception(String.Format("No device command found when parsing command: {0}", command));
                        }
                        String deviceID = tgtcmd[2].Trim();
                        ArduinoDevice device = adm.GetDevice(deviceID);
                        if(device == null)
                        {
                            throw new Exception(String.Format("Cannot find device {0}", deviceID));
                        }
                        List<String> deviceCommands = tgtcmd[3].Split(',').ToList();
                        //these commands are relaed to the device ... firs are meta commands ... the rest need to be sent to the device
                        switch (deviceCommands[0])
                        {
                            case MessageSchema.COMMAND_STATUS:
                                break;

                            case MessageSchema.COMMAND_LIST_COMMANDS:
                                break;

                            default:
                                foreach(var deviceCommand in deviceCommands)
                                {
                                    if (deviceCommand == MessageSchema.COMMAND_WAIT)
                                    {
                                        int delay = deviceCommand.Length > 4 ? System.Convert.ToInt16(deviceCommand.Substring(4, deviceCommand.Length - 4)) : 200;
                                        System.Threading.Thread.Sleep(delay);
                                    }
                                    else
                                    {
                                        byte tag = device.ExecuteCommand(deviceCommand, args);
                                        AddADMRequest(device, tag, response);
                                    }
                                }
                                break;
                        }
                    }
                    break;
            }
            return respond;
        }

        //Comms between ADM and this service

        /// <summary>
        /// This intercepts a message coming from an ADM and sends it to HandleADMMessage for service specific processing.  If the service
        /// method return true then the message will be braodcast to all subscribers to this service
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void TryHandleADMMessage(Object sender, ArduinoDeviceManager.MessageReceivedArgs args)
        {
            try
            {
                ADMMessage message = args.Message;
                if(sender is ArduinoDeviceManager)
                {
                    ArduinoDeviceManager adm = (ArduinoDeviceManager)sender;
                    ArduinoDevice device = null;
                    if(message.TargetID != ArduinoDeviceManager.ADM_TARGET_ID)
                    {
                        device = adm.GetDevice(message.TargetID);
                    }

                    Message messageToBroadcast = new Message(message.Type);
                    MessageSchema schema = new MessageSchema(messageToBroadcast);
                    if (device != null)
                    {
                        messageToBroadcast.Sender = device.ID;
                        messageToBroadcast.Target = GetADMRequestTarget(device, message.Tag);
                        schema.AddDevice(device);
                        
                    } else
                    {
                        messageToBroadcast.Sender = adm.ID;
                        messageToBroadcast.Target = GetADMRequestTarget(adm, message.Tag);
                        schema.AddADM(adm);   
                    }
                    

                    bool broadcast = HandleADMMessage(adm, device, message, messageToBroadcast);

                    if (broadcast)
                    {
                        Broadcast(messageToBroadcast);
                    }
                } 
            } catch (Exception e)
            {
                Tracing?.TraceEvent(TraceEventType.Error, 0, e.Message);
            }
        }

        virtual protected bool HandleADMMessage(ArduinoDeviceManager adm, ArduinoDevice device, ADMMessage message, Message messageToBroadcast)
        {
            //A hook at present designed to allow the service to cancel broadcast and/or modify the message to be broadcast
            return true;
        }

    }
}
