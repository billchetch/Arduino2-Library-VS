﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using Chetch.Utilities;
using Chetch.Utilities.Streams;
using Chetch.Messaging;


namespace Chetch.Arduino2
{
    public class ArduinoDeviceManager : ArduinoObject
    {
        public const byte ADM_TARGET_ID = 0;
        public const byte ADM_STREAM_TARGET_ID = 255;
        public const int ADM_MESSAGE_SIZE = 50; //in bytes
        public const byte RESET_ADM_COMMAND = 201;
        public const int DEFAULT_CONNECT_TIMEOUT = 5000;
        public const int DEFAULT_SYNCHRONISE_TIMEOUT = 3000;
        public const int DEFAULT_SYNCHRONISE_TIMER_INTERVAL = 1000;
        public const int DEFAULT_INACTIVITY_TIMEOUT = 10000;

        public enum ADMErrorCode
        {
            NO_ERROR = 0,
            NO_ADM_INSTANCE = 1,
            MESSAGE_FRAME_ERROR = 10,
            ADM_MESSAGE_ERROR = 11,
            ADM_MESSAGE_IS_EMPTY= 12,
            ADM_FAILED_TO_INITIALISE = 13,
            ADM_FAILED_TO_CONFIUGRE = 14,
            NO_DEVICE_ID = 20,
            DEVICE_LIMIT_REACHED = 21,
            DEVICE_ID_ALREADY_USED = 22,
            DEVICE_NOT_FOUND = 23,
            DEVICE_CANNOT_BE_CREATED = 24,
            DEVICE_ERROR = 100,
        }

        public enum AttachmentMode
        {
            NOT_SET = 0,
            MASTER_SLAVE,
            OBSERVER_OBSERVED,
        }

        public enum  AnalogReference
        {
            AREF_EXTERNAL = 0,
            AREF_INTERNAL,
            AREF_INTERNAL1V1, //mega only
            AREF_INTERNAL2V56, //mega only
        }

        public enum ADMState
        {
            CREATED = 1,
            BEGINNING,
            BEGUN,
            INITIALISING,
            INITIALISED,
            INITIALISE_FAILED,
            CONFIGURING,
            CONFIGURED,
            CONFIGURE_FAILED,
            DEVICE_INITIALISING,
            DEVICE_INITIALISED,
            DEVICE_INITIALISE_FAILED,
            DEVICE_CONFIGURING,
            DEVICE_CONFIGURED,
            DEVICE_CONFIGURE_FAILED,
        }

        

        public static ArduinoDeviceManager Create(String boardName, int baudRate, int localUartSize, int remoteUartSize, int connectTimeout = DEFAULT_CONNECT_TIMEOUT, bool useGenericUSBDeviceAsFallback = true)
        {
            try
            {
                var ports = SerialPorts.Find(boardName);
                if(ports.Count == 0 && useGenericUSBDeviceAsFallback)
                {
                    ports = SerialPorts.Find(ArduinoSerialConnection.USB_SERIAL_DEVICE);
                }

                if(ports.Count == 0)
                {
                    throw new Exception(String.Format("Cannot find a Serial Port for board {0}", boardName));
                }

                var serial = new ArduinoSerialConnection(ports[0], baudRate);
                var sfc = new StreamFlowController(serial, localUartSize, remoteUartSize);
                var adm = new ArduinoDeviceManager(sfc, connectTimeout);
                adm.ID = ports[0];
                return adm;
            } catch(Exception e)
            {
                throw new Exception(String.Format("Cannot create Serial Connection ADM {0}: {1}", boardName, e.Message));
            }
        }

        //Usually a single board will register itself as a unique service using the 'hostname' of the board connection as the service
        //So for example if the hostname of the board as connected to wifi is 'unohost' then there will be a corresponding service called
        //'unohost' registered with the network service.  The ArduinoTCPConnection will then attempt to find that service and make a connection
        //thereby connecting to the board 'unohost'
        public static ArduinoDeviceManager Create(String serviceName, String networkServiceURL, int localUartSize, int remoteUartSize, int connectTimeout = DEFAULT_CONNECT_TIMEOUT)
        {
            try
            {
                var cnn = new ArduinoTCPConnection(serviceName, networkServiceURL);
                var sfc = new StreamFlowController(cnn, localUartSize, remoteUartSize);
                var adm = new ArduinoDeviceManager(sfc, connectTimeout);
                adm.ID = serviceName;
                return adm;
            } catch (Exception e) 
            { 
                throw new Exception(String.Format("Cannot create TCP Connection ADM {0}: {1}", serviceName, e.Message));
            }
        }

        public TraceSource Tracing { get; set; } = null;

        [ArduinoProperty(PropertyAttribute.IDENTIFIER)]
        override public String UID => ID;

        private StreamFlowController _sfc;
        private int _connectTimeout = DEFAULT_CONNECT_TIMEOUT;

        public int InactivityTimeout { get; set; } = DEFAULT_INACTIVITY_TIMEOUT;

        public AttachmentMode AttachMode { get; set; } = AttachmentMode.MASTER_SLAVE;

        public AnalogReference AREF { get; set; } = AnalogReference.AREF_INTERNAL;

        [ArduinoProperty(ArduinoPropertyAttribute.STATE, false)]
        public bool Connecting 
        {
            get { return Get<bool>(); }
            internal set { Set(value); }
        }

