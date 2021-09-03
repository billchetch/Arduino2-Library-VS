using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.RestAPI.Network;
using System.Net;
using Chetch.Utilities.Streams;

namespace Arduino2
{
    public class ArduinoTCPConnection : TCPClientStream
    {

        public String NetworkHostName { get; set; } //host name on network of arduino board you are trying to connect to
        public String NetworkServiceURL { get; set; } //where to find netowrk service data (including data for hostname) ... if NULL then local host will be used


        public ArduinoTCPConnection(String networkHostName, String networkServiceURL) : base(null) //null networkServiceURL resuls in localhost
        {
            NetworkHostName = networkHostName;
            NetworkServiceURL = networkServiceURL;
        }

        public ArduinoTCPConnection(IPEndPoint remoteEndPoint) : base(remoteEndPoint) { }

        override protected IPEndPoint GetEndPoint()
        {
            if (RemoteEndPoint == null)
            {
                NetworkAPI.Service service = NetworkAPI.GetAPIService(NetworkHostName, NetworkServiceURL);
                if (service == null) throw new Exception("Cannot find service " + NetworkHostName);

                IPAddress ip = IPAddress.Parse(service.Domain);
                int port = service.Port;

                RemoteEndPoint = new IPEndPoint(ip, port);
            }
            return base.GetEndPoint();
        }
    }

}
