using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Chetch.Messaging;

namespace Chetch.Arduino2.Devices.Diagnostics
{
    public class TestBandwidth : ArduinoDevice
    {
        public static String FormatResultsAsString(TestResults results, String lineFeed = "\n")
        {
            String s = "";

            //test info
            s += String.Format("Testing: {0}, Duration: {1} seconds" + lineFeed, results.IsTesting, results.TestDuration.TotalSeconds.ToString("0"));

            //Messages
            s += String.Format("Messages received: {0} @ {1} mps, {2} in total @ {3} mps" + lineFeed, results.MessagesReceived, results.MessagesReceivedRate.ToString("0.0"), results.TotalMessagesReceived, results.TotalMessagesReceivedRate.ToString("0.0"));
            s += String.Format("Messages sent: {0} @ {1} mps, {2} in total @ {3} mps" + lineFeed, results.MessagesSent, results.MessagesSentRate.ToString("0.0"), results.TotalMessagesSent, results.TotalMessagesSentRate.ToString("0.0"));

            //bytes
            s += String.Format("Bytes received: {0} @ {1} bps, {2} in total @ {3} bps" + lineFeed, results.BytesReceived, results.BytesReceivedRate.ToString("0.0"), results.TotalBytesReceived, results.TotalBytesReceivedRate.ToString("0.0"));
            s += String.Format("Bytes sent: {0} @ {1} bps, {2} in total @ {3} bps", results.BytesSent, results.BytesSentRate.ToString("0.0"), results.TotalBytesSent, results.TotalBytesSentRate.ToString("0.0"));

            return s;
        }

        public struct TestResults
        {
            public bool IsTesting;
            public TimeSpan TimeInterval;
            public TimeSpan TestDuration;

            public int MessagesReceived;
            public int MessagesSent;
            public int BytesReceived;
            public int BytesSent;

            public double MessagesReceivedRate;
            public double MessagesSentRate;
            public double BytesReceivedRate;
            public double BytesSentRate;

            public int TotalMessagesReceived;
            public int TotalMessagesSent;
            public int TotalBytesReceived;
            public int TotalBytesSent;

            public double TotalMessagesReceivedRate;
            public double TotalMessagesSentRate;
            public double TotalBytesReceivedRate;
            public double TotalBytesSentRate;

        }

        public class TestEventArgs : EventArgs
        {
            public TestResults Results;
            public TestEventArgs(TestResults results)
            {
                Results = results;
            }
        }

        public event EventHandler<TestEventArgs> TestResultsUpdated;

        private Thread _runTestThread;

        private System.Timers.Timer _testResultsTimer;
        private DateTime _prevResultsTimerOn;

        public bool IsTesting { get; internal set; } = false;

        //duration in seconds
        public int TestDuration { get; internal set; } = -1;

        private int _delayUpper = 0;
        private int _delayLower = 0;

        public DateTime TestStartedOn { get; internal set; }

        private TestResults _results = new TestResults();


        public short ActivityPin { get; internal set; } = -1;

        public int MessagesReceived { get; internal set;  } = 0;
        
        public int MessagesSent { get; internal set; } = 0;
        
        public int BytesReceived { get; internal set; } = 0;
        
        public int BytesSent { get; internal set; } = 0;
        

        public int RemoteMessagesReceived { get; internal set; }
        public int RemoteMessagesSent { get; internal set; }
        public bool RemoteCTS { get; internal set; }
        public int RemoteRBUsed { get; internal set; }
        public int RemoteSBUsed { get; internal set; }

        public int RemoteBytesToRead { get; internal set; }


        public TestBandwidth(String id, short activityPin = -1, String name = "TESTBW") : base(id, name)
        {
            Category = DeviceCategory.DIAGNOSTICS;
            Enabled = true;
            ActivityPin = activityPin;

            AddCommand(ArduinoCommand.DeviceCommand.ANALYSE, ArduinoCommand.ParameterType.STRING);
        }

        protected override void AddConfig(ADMMessage message)
        {
            base.AddConfig(message);

            message.AddArgument(ActivityPin);
        }

        override protected int GetArgumentIndex(String fieldName, ADMMessage message)
        {
            switch (fieldName)
            {
                case "RemoteMessagesReceived":
                    return 2;
                case "RemoteMessagesSent":
                    return 3;
                case "RemoteCTS":
                    return 4;
                case "RemoteRBUsed":
                    return 5;
                case "RemoteSBUsed":
                    return 6;
                case "RemoteBytesToRead":
                    return 7;

                default:
                    return base.GetArgumentIndex(fieldName, message);
            }
        }

        public void Echo(String s)
        {
            var message = CreateMessage(MessageType.ECHO);
            message.AddArgument(s);
            SendMessage(message);
        }

        public void Analyse(String s)
        {
            ExecuteCommand(ArduinoCommand.DeviceCommand.ANALYSE, s);
        }


        public override void SendMessage(ADMMessage message)
        {
            base.SendMessage(message);
            MessagesSent++;
            BytesSent += message.GetByteCount();
        }