        [ArduinoProperty(ArduinoPropertyAttribute.STATE, false)]
        public bool Disconnecting
        {
            get { return Get<bool>(); }
            internal set { Set(value); }
        }

        public bool IsConnected => _sfc.IsReady; //an alias for the readiness of the connection
        
        private bool _synchronising = false;
        private bool _synchronised = false;
        
        public bool Synchronising
        {
            get { return _synchronising; }
            internal set
            {
                _synchronising = value;
                if (_synchronising) _synchronised = false;
            }
        }
        public bool Synchronised
        {
            get { return _synchronised; }
            internal set
            {
                _synchronised = value;
                if (_synchronised) _synchronising = false;
            }
        }

        [ArduinoProperty(ArduinoPropertyAttribute.STATE, ADMState.CREATED)]
        public ADMState State
        {
            get { return Get<ADMState>(); }
            internal set { Set(value, value > ADMState.CREATED); }
        }


        //Devices
        private Dictionary<String, ArduinoDevice> _devices = new Dictionary<string, ArduinoDevice>();

        private Dictionary<String, ArduinoDeviceGroup> _deviceGroups = new Dictionary<string, ArduinoDeviceGroup>();

        public bool IsBoardReady => IsConnected && ((int)State >= (int)ADMState.CONFIGURED);
        public bool IsDeviceReady => IsConnected && (State == ADMState.DEVICE_CONFIGURED);

        public bool IsReady => IsEmpty ? IsBoardReady : IsDeviceReady;

        public bool IsEmpty => _devices.Count == 0;

        [ArduinoProperty(PropertyAttribute.DESCRIPTOR)]
        public int DeviceCount => _devices.Count;

        [ArduinoProperty(PropertyAttribute.DESCRIPTOR)]
        public int DeviceGroupCount => _deviceGroups.Count;

        public ADMRequestManager Requests { get; } = new ADMRequestManager();
        
        public ADMRequestManager.ADMRequest ProcessingRequest { get; internal set; } = null;


        [ArduinoProperty(ArduinoPropertyAttribute.METADATA | PropertyAttribute.DESCRIPTOR)]
        public int MessagesSemt { get; internal set; } = 0;

        [ArduinoProperty(ArduinoPropertyAttribute.METADATA | PropertyAttribute.DESCRIPTOR)]
        public int MessagesReceived { get; internal set; } = 0;

        [ArduinoProperty(ArduinoPropertyAttribute.METADATA | PropertyAttribute.DESCRIPTOR)]
        public ulong BytesSent => _sfc.BytesSent;

        [ArduinoProperty(ArduinoPropertyAttribute.METADATA | PropertyAttribute.DESCRIPTOR)]
        public ulong BytesReceived => _sfc.BytesReceived;

        [ArduinoProperty(ArduinoPropertyAttribute.METADATA | PropertyAttribute.DESCRIPTOR)]
        public DateTime LastMessageReceivedOn { get; internal set; }

        private System.Timers.Timer _synchroniseTimer;

        [ArduinoProperty(ArduinoPropertyAttribute.METADATA | PropertyAttribute.DESCRIPTOR, PropertyAttribute.DATETIME_DEFAULT_VALUE_MIN)]
        public DateTime LastStatusResponseOn
        {
            get { return Get<DateTime>(); }
            set { Set(value, IsReady, IsReady); }
        }

        [ArduinoProperty(ArduinoPropertyAttribute.METADATA | PropertyAttribute.DESCRIPTOR, PropertyAttribute.DATETIME_DEFAULT_VALUE_MIN)]
        public DateTime LastPingResponseOn
        {
            get { return Get<DateTime>(); }
            set { Set(value, IsReady, IsReady); }
        }

        //Board properties (assigned by return message from initialise response)
        [ArduinoProperty(PropertyAttribute.DESCRIPTOR)]
        public String BoardName { get; internal set; }

        [ArduinoProperty(PropertyAttribute.DESCRIPTOR)]
        public int BoardMaxDevices { get; internal set; } = 0;

        //Board specified properties (assigned by return message from Initialise or RequestStatus)
        [ArduinoProperty(PropertyAttribute.DESCRIPTOR)]
        public long BoardMillis { get; internal set; } = 0;

        [ArduinoProperty(PropertyAttribute.DESCRIPTOR)]
        public int BoardMemory { get; internal set; } = 0;

        [ArduinoProperty(PropertyAttribute.DESCRIPTOR)]
        public bool BoardInitialised { get; internal set; } = false;

        [ArduinoProperty(PropertyAttribute.DESCRIPTOR)]
        public bool BoardConfigured { get; internal set; } = false;

        [ArduinoProperty(PropertyAttribute.DESCRIPTOR)]
        public long BoardLoopDuration { get; internal set; } = 0;

        [ArduinoProperty(PropertyAttribute.DESCRIPTOR)]
        public long BoardBytesReceived { get; internal set; } = 0;

        [ArduinoProperty(PropertyAttribute.DESCRIPTOR)]
        public long BoardBytesSent { get; internal set; } = 0;

