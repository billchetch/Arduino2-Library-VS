using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Chetch.Utilities;
using Chetch.Utilities.Streams;
using Chetch.Messaging;
using Chetch.Arduino;


namespace Chetch.Arduino2
{
    public class ArduinoDeviceManager
    {
        public const byte ADM_TARGET_ID = 0;
        public const byte ADM_STREAM_TARGET_ID = 255;
        public const int ADM_MESSAGE_SIZE = 50; //in bytes
        public const int DEFAULT_CONNECT_TIMEOUT = 5000;

        public enum ErrorCode
        {
            NO_ERROR = 0,
            NO_ADM_INSTANCE = 1,
            MESSAGE_FRAME_ERROR = 10,
            ADM_MESSAGE_ERROR = 11,
            ADM_MESSAGE_IS_EMPTY= 12,
            NO_DEVICE_ID = 20,
            DEVICE_LIMIT_REACHED = 21,
            DEVICE_ID_ALREADY_USED = 22,
            DEVICE_NOT_FOUND = 23,
            DEVICE_CANNOT_BE_CREATED = 24,
        }

        public enum ADMState
        {
            CREATED = 1,
            INITIALISING,
            INITIALISED,
            CONFIGURING,
            CONFIGURED,
            DEVICE_INITIALISING,
            DEVICE_INITIALISED,
            DEVICE_CONFIGURING,
            DEVICE_CONFIGURED,
        }

        public enum MessageField
        {
            MILLIS = 0,
            MEMORY,
            DEVICE_COUNT,
            IS_READY,
        }

        public class MessageReceivedArgs : EventArgs
        {
            public ADMMessage Message { get; internal set; }

            public MessageReceivedArgs(ADMMessage message)
            {
                Message = message;
            }
        }

        public static ArduinoDeviceManager Create(String portName, int baudRate, int localUartSize, int remoteUartSize, int connectTimeout = DEFAULT_CONNECT_TIMEOUT)
        {
            var ports = SerialPorts.Find(portName);
            var serial = new SerialPortX(ports[0], baudRate);
            var sfc = new StreamFlowController(serial, localUartSize, remoteUartSize);
            var adm = new ArduinoDeviceManager(sfc, connectTimeout);
            return adm;
        }
        public static ArduinoDeviceManager Create(String serviceName, String networkServiceURL, int localUartSize, int remoteUartSize, int connectTimeout = DEFAULT_CONNECT_TIMEOUT)
        {
            var cnn = new ArduinoTCPConnection(serviceName, networkServiceURL);
            var sfc = new StreamFlowController(cnn, localUartSize, remoteUartSize);
            var adm = new ArduinoDeviceManager(sfc, connectTimeout);
            return adm;
        }

        private StreamFlowController _sfc;
        private int _connectTimeout = DEFAULT_CONNECT_TIMEOUT;

        private bool _connected = false;
        private bool _connecting = false;
        public bool Connecting
        {
            get{ return _connecting; }
            internal set
            {
                _connecting = value;
                if (_connecting) _connected = false;
            }
        }
        public bool Connected
        {
            get{ return _connected; }
            internal set
            {
                _connected = value;
                if (_connected) _connecting = false;
            }
        }

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

        private ADMState _state = ADMState.CREATED;
        public ADMState State
        {
            get
            {
                return _state;
            }
            internal set
            {
                //TODO: add an event handler here for external code to monitor ADM state changes
                Console.WriteLine("ADM State = {0}", value);
                _state = value;
            }
        }

        private Dictionary<String, ArduinoDevice> _devices = new Dictionary<string, ArduinoDevice>();

        public bool IsBoardReady => Connected && ((int)State >= (int)ADMState.CONFIGURED);
        public bool IsDeviceReady => Connected && (State == ADMState.DEVICE_CONFIGURED);

        public bool IsReady => IsEmpty ? IsBoardReady : IsDeviceReady;

        public bool IsEmpty => _devices.Count == 0;
        
        public event EventHandler<MessageReceivedArgs> MessageReceived;

        private System.Timers.Timer _synchroniseTimer;

        public ArduinoDeviceManager(StreamFlowController sfc, int connectTimeout)
        {
            _sfc = sfc;
            _sfc.CTSTimeout = 1000; //in ms
            _sfc.StreamError += HandleStreamError;
            _sfc.DataBlockReceived += HandleStreamData;
            _sfc.EventByteReceived += HandleStreamEventByteReceived;
            _sfc.EventByteSent += HandleStreamEventByteSent;
            _connectTimeout = connectTimeout;
        }

