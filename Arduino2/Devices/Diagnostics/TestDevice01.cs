using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Messaging;

namespace Chetch.Arduino2.Devices.Diagnostics
{
    public class TestDevice01 : ArduinoDevice
    {
        List<int> valueHistory = new List<int>();
        List<int> timestampHistory = new List<int>();

        [ArduinoProperty(ArduinoPropertyAttribute.DATA, 0)]
        public int TestValue
        {
            get { return Get<int>(); }
            internal set { Set(value, IsReady); }
        }

        public int PrevTestValue { get; internal set; } = -1;


        public TestDevice01(String id, String name = "TEST01") : base(id, name)
        {
            Category = DeviceCategory.DIAGNOSTICS;
            Enabled = true;

            var enable = ArduinoCommand.Enable(true);
            var disable = ArduinoCommand.Enable(false);
            var d1 = ArduinoCommand.Delay(1000);
            var d5 = ArduinoCommand.Delay(5000);
            var cmd = AddCommand(ArduinoCommand.DeviceCommand.COMPOUND, "test");
            cmd.Repeat = 4;
            cmd.AddCommands(enable, d5, disable, d1, enable, d1, disable);
        }

        override protected int GetArgumentIndex(String fieldName, ADMMessage message)
        {
            switch (fieldName)
            {
                case "TestValue":
                    return 0;

                default:
                    return base.GetArgumentIndex(fieldName, message);
            }
        }

        override public void HandleMessage(ADMMessage message)
        {
            switch (message.Type)
            {
                case MessageType.DATA:
                    PrevTestValue = TestValue;
                    //valueHistory.Add(message.GetArgument<int>(0));
                    //timestampHistory.Add(message.GetArgument<int>(1));
                    AssignMessageValues(message, "TestValue");
                    break;
            }

            base.HandleMessage(message);
        }
    }
}