        public ArduinoDeviceManager(StreamFlowController sfc, int connectTimeout)
        {
            _sfc = sfc;
            _sfc.CTSTimeout = 4000; //in ms
            _sfc.StreamError += HandleStreamError;
            _sfc.DataBlockReceived += HandleStreamData;
            _sfc.CommandByteReceived += HandleStreamCommandByteReceived;
            _sfc.EventByteReceived += HandleStreamEventByteReceived;
            _sfc.EventByteSent += HandleStreamEventByteSent;
            _connectTimeout = connectTimeout;
            State = ADMState.CREATED;
        }

        public override string ToString()
        {
            return String.Format("{0} {1}, {2} devices, {3} groups", UID, State, _devices.Count, _deviceGroups.Count);
        }

        override public void Serialize(Dictionary<String, Object> vals)
        {
            base.Serialize(vals);
        }

        public override void Deserialize(Dictionary<string, object> source, bool notify = false)
        {
            source.Remove("State");
            source.Remove("Connected");
            base.Deserialize(source, notify);
        }

        private void wait(int sleep, DateTime started = default(DateTime), int timeout = -1, String timeoutMessage = "Timed out!")
        {
            if (timeout > 0)
            {
                if (Measurement.HasTimedOut(started, timeout))
                {
                    Tracing?.TraceEvent(TraceEventType.Error, 300, "ADM {0} Error: {1}", ID, timeoutMessage);
                    throw new TimeoutException(timeoutMessage);
                }
            }
            Thread.Sleep(sleep);
        }


        public void Connect(int timeout = -1)
        {
            if (Connecting) throw new InvalidOperationException("ADM is in the process of connecting");
            if (IsConnected) throw new InvalidOperationException("ADM is already connected");
            if (_sfc.IsOpen) throw new InvalidOperationException("Underlying stream is open");
            try
            {
                Connecting = true;
                if (timeout <= 0) timeout = _connectTimeout;
                DateTime started = DateTime.Now;
                Tracing?.TraceEvent(TraceEventType.Verbose, 0, "ADM {0} Connecting... will timeout after {1} ms", ID, timeout);
                do
                {
                    
                    //Some connections e.g. TCPClient need to be made new if connecting to same end point
                    if (_sfc.Stream is ArduinoTCPConnection)
                    {
                        var cnn = (ArduinoTCPConnection)_sfc.Stream;
                        if (cnn.RemoteEndPoint != null)
                        {
                            Tracing?.TraceEvent(TraceEventType.Verbose, 0, "ADM {0} creating new ArduinoTCPConnection as existing one has no remote end point", ID);
                            _sfc.Stream = new ArduinoTCPConnection(cnn);
                        }
                    }

                    Tracing?.TraceEvent(TraceEventType.Verbose, 0, "ADM {0} Attempting to open stream... ", ID);
                    try
                    {
                        _sfc.Open();
                    }
                    catch (Exception e)
                    {
                        Tracing?.TraceEvent(TraceEventType.Error, 311, "ADM {0} Error: {1}", ID, e.Message);
                    }
                    if (!_sfc.IsOpen)
                    {
                        wait(500, started, timeout, "Connecting timed out waiting for stream to open");
                    }
                    
                } while (!_sfc.IsOpen);

                Tracing?.TraceEvent(TraceEventType.Verbose, 0, "ADM {0} Stream opened!", ID);

                //we now wait for the stream flow controller to synchronise stream reset
                while (!_sfc.IsReady)
                {
                    wait(500, started, timeout, "Connecting timed out waiting for stream to become ready");
                }

                //by here the stream is open and reset and ready for use
                Tracing?.TraceEvent(TraceEventType.Verbose, 0, "ADM {0} Stream is ready!", ID);
            }
            catch (Exception e)
            {
                if (_sfc.IsOpen)
                {
                    Tracing?.TraceEvent(TraceEventType.Verbose, 0, "ADM {0} closing stream...", ID);
                    _sfc.Close();
                    Tracing?.TraceEvent(TraceEventType.Verbose, 0, "ADM {0} stream closed", ID);
                }
                throw e;
            }
            finally
            {
                Connecting = false;
            }
        }

        public void Disconnect()
        {
            if (Connecting) throw new Exception("ADM is in the process of connecting");
            Disconnecting = true;
            Tracing?.TraceEvent(TraceEventType.Verbose, 0, "ADM {0} Disconnecting...", ID);
            
            if (_sfc.IsOpen)
            {
                _sfc.Close();
            }
            Tracing?.TraceEvent(TraceEventType.Verbose, 0, "ADM {0} Disconnected", ID);
            Disconnecting = false;
        }

        void HandleStreamError(Object sender, StreamFlowController.StreamErrorArgs e)
        {
            StreamFlowController sfc = (StreamFlowController)sender;

            Tracing?.TraceEvent(TraceEventType.Error, 400, "ADM {0} Stream Error: {1} {2}", ID, e.Error, e.Exception == null ? "N/A" : e.Exception.Message);
            switch (e.Error)
            {
                case StreamFlowController.ErrorCode.UNEXPECTED_DISCONNECT:
                    break;

                default:
                    break;
            }
        }

        void HandleStreamCommandByteReceived(Object sender, StreamFlowController.CommandByteArgs e)
        {
            StreamFlowController sfc = (StreamFlowController)sender;
            byte b = e.CommandByte;
            switch (b)
            {
                case (byte)StreamFlowController.Command.RESET:
                    //Console.WriteLine("<<<<< REMOTE COMMAND: Reset");
                    break;

                default:
                    //Console.WriteLine("<<<<< REMOTE COMMAND: {0}", b);
                    break;

            }
        }

