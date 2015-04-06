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
        private AzureBusSettings settings;

        public AzureBusTransportFactory(ICloudCredentials cloudCredentials, ILogFactory logFactory, AzureBusSettings settings = null)
        {
            this.cloudCredentials = cloudCredentials;
            this.log = logFactory.GetLogger("Qlue");

            if (settings == null)
                this.settings = new AzureBusSettings
                {
                    AutoDeleteOnIdle = TimeSpan.FromDays(2),
                    Express = true,
                    UseAmqp = false,
                    Partitioning = false
                };
            else
                this.settings = settings;
        }

        public IBusTransport CreateRequestTopic(string listenTopic, string filterVersion, string subscriptionName)
        {
            return AzureBusTransport.CreateRequestTopic(this.log, this.cloudCredentials, listenTopic, filterVersion, subscriptionName, this.settings);
        }

        public IBusTransport CreateResponseTopic(string listenTopic, string responseSessionId, string filterVersion)
        {
            return AzureBusTransport.CreateResponseTopic(this.log, this.cloudCredentials, listenTopic, responseSessionId, filterVersion, this.settings);
        }

        public IBusTransport CreateNotifyTopic(string listenTopic, string filterVersion, string subscriptionName)
        {
            return AzureBusTransport.CreateNotifyTopic(this.log, this.cloudCredentials, listenTopic, filterVersion, subscriptionName, this.settings);
        }
    }
}