        private void wait(int sleep, DateTime started = default(DateTime), int timeout = -1, String timeoutMessage = "Timed out!")
        {
            if (timeout > 0)
            {
                if (Measurement.HasTimedOut(started, timeout))
                {
                    throw new TimeoutException(timeoutMessage);
                }
            }
            Thread.Sleep(sleep);
        }

        public void Connect(int timeout = -1)
        {
            if (Connecting) throw new InvalidOperationException("ADM is in the process of connecting");
            if (Connected) throw new InvalidOperationException("ADM is already connected");
            if (_sfc.IsOpen) throw new InvalidOperationException("Underlying stream is open");
            try
            {
                Connecting = true;
                if (timeout <= 0) timeout = _connectTimeout;
                DateTime started = DateTime.Now;
                do
                {
                    
                    //Some connections e.g. TCPClient need to be made new if connecting to same end point
                    if (_sfc.Stream is ArduinoTCPConnection)
                    {
                        var cnn = (ArduinoTCPConnection)_sfc.Stream;
                        if (cnn.RemoteEndPoint != null)
                        {
                            _sfc.Stream = new ArduinoTCPConnection(cnn.RemoteEndPoint);
                        }
                    }

                    Console.WriteLine("Attempting to open stream...");
                    try
                    {
                        _sfc.Open();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                    if (!_sfc.IsOpen)
                    {
                        wait(500, started, timeout, "Connecting timed out waiting for stream to open");
                    }
                    
                } while (!_sfc.IsOpen);

                Console.WriteLine("Stream opened");

                //we now wait for the stream flow controller to synchronise stream reset
                while (!_sfc.IsReady)
                {
                    Console.WriteLine("Waiting for remote to reset...");
                    wait(500, started, timeout, "Connecting timed out waiting for stream to become ready");
                }

                //by here the stream is open and reset and ready for use
                Console.WriteLine("Stream is Ready!");
                Connected = true;
            }
            catch (Exception e)
            {
                Connected = false;
                Connecting = false;
                throw e;
            }
        }

        public void Disconnect()
        {
            if (Connecting) throw new Exception("ADM is in the process of connecting");
            if (_sfc.IsOpen)
            {
                _sfc.Close();
            }
            Connected = false;
        }

        public void Reconnect()
        {
            Console.WriteLine("Reconnecting...");
            Disconnect();
            wait(100);
            Connect(_connectTimeout);
            wait(100);
            if (!IsReady || !Synchronise())
            {
                Initialise(); //this will start init config process
            }
        }

        void HandleStreamError(Object sender, StreamFlowController.StreamErrorArgs e)
        {
            StreamFlowController sfc = (StreamFlowController)sender;

            Console.WriteLine("Stream error: {0} {1}", e.Error, e.Exception == null ? "N/A" : e.Exception.Message);
            switch (e.Error)
            {
                case StreamFlowController.ErrorCode.UNEXPECTED_DISCONNECT:
                    break;

                default:
                    break;
            }

            Console.WriteLine("ERROR: Stream error: {0} {1}", e.Error, e.Exception == null ? "N / A" : e.Exception.Message);
            if (!sfc.IsOpen && !Connecting) 
            {
                Task.Run(() =>
                {
                    Reconnect();
                });
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
                    Console.WriteLine("<<<<< REMOTE ESP EVENT: Reset");
                    break;

                case (byte)StreamFlowController.Event.CTS_TIMEOUT:
                    //sfc.SendCommand(StreamFlowController.Command.REQUEST_STATUS);
                    Console.WriteLine("REMOTE ESP EVENT: Remote CTS timeout");
                    //log.Add("EVENT: Remote CTS timeout");
                    //sfc.SendCTS(true);
                    break;

                case (byte)StreamFlowController.Event.RECEIVE_BUFFER_FULL:
                    Console.WriteLine("REMOTE ESP EVENT: Receive buffer full argghghgh");
                    break;

                case (byte)StreamFlowController.Event.MAX_DATABLOCK_SIZE_EXCEEDED:
                    Console.WriteLine("REMOTE ESP EVENT: Too much data in da block cock");
                    break;

                case (byte)StreamFlowController.Event.CTS_REQUEST_TIMEOUT:
                    Console.WriteLine("REMOTE ESP EVENT: The cts request has timed out");
                    break;

                case (byte)StreamFlowController.Event.PING_RECEIVED:
                    Console.WriteLine("REMOTE ESP EVENT: Ping received! aka PONG");
                    break;


                case 200 + (byte)StreamFlowController.Event.PING_RECEIVED:
                    Console.WriteLine("REMOTE ARDUINO EVENT: Ping received! aka PONG");
                    break;

                case 200 + (byte)StreamFlowController.Event.RESET:
                    Console.WriteLine("<<<<< REMOTE ARDUINO EVENT: Reset");
                    break;

                case 200 + (byte)StreamFlowController.Event.SEND_BUFFER_OVERFLOW_ALERT:
                    Console.WriteLine("REMOTE ARDUINO EVENT: Send buffer overflow alert");
                    break;

                default:
                    Console.WriteLine("REMOTE {0} EVENT: {1}", b > 200 ? "ARDUINO" : "ESP", b);
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
                    Console.WriteLine(">>>> LOCAL EVENT: {0} ... sending RESET to remote", b);
                    break;

                case (byte)StreamFlowController.Event.CTS_TIMEOUT:
                    Console.WriteLine("LOCAL EVENT: {0} ... CTS Timeout event sent to remote", b);
                    Console.WriteLine("Bytes received/sent {0}/{1}", _sfc.BytesReceived, _sfc.BytesSent);
                    break;

            }
            //log.Add(String.Format(" event byte: {0}", b));
        }

        void HandleStreamData(Object sender, StreamFlowController.DataBlockArgs e)
        {
            ADMMessage message = null;
            Frame f = new Frame(Frame.FrameSchema.SMALL_SIMPLE_CHECKSUM);
            f.Add(e.DataBlock);

            try
            {
                f.Validate();
                //TODO: pointless bytes to string to message conversion ...!!!
                String s = Chetch.Utilities.Convert.ToString(f.Payload);
                message = ADMMessage.Deserialize<ADMMessage>(s, MessageEncoding.BYTES_ARRAY);
                if (message.TargetID == ADM_TARGET_ID)
                {
                    //Board
                    switch (message.Type)
                    {
                        case MessageType.ERROR:
                            Console.WriteLine("---------------------------");
                            Console.WriteLine("ADM ERROR: {0}", message.ArgumentAsInt(0));
                            //log.Add(String.Format("ERROR: {0}", message.ArgumentAsInt(0)));
                            Console.WriteLine("---------------------------");
                            break;

                        case MessageType.INITIALISE_RESPONSE:
                            State = ADMState.INITIALISED;
                            Configure();
                            Console.WriteLine("---------------------------");
                            Console.WriteLine("ADM INIIALISE RESPONSE");
                            Console.WriteLine("---------------------------");
                            break;

                        case MessageType.CONFIGURE_RESPONSE:
                            State = ADMState.CONFIGURED;
                            Console.WriteLine("---------------------------");
                            Console.WriteLine("ADM CONFIGURE RESPONSE");
                            Console.WriteLine("---------------------------");

                            //now configure all device
                            if (!IsEmpty)
                            {
                                State = ADMState.DEVICE_INITIALISING; //state will be upodated when all responses are given (see device switch below)
                                foreach (var dev in _devices.Values)
                                {
                                    Console.WriteLine("Initialising {0}", dev.ID);
                                    ADMMessage m = dev.Initialise();
                                    SendMessage(m);
                                    //Thread.Sleep(500);
                                }
                            }
                            break;

                        case MessageType.STATUS_RESPONSE:
                            if (IsDeviceReady)
                            {
                                int n = message.ArgumentAsInt(GetArgumentIndex(message, MessageField.DEVICE_COUNT));
                                if(n != _devices.Count)
                                {
                                    if (Synchronising)
                                    {
                                        Synchronising = false;
                                        Synchronised = false;
                                    }
                                    throw new Exception(String.Format("Status response reurned {0} devices but local has {1} devices", n, _devices.Count));
                                }
                                else
                                {
                                    if (Synchronising)
                                    {
                                        Synchronised = true;
                                    }
                                }
                            }
                            break;
                    }
                }
                else if (message.TargetID == ADM_STREAM_TARGET_ID)
                {
                    //Stream flow controller
                }
                else if (IsBoardReady)
                {
                    //devices
                    ArduinoDevice dev = GetDevice(message.TargetID);
                    if(dev == null)
                    {
                        throw new Exception(String.Format("Device {0} not found", message.TargetID));
                    }
                    ADMMessage response = dev.HandleMessage(message);

                    //we work out where we are in terms of device state ... if all are of the same state then this updates
                    //the board state
                    switch (message.Type)
                    {
                        case MessageType.INITIALISE_RESPONSE:
                        case MessageType.CONFIGURE_RESPONSE:
                            ArduinoDevice.DeviceState devState = dev.State;
                            bool allOfSameState = true;
                            foreach (var d in _devices.Values)
                            {
                                if (d.State != devState) allOfSameState = false;
                            }
                            if (allOfSameState)
                            {
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
                            break;
                    }
                    
                    if(response != null)
                    {
                        SendMessage(response);
                    }
                } //end target switch
            }
            catch (Exception ex)
            {
                Console.WriteLine("HandleSreamData excepion: {0}", ex.Message);
            }

            if (message != null && IsBoardReady && MessageReceived != null)
            {
                var args = new MessageReceivedArgs(message);
                MessageReceived(this, args);
            }
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
            message.TargetID = ADM_TARGET_ID;
            message.SenderID = ADM_TARGET_ID;
            return message;
        }

        public void SendMessage(ADMMessage message)
        {
            if (!_sfc.IsReady && !Connecting) throw new Exception("ADM is not able to send messages");

            Frame messageFrame = new Frame(Frame.FrameSchema.SMALL_SIMPLE_CHECKSUM, MessageEncoding.BYTES_ARRAY);
            List<byte> bts2send = new List<byte>();
            message.AddBytes(bts2send);
            if (bts2send.Count > ADM_MESSAGE_SIZE)
            {
                throw new Exception(String.Format("Message is of length {0} it must be less than {1}", bts2send.Count, ADM_MESSAGE_SIZE));
            }

            messageFrame.Payload = bts2send.ToArray();
            _sfc.Send(messageFrame.GetBytes());
        }

        public int GetArgumentIndex(ADMMessage message, MessageField field)
        {
            switch (field)
            {
                default:
                    return (int)field;
            }
        }

        public void Initialise(bool allowEmpty = false)
        {
            if (!allowEmpty && IsEmpty) throw new Exception("No devices have been added to the ADM");

            State = ADMState.INITIALISING;
            var message = CreateMessage(MessageType.INITIALISE);
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
                throw new Exception(String.Format("Board is in state {0} but devices can only be added once connected and prior to initialising", State));
            }
            if (_devices.ContainsKey(device.ID))
            {
                throw new Exception(String.Format("Device {0} already added", device.ID));
            }

            _devices[device.ID] = device;
            device.ADM = this;
            device.BoardID = (byte)_devices.Count;
            return device;
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

        public void Begin(int timeout, bool allowNoDevices = false)
        {
            //will close the stream if it's open and set Connected to false
            Disconnect();

            DateTime started = DateTime.Now;
            Connect(timeout);
            
            Initialise(allowNoDevices);
            
            while (!IsReady)
            {
                wait(100, started, timeout, String.Format("Timed out Waiting for {0} readiness", _devices.Count > 0 ? "Device" : "ADM"));
            }

            long remaining = (DateTime.Now.Ticks - started.Ticks) / TimeSpan.TicksPerMillisecond;
            if (!Synchronise((int)remaining))
            {
                throw new Exception("Failed to synchronise");
            }

            if(_synchroniseTimer == null)
            {
                _synchroniseTimer = new System.Timers.Timer(1000);
                _synchroniseTimer.Elapsed += OnSynchroniseTimer;
                _synchroniseTimer.AutoReset = true;
                _synchroniseTimer.Start();
            }
        }

        protected void OnSynchroniseTimer(Object sender, EventArgs e)
        {
            if (IsReady)
            {
                _synchroniseTimer.Stop();
            }
        }

        public byte RequestStatus()
        {
            if (!IsBoardReady) throw new Exception("ADM is not ready");
            var message = CreateMessage(MessageType.STATUS_REQUEST);
            SendMessage(message);
            return message.Tag;
        }

        public bool Synchronise(int timeout = 2000)
        {
            if (!IsReady) throw new Exception("Cannot synchronise as ADM is not ready");
            if (Synchronising) throw new Exception("ADM is in the process of synchronising");

            Console.WriteLine("Stared synchronising...");
            Synchronising = true;
            RequestStatus();
            DateTime started = DateTime.Now;
            try
            {
                while (Synchronising)
                {
                    wait(100, started, timeout, "Timed out while synchronising");
                }
            } catch (TimeoutException)
            {
                Synchronised = false;
            }
            Synchronising = false;
            return Synchronised;
        }
    }
}