        void HandleStreamEventByteReceived(Object sender, StreamFlowController.EventByteArgs e)
        {
            StreamFlowController sfc = (StreamFlowController)sender;
            byte b = e.EventByte;
            //if (!byteEvents.ContainsKey(b)) byteEvents[b] = 0;
            //byteEvents[b]++;
            switch (b)
            {
                case (byte)StreamFlowController.Event.RESET:
                    //Console.WriteLine("<<<<< REMOTE ESP EVENT: Reset");
                    //Tracing?.TraceEvent(TraceEventType.Information, 1000, "{0} REMOTE EVENT: Reset", ID);
                    break;

                case (byte)StreamFlowController.Event.CTS_TIMEOUT:
                    //sfc.SendCommand(StreamFlowController.Command.REQUEST_STATUS);
                    Tracing?.TraceEvent(TraceEventType.Warning, 1000, "{0} REMOTE EVENT: Remote CTS timeout", ID);
                    //log.Add("EVENT: Remote CTS timeout");
                    //sfc.SendCTS(true);
                    break;

                case (byte)StreamFlowController.Event.RECEIVE_BUFFER_FULL:
                    Tracing?.TraceEvent(TraceEventType.Warning, 1000, "{0} REMOTE EVENT: Receive buffer full", ID);
                    //Console.WriteLine("REMOTE ESP EVENT: Receive buffer full argghghgh");
                    break;

                case (byte)StreamFlowController.Event.MAX_DATABLOCK_SIZE_EXCEEDED:
                    Tracing?.TraceEvent(TraceEventType.Warning, 1000, "{0} REMOTE EVENT: Datablock size exceeded", ID);
                    //Console.WriteLine("REMOTE ESP EVENT: Too much data in da block cock");
                    break;

                case (byte)StreamFlowController.Event.CTS_REQUEST_TIMEOUT:
                    Tracing?.TraceEvent(TraceEventType.Warning, 1000, "{0} REMOTE EVENT: The cts request has timed out", ID);
                    break;

                case (byte)StreamFlowController.Event.PING_RECEIVED:
                    //Console.WriteLine("REMOTE EVENT: Ping received! aka PONG");
                    break;


                case 200 + (byte)StreamFlowController.Event.PING_RECEIVED:
                    //Console.WriteLine("REMOTE ARDUINO EVENT: Ping received! aka PONG");
                    break;

                case 200 + (byte)StreamFlowController.Event.RESET:
                    //Console.WriteLine("<<<<< REMOTE ARDUINO EVENT: Reset");
                    break;

                case 200 + (byte)StreamFlowController.Event.SEND_BUFFER_OVERFLOW_ALERT:
                    //Console.WriteLine("REMOTE ARDUINO EVENT: Send buffer overflow alert");
                    break;

                default:
                    //Console.WriteLine("REMOTE {0} EVENT: {1}", b > 200 ? "ARDUINO" : "ESP", b);
                    break;

            }
            //log.Add(String.Format(" event byte: {0}", b));
        }

        void HandleStreamEventByteSent(Object sender, StreamFlowController.EventByteArgs e)
        {
            byte b = e.EventByte;
            switch (b)
            {
                case (byte)StreamFlowController.Event.RESET:
                    //Console.WriteLine(">>>> LOCAL EVENT: {0} ... sending RESET event to remote", b);
                    break;

                case (byte)StreamFlowController.Event.CTS_TIMEOUT:
                    //Console.WriteLine("LOCAL EVENT: {0} ... CTS Timeout event sent to remote", b);
                    //Console.WriteLine("Bytes received/sent {0}/{1}", _sfc.BytesReceived, _sfc.BytesSent);
                    Tracing?.TraceEvent(TraceEventType.Warning, 1000, "{0} LOCAL EVENT: Local CTS timeout", ID);
                    break;

            }
            //log.Add(String.Format(" event byte: {0}", b));
        }


        /// <summary>
        /// Used to update the ADM device state based on the state of all the devices e.g when all are INITIALISED then the ADM Device state is DEVICE_INITIALISED
        /// </summary>
        /// <param name="devState"></param>
        private void updateADMDeviceState(ArduinoDevice.DeviceState devState)
        {
            foreach (var d in _devices.Values)
            {
                if (d.State != devState)
                {
                    return;
                }
            }

            //here all devices are of the same state
            switch (devState)
            {
                case ArduinoDevice.DeviceState.INITIALISED:
                    State = ADMState.DEVICE_INITIALISED;
                    break;

                case ArduinoDevice.DeviceState.CONFIGURING:
                    State = ADMState.DEVICE_CONFIGURING;
                    break;

                case ArduinoDevice.DeviceState.CONFIGURED:
                    State = ADMState.DEVICE_CONFIGURED;
                    break;
            }
        }

