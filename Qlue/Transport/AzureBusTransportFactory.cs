using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Qlue.Logging;

namespace Qlue.Transport
{
    public class AzureBusTransportFactory : IBusTransportFactory
    {
        private ICloudCredentials cloudCredentials;
        private ILog log;

        public AzureBusTransportFactory(ICloudCredentials cloudCredentials, ILogFactory logFactory)
        {
            this.cloudCredentials = cloudCredentials;
            this.log = logFactory.GetLogger("Qlue");
        }

        public IBusTransport CreateRequestTopic(string listenTopic, string filterVersion, string subscriptionName)
        {
            return AzureBusTransport.CreateRequestTopic(this.log, this.cloudCredentials, listenTopic, filterVersion, subscriptionName);
        }

        public IBusTransport CreateResponseTopic(string listenTopic, string responseSessionId, string filterVersion)
        {
            return AzureBusTransport.CreateResponseTopic(this.log, this.cloudCredentials, listenTopic, responseSessionId, filterVersion);
        }

        public IBusTransport CreateNotifyTopic(string listenTopic, string filterVersion, string subscriptionName)
        {
            return AzureBusTransport.CreateNotifyTopic(this.log, this.cloudCredentials, listenTopic, filterVersion, subscriptionName);
        }
    }
}
