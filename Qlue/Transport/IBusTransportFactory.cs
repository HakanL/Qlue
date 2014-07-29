using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Qlue.Transport;
using Qlue.Logging;

namespace Qlue
{
    public interface IBusTransportFactory
    {
        IBusTransport CreateRequestTopic(string listenTopic, string filterVersion, string subscriptionName);

        IBusTransport CreateResponseTopic(string listenTopic, string responseSessionId, string filterVersion);

        IBusTransport CreateNotifyTopic(string listenTopic, string filterVersion, string subscriptionName);
    }
}