        protected override void OnStatusResponse(ADMMessage message)
        {
            base.OnStatusResponse(message);

            AssignMessageValues(message, "RemoteMessagesReceived", "RemoteMessagesSent", "RemoteCTS", "RemoteRBUsed", "RemoteSBUsed", "RemoteBytesToRead");
        }

        override public void HandleMessage(ADMMessage message)
        {
            switch (message.Type)
            {
                case MessageType.DATA:
                    //AssignMessageValues(message, "RemoteMessagesReceived", "RemoteMessagesSent");
                    break;

                case MessageType.ECHO_RESPONSE:
                    break;
            }

            base.HandleMessage(message);

            MessagesReceived++;
            BytesReceived += message.GetByteCount();
        }

        protected override bool HandleCommandResponse(ArduinoCommand.DeviceCommand deviceCommand, ADMMessage message)
        {
            switch (deviceCommand)
            {
                case ArduinoCommand.DeviceCommand.ANALYSE:
                    break;
            }
            return base.HandleCommandResponse(deviceCommand, message);
        }

        private void runTest()
        {
            while (IsTesting)
            {
                if (!IsReady) continue;

                var rnd = new System.Random();
                try
                {
                    RequestStatus();

                    if(_delayUpper > 0)
                    {
                        var delay = rnd.Next(_delayLower, _delayUpper);
                        Thread.Sleep(delay);
                    }
                } catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Thread.Sleep(1000);
                }
            }

            if(TestResultsUpdated != null)
            {
                var eargs = new TestEventArgs(_results);

                TestResultsUpdated(this, eargs);
            }
        }



        protected void OnTestResultsTimer(object sender, EventArgs ea)
        {
            TimeSpan testDuration = DateTime.Now - TestStartedOn;

            _results.IsTesting = IsTesting;
            _results.TimeInterval = DateTime.Now - _prevResultsTimerOn;
            _results.TestDuration = testDuration;
            
            _results.MessagesReceived = MessagesReceived - _results.TotalMessagesReceived;
            _results.MessagesSent = MessagesSent - _results.TotalMessagesSent;
            _results.BytesReceived = BytesReceived - _results.TotalBytesReceived;
            _results.BytesSent = BytesSent - _results.TotalBytesSent;
            _results.MessagesReceivedRate = (double)_results.MessagesReceived / _results.TimeInterval.TotalSeconds;
            _results.MessagesSentRate = (double)_results.MessagesSent / _results.TimeInterval.TotalSeconds;
            _results.BytesReceivedRate = (double)_results.BytesReceived / _results.TimeInterval.TotalSeconds;
            _results.BytesSentRate = (double)_results.BytesSent / _results.TimeInterval.TotalSeconds;

            _results.TotalMessagesReceived = MessagesReceived;
            _results.TotalMessagesSent = MessagesSent;
            _results.TotalBytesReceived = BytesReceived;
            _results.TotalBytesSent = BytesSent;
            _results.TotalMessagesReceivedRate = (double)_results.TotalMessagesReceived / _results.TestDuration.TotalSeconds;
            _results.TotalMessagesSentRate = (double)_results.TotalMessagesSent / _results.TestDuration.TotalSeconds;
            _results.TotalBytesReceivedRate = (double)_results.TotalBytesReceived / _results.TestDuration.TotalSeconds;
            _results.TotalBytesSentRate = (double)_results.TotalBytesSent / _results.TestDuration.TotalSeconds;


            //
            _prevResultsTimerOn = DateTime.Now;
            

            if (IsTesting)
            {
                if(TestDuration > 0 && testDuration.TotalSeconds > TestDuration)
                {
                    StopTest();
                }
            }


            if(TestResultsUpdated != null)
            {
                var eargs = new TestEventArgs(_results);
                TestResultsUpdated(this, eargs);
            }
        }

        public void StartTest(int duration = -1, int delayUpper = 0, int delayLower = 0) 
        {
            StopTest();

            _delayUpper = delayUpper;
            _delayLower = delayLower;

            IsTesting = true;
            TestDuration = duration;
            TestStartedOn = DateTime.Now;

            MessagesReceived = 0;
            _results.MessagesReceived = 0;
            MessagesSent = 0;
            _results.MessagesSent = 0;

            BytesReceived = 0;
            _results.BytesReceived = 0;
            BytesSent = 0;
            _results.BytesSent = 0;


            if (_testResultsTimer == null)
            {
                _testResultsTimer = new System.Timers.Timer();
                _testResultsTimer.Elapsed += OnTestResultsTimer;
                _testResultsTimer.AutoReset = true;
                _testResultsTimer.Interval = 1000;
            }
            else
            {
                _testResultsTimer.Stop();
            }
            _testResultsTimer.Start();
            

            _runTestThread = new Thread(runTest);
            _runTestThread.Name = "TBWRunTest";
            _runTestThread.Start();
        }

        public void StopTest()
        {
            //Console.WriteLine("Stopping test...");
            IsTesting = false;

            if (_runTestThread != null)
            {
                /*while (_runTestThread.IsAlive)
                {
                    Thread.Sleep(100);
                }*/
            }

            //now we have ended
        }
    }
}
