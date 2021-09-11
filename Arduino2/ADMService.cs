using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Messaging;
using Chetch.Services;

namespace Chetch.Arduino2
{
    abstract public class ADMService : TCPMessagingClient
    {
        public ADMService(String clientName, String clientManagerSource, String serviceSource, String eventLog) : base(clientName, clientManagerSource, serviceSource, eventLog)
        {
            //empty
        }
        public override void HandleClientError(Connection cnn, Exception e)
        {
            throw new NotImplementedException();
        }

        public override bool HandleCommand(Connection cnn, Message message, string command, List<object> args, Message response)
        {
            throw new NotImplementedException();
        }
    }
}