        /// <summary>
        /// This method takes stream data from the connected board and converts it in to a message and then routes it to 
        /// the intended target.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void HandleStreamData(Object sender, StreamFlowController.DataBlockArgs e)
        {
            ADMMessage message = null;
            Frame f = new Frame(Frame.FrameSchema.SMALL_SIMPLE_CHECKSUM);
            
            try
            {
                //get the message from stream data
                f.Add(e.DataBlock);
                f.Validate();
                
                message = ADMMessage.Deserialize(f.Payload);
                LastMessageReceivedOn = DateTime.Now;
                MessagesReceived++;

                //Console.WriteLine("{0} Received message {1} for target {2}", UID, message.Type, message.Target);

                //use tag to get current request and release it for future use
                ProcessingRequest = Requests.Release(message.Tag);
                
                //now direct to the target
                if (message.Target == ADM_TARGET_ID)
                {
                    //Board
                    HandleMessage(message);
                }
                else if (message.Target == ADM_STREAM_TARGET_ID)
                {
                    //Stream flow controller
                }
                else if (IsBoardReady)
                {
                    //devices
                    ArduinoDevice dev = GetDevice(message.Target);
                    if(dev == null)
                    {
                        throw new Exception(String.Format("Device {0} not found", message.Target));
                    }
                    //message tags will be released by the device as each device manages its own tags
                    dev.HandleMessage(message);

                    //we work out where we are in terms of device state ... if all are of the same state then this updates
                    //the board state
                    switch (message.Type)
                    {
                        case MessageType.INITIALISE_RESPONSE:
                            Tracing?.TraceEvent(TraceEventType.Verbose, 125003, "Device {0} initialised", dev.UID);
                            updateADMDeviceState(dev.State);
                            break;
                        
                        case MessageType.CONFIGURE_RESPONSE:
                            Tracing?.TraceEvent(TraceEventType.Verbose, 125003, "Device {0} configured", dev.UID);
                            updateADMDeviceState(dev.State);
                            break;

                        case MessageType.STATUS_RESPONSE: //if attachmode is observer
                            updateADMDeviceState(dev.State);
                            break;

                        case MessageType.ERROR:
                            Tracing?.TraceEvent(TraceEventType.Error, 15001, "Device {0} Error: {1} with info: {2}", dev.UID, dev.Error, dev.ErrorInfo);
                            if(State == ADMState.DEVICE_CONFIGURING && dev.ErrorCode == (int)ArduinoDevice.DeviceErrorCode.FAILED_TO_CONFIGURE)
                            {
                                State = ADMState.DEVICE_CONFIGURE_FAILED;
                            }
                            break;
                    }
                } //end target switch
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                Tracing?.TraceEvent(TraceEventType.Error, 500, "ADM {0} Error due to invocation exception: {1}", ID, ex.InnerException.Message);
            }
            catch (Exception ex)
            {
                Tracing?.TraceEvent(TraceEventType.Error, 500, "ADM {0} Error: {1}", ID, ex.Message);
            }
            finally
            {
                ProcessingRequest = null;
            }
        }


        /// <summary>
        /// This is to handle mesages specifically for the ADM (devices and groups handle their messges seperately.
        /// Message targetting is done in HandleStreamData...
        /// </summary>
        /// <param name="message"></param>
        public override void HandleMessage(ADMMessage message)
        {
            //Board
            switch (message.Type)
            {
                case MessageType.ERROR:
                    ADMErrorCode errorCode = (ArduinoDeviceManager.ADMErrorCode)GetMessageValue<int>("ErrorCode", message);
                    SetError("ADMErrorCode: " + errorCode.ToString(), "N/A", (int)errorCode);
                    Tracing?.TraceEvent(TraceEventType.Error, 1500, "ADM {0} Message Type Error, code: {1}", ID, errorCode);
                    //log.Add(String.Format("ERROR: {0}", message.ArgumentAsInt(0)));
                    //Console.WriteLine("---------------------------");
                    break;

                case MessageType.INITIALISE_RESPONSE:
                    //copy some values for compairson
                    AttachmentMode am = AttachMode;
                    AnalogReference ar = AREF;
                    AssignMessageValues(message, "BoardName", "BoardMaxDevices", "AttachMode", "AREF");
                    if (am != AttachMode)
                    {
                        throw new Exception(String.Format("{0} ADM has attachment mode {1} but board attachment mode is {2}", ID, am, AttachMode));
                    }
                    if (ar != AREF)
                    {
                        throw new Exception(String.Format("{0} ADM has AREF {1} but board AREF is {2}", ID, ar, AREF));
                    }

                    State = ADMState.INITIALISED;
                    Tracing?.TraceEvent(TraceEventType.Information, 100, "ADM {0} initialised board {1} with max devices {2}, Attachment mode {3}, AREF {4}", ID, BoardName, BoardMaxDevices, AttachMode, AREF);
                    if (_devices.Count > BoardMaxDevices)
                    {
                        throw new Exception(String.Format("{0} ADM has {1} devices but board {2} only supports {3} devices", ID, _devices.Count, BoardName, BoardMaxDevices));
                    }
                    else
                    {
                        Configure();
                    }
                    break;

                case MessageType.CONFIGURE_RESPONSE:
                    State = ADMState.CONFIGURED;

                    if (AttachMode == AttachmentMode.OBSERVER_OBSERVED)
                    {
                        Tracing?.TraceEvent(TraceEventType.Verbose, 0, "ADM {0} attached as observer so requesting status", ID);
                        RequestStatus(); //this will send a status request message
                    }
                    else if (!IsEmpty)
                    {
                        State = ADMState.DEVICE_INITIALISING; //state will be upodated when all responses are given (see device switch below)
                        foreach (var dev in _devices.Values)
                        {
                            Tracing?.TraceEvent(TraceEventType.Verbose, 0, "ADM {0} Initialising device {1}", ID, dev.ID);
                            dev.Initialise();
                            //Thread.Sleep(500);
                        }
                    }
                    break;

                case MessageType.STATUS_RESPONSE:
                    AssignMessageValues(message, "BoardMillis", "BoardMemory", "BoardInitialised", "BoardConfigured", "BoardLoopDuration", "BoardBytesReceived", "BoardBytesSent");
                    //Console.WriteLine(">>> Memory: {0}", BoardMemory);
                    
                    if (IsDeviceReady)
                    {
                        int n = GetMessageValue<int>("DeviceCount", message);
                        if (n != _devices.Count)
                        {
                            if (Synchronising)
                            {
                                Synchronising = false;
                                Synchronised = false;
                            }
                            throw new Exception(String.Format("Status response returned {0} devices but local has {1} devices", n, _devices.Count));
                        }
                        else
                        {
                            //we use a successful status response to mean that this ADM and the board is synchronised hence setting the flag to true
                            //if a synchronising attempt  hadbeen initiated
                            if (Synchronising)
                            {
                                Synchronised = true;
                            }
                        }
                    }
                    else if (AttachMode == AttachmentMode.OBSERVER_OBSERVED)
                    {
                        int n = GetMessageValue<int>("DeviceCount", message);
                        if (n != _devices.Count)
                        {
                            throw new Exception(String.Format("Status response reurned {0} devices but local has {1} devices", n, _devices.Count));
                        }

                        //We have same number of devices in this ADM as in board so let's get the status of each device as this will set it's state upon return (see ArduinoDevice::HandleMesssage)
                        foreach (var d in _devices.Values)
                        {
                            Tracing?.TraceEvent(TraceEventType.Verbose, 0, "ADM {0} device {1} requesting status", ID, d.ID);
                            d.RequestStatus();
                        }
                    }
                    LastStatusResponseOn = DateTime.Now;
                    break;

                case MessageType.PING_RESPONSE:
                    AssignMessageValues(message, "BoardMillis", "BoardMemory");
                    LastPingResponseOn = DateTime.Now;
                    break;
            }
            base.HandleMessage(message);
        }

