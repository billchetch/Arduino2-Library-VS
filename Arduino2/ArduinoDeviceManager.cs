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

        public static ArduinoDeviceManager Connect(String portName, int baudRate, int localUartSize, int remoteUartSize)
        {
            var ports = SerialPorts.Find(portName);
            var serial = new SerialPortX(ports[0], baudRate);
            var sfc = new StreamFlowController(serial, localUartSize, remoteUartSize);
            var adm = new ArduinoDeviceManager(sfc);
            adm.Connect();
            return adm;
        }
        public static ArduinoDeviceManager Connect(String serviceName, String networkServiceURL, int localUartSize, int remoteUartSize)
        {
            var cnn = new ArduinoTCPConnection(serviceName, networkServiceURL);
            var sfc = new StreamFlowController(cnn, localUartSize, remoteUartSize);
            var adm = new ArduinoDeviceManager(sfc);
            adm.Connect();
            return adm;
        }

        private StreamFlowController _sfc;
        private bool _connecting = false;

        public bool IsReady => _sfc.IsReady && !IsConnecting && _initialised && _configured;
        public bool IsConnecting => _connecting;

        public bool _initialised = false;
        public bool _configured = false;

        private Dictionary<String, ArduinoDevice> _devices = new Dictionary<string, ArduinoDevice>();

        public bool IsDeviceReady 
        { 
            get
            {
                foreach(var d in _devices.Values)
                {
                    if (!d.IsReady) return false;
                }
                return _devices.Count > 0;
            } 
        }

        public event EventHandler<MessageReceivedArgs> MessageReceived;

        public ArduinoDeviceManager(StreamFlowController sfc)
        {
            _sfc = sfc;
            _sfc.CTSTimeout = 1000; //in ms
            _sfc.StreamError += HandleStreamError;
            _sfc.DataBlockReceived += HandleStreamData;
            _sfc.EventByteReceived += HandleStreamEventByteReceived;
            _sfc.EventByteSent += HandleStreamEventByteSent;
        }

        public void Connect()
        {
            if (IsConnecting) throw new Exception("ADM is in the process of connecting");
            try
            {
                _connecting = true;
                do
                {
                    Console.WriteLine("Attempting to open stream...");
                    if (_sfc.Stream is ArduinoTCPConnection)
                    {
                        var cnn = (ArduinoTCPConnection)_sfc.Stream;
                        if (cnn.RemoteEndPoint != null)
                        {
                            _sfc.Stream = new ArduinoTCPConnection(cnn.RemoteEndPoint);
                        }
                    }
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
                        Thread.Sleep(500);
                    }
                } while (!_sfc.IsOpen);

                Console.WriteLine("Stream opened");

                //we now wait for the stream flow controller to synchronise stream reset
                while (!_sfc.IsReady)
                {
                    Console.WriteLine("Waiting for remote to reset...");
                    Thread.Sleep(500);
                }

                //by here the stream is open and reset and ready for use
                Console.WriteLine("Stream is Ready!");
                _connecting = false;
            }
            catch (Exception e)
            {
                _connecting = false;
                throw e;
            }
        }

        public void Disconnect()
        {
            if (IsConnecting) throw new Exception("ADM is in the process of connecting");
            if (_sfc.IsOpen)
            {
                _sfc.Close();
            }
        }

        public void Reconnect()
        {
            if (!_initialised) throw new Exception("Cannot reconnection if the ADM has never been initialised");
            Disconnect();
            Thread.Sleep(100);
            Connect();
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
            if (!sfc.IsOpen && !IsConnecting)
            {
                Reconnect();
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
                    Console.WriteLine("REMOTE ESP EVENT: Reset");
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
                    Console.WriteLine("REMOTE ARDUINO EVENT: Reset");
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
                case (byte)StreamFlowController.Event.CTS_TIMEOUT:
                    Console.WriteLine("LOCAL EVENT: {0} ... CTS Timeout event sent to remote", b);
                    Console.WriteLine("Bytes received/sent {0}/{1}", _sfc.BytesReceived, _sfc.BytesSent);
                    break;

            }
            //log.Add(String.Format(" event byte: {0}", b));
        }

        void HandleStreamData(Object sender, EventArgs e)
        {
            ADMMessage message = null;
            Frame f = new Frame(Frame.FrameSchema.SMALL_SIMPLE_CHECKSUM);
            f.Add(_sfc.ReceiveBuffer);

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
                            _initialised = true;
                            Configure();
                            Console.WriteLine("---------------------------");
                            Console.WriteLine("ADM INIIALISE RESPONSE");
                            Console.WriteLine("---------------------------");
                            break;

                        case MessageType.CONFIGURE_RESPONSE:
                            _configured = true;
                            if (!IsReady) throw new Exception("Configure reponse received but board not ready!");

                            Console.WriteLine("---------------------------");
                            Console.WriteLine("ADM CONFIGURE RESPONSE");
                            Console.WriteLine("---------------------------");

                            //now configure all devices
                            foreach(var dev in _devices.Values)
                            {
                                Console.WriteLine("Initialising {0}", dev.ID);
                                ADMMessage m = dev.Initialise();
                                SendMessage(m);
                            }
                            break;

                        case MessageType.STATUS_RESPONSE:
                            if (IsDeviceReady)
                            {
                                int n = message.ArgumentAsInt(GetArgumentIndex(message, MessageField.DEVICE_COUNT));
                                if(n != _devices.Count)
                                {
                                    throw new Exception(String.Format("Number of devices local is {0} but remote is {1}", _devices.Count, n));
                                }

                                foreach(var dev in _devices.Values)
                                {
                                    ADMMessage m = dev.CreateMessage(MessageType.STATUS_REQUEST);
                                    SendMessage(m);
                                }
                            }
                            break;
                    }


                }
                else if (message.TargetID == ADM_STREAM_TARGET_ID)
                {
                    //Stream flow controller
                }
                else if (IsReady)
                {
                    //devices
                    var dev = GetDevice(message.TargetID);
                    if(dev == null)
                    {
                        throw new Exception(String.Format("Device {0} not found", message.TargetID));
                    }
                    ADMMessage response = dev.HandleMessage(message);
                    if(response != null)
                    {
                        SendMessage(response);
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Fuck it errored: {0}", ex.Message);

                //TODO: create error message
                //log.Add(String.Format("Fuck it errored: {0}", ex.Message));
            }

            if (message != null && IsReady && MessageReceived != null)
            {
                var args = new MessageReceivedArgs(message);
                MessageReceived(this, args);
            }
            //Console.WriteLine("---- Message {0} received of length {1} bytes: {2} --------------------------", messageReceivedCount, serial.ReceiveBuffer.Count, s);
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
            if (!_sfc.IsReady && !IsConnecting) throw new Exception("ADM is not able to send messages");

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

        public void Initialise(bool allowNoDevices = false)
        {
            if (!allowNoDevices && _devices.Count == 0) throw new Exception("No devices have been added to the ADM");

            _initialised = false;
            var message = CreateMessage(MessageType.INITIALISE);
            SendMessage(message);
        }

        public void Configure()
        {
            _configured = false;
            var message = CreateMessage(MessageType.CONFIGURE);
            SendMessage(message);
        }

        public ArduinoDevice AddDevice(ArduinoDevice device)
        {
            if (_initialised) throw new Exception("Cannot add device as board already initialised.  All devices must be added PRIOR to calling Initialise");
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

        public byte RequestStatus()
        {
            if (!IsReady) throw new Exception("ADM is not ready");
            var message = CreateMessage(MessageType.STATUS_REQUEST);
            SendMessage(message);
            return message.Tag;
        }
    }
}
