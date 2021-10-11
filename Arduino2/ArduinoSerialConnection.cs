using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Utilities.Streams;

namespace Chetch.Arduino2
{
    public class ArduinoSerialConnection : SerialPortX
    {
        public const String BOARD_CH340 = "CH340";
        public const String BOARD_UNO = "Arduino Uno";
        public const String BOARD_MEGA = "Arduino Mega";

        public ArduinoSerialConnection(String portName, int baudRate) : base(portName, baudRate)
        {
            //empty
        }
    }
}