        public void PingStream()
        {
            _sfc.Ping();
        }

        public ADMMessage CreateMessage(MessageType type, byte tag = 0, byte sender = ADM_TARGET_ID)
        {
            ADMMessage message = new ADMMessage();
            message.Type = type;
            message.Tag = tag;
            message.Target = ADM_TARGET_ID;
            message.Sender = ADM_TARGET_ID;
            return message;
        }

        /// <summary>
        /// All messages sent to the board should pass through this method (which means messages sent by devices and groups as well)
        /// </summary>
        /// <param name="message"></param>
        public void SendMessage(ADMMessage message)
        {
            if (!_sfc.IsReady && !Connecting)
            {
                throw new Exception("ADM is not able to send messages");
            }

            Frame messageFrame = new Frame(Frame.FrameSchema.SMALL_SIMPLE_CHECKSUM, MessageEncoding.BYTES_ARRAY);
            byte[] payload = message.Serialize();
            if (payload.Length > ADM_MESSAGE_SIZE)
            {
                throw new Exception(String.Format("Message is of length {0} it must be less than {1}", payload.Length, ADM_MESSAGE_SIZE));
            }

            messageFrame.Payload = payload;

            try
            {
                //this is the unique point at which the arduino messaging layer connects with the underlying Stream connection
                _sfc.Send(messageFrame.GetBytes());

                //keep at trackof messages sent
                MessagesSemt++;
            } catch (Exception e)
            {
                //any exception is deemed catastrohic atm requiring a connection reset
                Tracing?.TraceEvent(TraceEventType.Error, 2188, "Exception {0} in ArduinoDeviceManager::SendMessage: {1}", e.GetType().ToString(), e.Message);
                
                //we dissconnect with a task because this method could be called from the stream (_sfc) thread via DataBlocksReceived event handler
                Task.Delay(100).ContinueWith((_) =>
                {
                    Disconnect(); //this will be picked up sync timer
                });

                //throw the exception again
                throw e;
            }
        }

        override protected int GetArgumentIndex(String fieldName, ADMMessage message)
        {
            switch (fieldName)
            {
                case "BoardName":
                    return 0;
                case "BoardMaxDevices":
                    return 1;
                case "BoardMillis":
                    return 0;
                case "BoardMemory":
                    return 1;
                case "BoardInitialised":
                    return 2;
                case "BoardConfigured":
                    return 3;
                case "DeviceCount":
                    return 4;
                case "BoardLoopDuration":
                    return 5;
                case "BoardBytesReceived":
                    return 6;
                case "BoardBytesSent":
                    return 7;
                case "ErrorCode":
                    return 0;
                case "AttachMode":
                    return 2;
                case "AREF":
                    return 3;

                default:
                    throw new ArgumentException(String.Format("unrecognised message field {0}", fieldName));
            }
        }

        public void Initialise(bool allowEmpty = false)
        {
            if (!allowEmpty && IsEmpty) throw new Exception("No devices have been added to the ADM");

            State = ADMState.INITIALISING;
            var message = CreateMessage(MessageType.INITIALISE);
            message.AddArgument((byte)AttachMode);
            message.AddArgument(DeviceCount);
            message.AddArgument((byte)AREF);
            SendMessage(message);
        }

