using System;

namespace Qlue
{
    public interface IChannelFactory
    {
        IRequestChannel CreateRequestChannel(string listenTopic, string destinationTopic, int outboundConnections = 1);

        IServiceChannel CreateServiceChannel(string listenTopic, string subscriptionName = null);

        INotifyChannel CreateNotifyChannel(string listenTopic, IServiceChannel serviceChannel = null, string subscriptionName = null);
    }
}
