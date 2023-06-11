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

        public class TestEventArgs : EventArgs
        {
            public TestEventArgs()
            {
                
            }
        }

        public event EventHandler<TestEventArgs> TestEnded;

        private Thread _runTestThread;

        private System.Timers.Timer _runTestTimer;

        public bool IsTesting { get; internal set; } = false;

        public short ActivityPin { get; internal set; } = -1;

        public int MessagesReceived { get; internal set;  } = 0;

        public int MessagesSent { get; internal set; } = 0;
       

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
                    return 4;
                case "RemoteMessagesSent":
                    return 5;
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
        }

        protected override void OnStatusResponse(ADMMessage message)
        {
            base.OnStatusResponse(message);

            //AssignMessageValues(message, "RemoteMessagesReceived", "RemoteMessagesSent");
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
            var bytesReceived = ADM.BytesReceived;
            var bytsSent = ADM.BytesSent;


            while (IsTesting)
            {
                RequestStatus();
                Console.WriteLine("Testing...");
                //Thread.Sleep(1000);
            }


            Console.WriteLine("Test ended...");

            if(TestEnded != null)
            {
                var eargs = new TestEventArgs();

                TestEnded(this, eargs);
            }
        }

        protected void OnRunTestTimer(object sender, EventArgs ea)
        {
            StopTest();
        }

        public void StartTest(int duration = -1)
        {
            StopTest();

            IsTesting = true;
            MessagesReceived = 0;
            MessagesSent = 0;

            if (duration > 0)
            {
                if (_runTestTimer == null)
                {
                    _runTestTimer = new System.Timers.Timer();
                    _runTestTimer.Elapsed += OnRunTestTimer;
                    _runTestTimer.AutoReset = false;
                }
                else
                {
                    _runTestTimer.Stop();
                }
                _runTestTimer.Interval = duration;
                _runTestTimer.Start();
            } else if (_runTestTimer != null)
            {
                _runTestTimer.Stop();
            }
            

            _runTestThread = new Thread(runTest);
            _runTestThread.Name = "TBWRunTest";
            _runTestThread.Start();
        }

        public void StopTest()
        {
            Console.WriteLine("Stopping test...");
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