        public void Configure()
        {
            State = ADMState.CONFIGURING;
            var message = CreateMessage(MessageType.CONFIGURE);
            SendMessage(message);
        }

        public ArduinoDevice AddDevice(ArduinoDevice device)
        {
            if (State >= ADMState.INITIALISING)
            {
                throw new Exception(String.Format("Board is in state {0} but devices can only be added prior to initialising", State));
            }
            if (_devices.ContainsKey(device.ID))
            {
                throw new Exception(String.Format("Device {0} already added", device.ID));
            }
            if (_deviceGroups.ContainsKey(device.ID))
            {
                throw new Exception(String.Format("Device {0} cammpt be added as there is already a device group with tha ID", device.ID));
            }

            _devices[device.ID] = device;
            device.ADM = this;
            device.BoardID = (byte)_devices.Count;
            return device;
        }

        public void AddDevices(params ArduinoDevice[] devices)
        {
            AddDevices(devices.ToList());
        }

        public void AddDevices(List<ArduinoDevice> devices)
        {
            foreach (var dev in devices)
            {
                AddDevice(dev);
            }
        }

        public ArduinoDevice GetDevice(String id)
        {
            return _devices.ContainsKey(id) ? _devices[id] : null;
        }

        public ArduinoDevice GetDevice(byte boardID)
        {
            foreach(var d in _devices.Values)
            {
                if (d.BoardID == boardID) return d;
            }

            return null;
        }

        public List<ArduinoDevice> GetDevices()
        {
            return _devices.Values.ToList();
        }

        public ArduinoDeviceGroup AddDeviceGroup(ArduinoDeviceGroup deviceGroup)
        {
            if (State >= ADMState.INITIALISING)
            {
                throw new Exception(String.Format("Board is in state {0} but device groups can only be added prior to initialising", State));
            }
            if (deviceGroup.Devices.Count == 0)
            {
                throw new Exception(String.Format("Device group {0} does not have any devices", deviceGroup.ID));
            }
            if (_deviceGroups.ContainsKey(deviceGroup.ID))
            {
                throw new Exception(String.Format("Device group {0} already added", deviceGroup.ID));
            }
            if (_devices.ContainsKey(deviceGroup.ID))
            {
                throw new Exception(String.Format("Device group {0} cannot be added as there is already a device with tha ID", deviceGroup.ID));
            }

            //add the devices of the group 
            foreach (var dev in deviceGroup.Devices)
            {
                AddDevice(dev);
            }

            //Add the device group and set ADM value
            _deviceGroups[deviceGroup.ID] = deviceGroup;
            deviceGroup.ADM = this;
            return deviceGroup;
        }

        public ArduinoDeviceGroup GetDeviceGroup(String id)
        {
            return _deviceGroups.ContainsKey(id) ? _deviceGroups[id] : null;
        }

        public List<ArduinoDeviceGroup> GetDeviceGroups()
        {
            return _deviceGroups.Values.ToList();
        }

        public void Begin(int timeout, bool allowNoDevices = false)
        {
            State = ADMState.BEGINNING;

            //will close the stream if it's open and set Connected to false
            Disconnect();

            DateTime started = DateTime.Now;
            Connect(timeout);

            State = ADMState.BEGUN;

            Initialise(allowNoDevices);
            
            while (!IsReady)
            {
                wait(100, started, timeout, String.Format("Timed out Waiting for {0} readiness", _devices.Count > 0 ? "Device" : "ADM"));
            }
            long remaining = (DateTime.Now.Ticks - started.Ticks) / TimeSpan.TicksPerMillisecond;
            if(remaining <= 0)
            {
                throw new TimeoutException(String.Format("Timed out Waiting for {0} readiness", _devices.Count > 0 ? "Device" : "ADM"));
            }
            if (!Synchronise((int)System.Math.Max(DEFAULT_SYNCHRONISE_TIMEOUT, remaining)))
            {
                throw new Exception("Failed to synchronise");
            }

            if(_synchroniseTimer == null)
            {
                _synchroniseTimer = new System.Timers.Timer(DEFAULT_SYNCHRONISE_TIMER_INTERVAL);
                _synchroniseTimer.Elapsed += OnSynchroniseTimer;
            }
            _synchroniseTimer.Start();
            Tracing?.TraceEvent(TraceEventType.Information, 0, "Starting sync timer with interval of {0}ms", DEFAULT_SYNCHRONISE_TIMER_INTERVAL);
        }

        public void End(int timeout = -1)
        {
            if (AttachMode != AttachmentMode.OBSERVER_OBSERVED)
            {
                Tracing?.TraceEvent(TraceEventType.Information, 2000, "Ending {0} calling finalise on devices then ADM ...", ID);
                try
                {
                    foreach(var dev in _devices.Values)
                    {
                        try
                        {
                            dev.Finalise();
                        } catch (Exception e)
                        {
                            throw e;
                        }
                    }
                    wait(250);

                    var message = CreateMessage(MessageType.FINALISE);
                    SendMessage(message);
                    wait(100);
                }
                catch (Exception e)
                {
                    Tracing?.TraceEvent(TraceEventType.Error, 2000, "{0} ", ID, e.Message);
                }
            } //end if certain attachment mode

            while (Connecting || Synchronising)
            {
                Tracing?.TraceEvent(TraceEventType.Information, 2000, "{0} Call to end but currently busy (connecting = {1}, synchronising = {2}) so waiting", ID, Connecting, Synchronising);
                wait(1000);
            }
            if (_synchroniseTimer != null)
            {
                _synchroniseTimer.Stop();
            }
            Disconnect();

            State = ADMState.CREATED;
        }

