using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.RestAPI.Network;
using System.Net;
using Chetch.Utilities.Streams;

namespace Chetch.Arduino2
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

        public ArduinoTCPConnection(ArduinoTCPConnection cnn) : base(null)
        {
            NetworkHostName = cnn.NetworkHostName;
            NetworkServiceURL = cnn.NetworkServiceURL;
            RemoteEndPoint = cnn.RemoteEndPoint;
        }

        override protected IPEndPoint GetEndPoint()
        {
            if (RemoteEndPoint == null)
            {
                NetworkAPI.SetHTTPTimeout(5000);
                NetworkAPI.Service service = NetworkAPI.GetAPIService(NetworkHostName, NetworkServiceURL);
                if (service == null) throw new Exception("Cannot find service " + NetworkHostName);

                IPAddress ip = IPAddress.Parse(service.Domain);
                int port = service.Port;

                RemoteEndPoint = new IPEndPoint(ip, port);
            }
            return base.GetEndPoint();
        }

        protected override void ConnectToEndPoint()
        {
            try
            {
                base.ConnectToEndPoint();
            } catch (Exception e)
            {
                RemoteEndPoint = null;
                throw e;
            }
        }
    }

}