        protected void OnSynchroniseTimer(Object sender, EventArgs eargs)
        {
            if (Connecting || Disconnecting) return;

            _synchroniseTimer.Stop();
            if (!IsConnected)
            {
                Tracing?.TraceEvent(TraceEventType.Information, 501, "OnSynchroniseTimer: ADM {0} Is not connected so attempting to reconnect ...", ID);
                try
                {
                    Disconnect(); //to ensure cleanly disconnected before trying to connect again
                    Connect(_connectTimeout);
                    Tracing?.TraceEvent(TraceEventType.Information, 502, "OnSynchroniseTimer: ADM {0} Connected!", ID);
                    bool initialise = !IsReady;
                    if (IsReady)
                    {
                        //board and devices already configured from previous effort so attempt to synchronise
                        Tracing?.TraceEvent(TraceEventType.Information, 503, "OnSynchroniseTimer: ADM {0} is in Ready state so attempting to Synchronise...!", ID);
                        initialise = !Synchronise();
                        Tracing?.TraceEvent(TraceEventType.Information, 504, "OnSynchroniseTimer: ADM {0} Synchronising {1}!", ID, initialise ? "failed" : "succeeded");
                    }
                    if (initialise)
                    {
                        Tracing?.TraceEvent(TraceEventType.Information, 505, "OnSynchroniseTimer: ADM {0} Initialising...", ID);
                        Initialise(); //this will start init config process
                        Thread.Sleep(500);
                        if (!IsReady) Thread.Sleep(1000);
                    }
                }
                catch (Exception e)
                {
                    Tracing?.TraceEvent(TraceEventType.Error, 500, "OnSynchroniseTimer: ADM {0} Error: {1}", ID, e.Message);
                }
            } 
            else if(LastMessageReceivedOn != default(DateTime) && (DateTime.Now - LastMessageReceivedOn).TotalMilliseconds > InactivityTimeout)
            {
                try
                {
                    Tracing?.TraceEvent(TraceEventType.Information, 511, "OnSynchroniseTimer: ADM {0} inactive for more than {1} ms", ID, DEFAULT_INACTIVITY_TIMEOUT);
                    bool disconnect = false;
                    if(!IsReady || !BoardInitialised || !BoardConfigured)
                    {
                        disconnect = true;
                        Tracing?.TraceEvent(TraceEventType.Information, 512, "OnSynchroniseTimer: ADM {0} is not in a ready state so disconnecting", ID);
                    } else
                    {
                        Tracing?.TraceEvent(TraceEventType.Information, 513, "OnSynchroniseTimer: ADM {0} is in a ready state so attempting to synchronise...", ID);
                        disconnect = !Synchronise();
                        Tracing?.TraceEvent(TraceEventType.Information, 514, "OnSynchroniseTimer: ADM {0} Synchronising {1}!", ID, disconnect ? "failed" : "succeeded");
                    }
                    if (disconnect)
                    {
                        Disconnect();
                    }
                } catch (Exception e)
                {
                    Tracing?.TraceEvent(TraceEventType.Error, 500, "OnSynchroniseTimer: ADM {0} Error: {1}", ID, e.Message);
                }
            }
            _synchroniseTimer.Start();
        }

        public ADMRequestManager.ADMRequest RequestStatus(String requester = null)
        {
            if (!IsBoardReady) throw new Exception("ADM is not ready");
            ADMRequestManager.ADMRequest req = null;
            byte tag = 0;
            if (requester != null)
            {
                req = Requests.AddRequest(requester);
                tag = req.Tag;
            }
            var message = CreateMessage(MessageType.STATUS_REQUEST, tag);
            SendMessage(message);
            return req;
        }

        public ADMRequestManager.ADMRequest Ping(String requester = null)
        {
            if (!IsBoardReady) throw new Exception("ADM is not ready");
            ADMRequestManager.ADMRequest req = null;
            byte tag = 0;
            if (requester != null)
            {
                req = Requests.AddRequest(requester);
                tag = req.Tag;
            }
            var message = CreateMessage(MessageType.PING, tag);
            SendMessage(message);
            return req;
        }

        public bool Synchronise(int timeout = DEFAULT_SYNCHRONISE_TIMEOUT)
        {
            if (!IsReady) throw new InvalidOperationException("Cannot synchronise as ADM is not ready");
            
            //Console.WriteLine("Started synchronising...");
            Synchronising = true;
            try
            {
                RequestStatus();
                DateTime started = DateTime.Now;
                while (Synchronising)
                {
                    wait(100, started, timeout, "Timed out while synchronising");
                }
            } catch (Exception e)
            {
                Synchronised = false;
                SetError(String.Format("Synchronise Exception: {0}"), e.Message);
            }
            Synchronising = false;
            return Synchronised;
        }

    }
}
