#define TRANSPORT_USENETMESSAGING

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Qlue.Logging;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Qlue.Transport;
using System.Globalization;

namespace Qlue
{
    public class AzureBusTransport : IBusTransport
    {
        private ILog log;
        private SubscriptionClient listenClient;
        private NamespaceManager namespaceManager;
        private string listenTopic;
        private string subscriptionNameToDelete;
        private ICloudCredentials cloudCredentials;
        private string topicSuffix;

        private AzureBusTransport(
            ILog log,
            ICloudCredentials cloudCredentials,
            string listenTopic,
            string filterVersion,
            string subscriptionName,
            bool deleteSubscriptionOnDispose,
            string subscriptionFilter)
        {
            this.log = log;
            this.cloudCredentials = cloudCredentials;

            if (!string.IsNullOrEmpty(filterVersion))
            {
                // Append the filter version to the subscription name so they are unique per version
                this.topicSuffix = "-" + filterVersion;
            }

            this.listenTopic = listenTopic + this.topicSuffix;

            this.log.Info("Create transport for topic {0}, subscription {1} and filter {2}",
                this.listenTopic, subscriptionName, subscriptionFilter);

            if (deleteSubscriptionOnDispose)
                this.subscriptionNameToDelete = subscriptionName;

            this.namespaceManager = NamespaceManager.CreateFromConnectionString(
                this.cloudCredentials.GetServiceBusConnectionString());

            if (!this.namespaceManager.TopicExists(this.listenTopic))
            {
                var topicDescription = new TopicDescription(this.listenTopic)
                {
                    SupportOrdering = false,
                    RequiresDuplicateDetection = true,
                    DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(10),
                    EnableBatchedOperations = false,
                    AutoDeleteOnIdle = TimeSpan.FromDays(3)
                };
                this.namespaceManager.CreateTopic(topicDescription);
            }

            if (!this.namespaceManager.SubscriptionExists(this.listenTopic, subscriptionName))
            {
                var subscriptionDescription = new SubscriptionDescription(this.listenTopic, subscriptionName)
                {
                    EnableBatchedOperations = false,
                    EnableDeadLetteringOnFilterEvaluationExceptions = true,
                    DefaultMessageTimeToLive = TimeSpan.FromDays(7),
                    AutoDeleteOnIdle = TimeSpan.FromDays(3)
                };

                this.namespaceManager.CreateSubscription(subscriptionDescription, new SqlFilter(subscriptionFilter));
            }

            this.listenClient = SubscriptionClient.CreateFromConnectionString(
                this.cloudCredentials.GetServiceBusConnectionString(),
                this.listenTopic,
                subscriptionName,
                ReceiveMode.ReceiveAndDelete);
        }

        private static Pipeline.PipelineContext GetContextFromBrokeredMessage(BrokeredMessage brokMsg)
        {
            var properties = new Dictionary<string, string>();
            foreach (var kvp in brokMsg.Properties)
                properties.Add(kvp.Key, kvp.Value.ToString());

            string customSessionId;
            properties.TryGetValue("CustomSessionId", out customSessionId);
            string version;
            properties.TryGetValue("Version", out version);

#if FULL_LOGGING
            if (!string.IsNullOrEmpty(version))
                this.log.Debug("Incoming message {1} from version {0}", version, brokMsg.MessageId);
#endif

            var context = Pipeline.PipelineContext.CreateFromInboundMessage(
                brokMsg.GetBody<Stream>(),
                brokMsg.ContentType,
                brokMsg.MessageId,
                brokMsg.ReplyTo,
                brokMsg.CorrelationId,
                brokMsg.SessionId,
                brokMsg,
                customSessionId,
                version,
                properties);

            return context;
        }

        public string TopicSuffix
        {
            get { return this.topicSuffix; }
        }

        public void DeleteTopic(string topicName)
        {
            if (this.namespaceManager.TopicExists(topicName))
                this.namespaceManager.DeleteTopic(topicName);
        }

        public static AzureBusTransport CreateRequestTopic(
            ILog log,
            ICloudCredentials cloudCredentials,
            string listenTopic,
            string filterVersion,
            string subscriptionName)
        {
            var busTransport = new AzureBusTransport(
                log,
                cloudCredentials,
                listenTopic,
                filterVersion,
                subscriptionName,
                false,
                "sys.Label = 'Request'");

            return busTransport;
        }

        public static AzureBusTransport CreateResponseTopic(
            ILog log,
            ICloudCredentials cloudCredentials,
            string listenTopic,
            string responseSessionId,
            string filterVersion)
        {
            string subscriptionName = string.Format(CultureInfo.InvariantCulture, "client-{0}", responseSessionId);

            var busTransport = new AzureBusTransport(
                log,
                cloudCredentials,
                listenTopic,
                filterVersion,
                subscriptionName,
                true,
                string.Format(CultureInfo.InvariantCulture, "sys.Label = 'Response-{0}'", responseSessionId));

            return busTransport;
        }

        public static AzureBusTransport CreateNotifyTopic(
            ILog log,
            ICloudCredentials cloudCredentials,
            string listenTopic,
            string filterVersion,
            string subscriptionName)
        {
            bool deleteSubscriptionOnDispose = false;
            if (subscriptionName == null)
            {
                subscriptionName = string.Format(CultureInfo.InvariantCulture, "{0}-{1:n}", Environment.MachineName, Guid.NewGuid());

                deleteSubscriptionOnDispose = true;
            }

            var busTransport = new AzureBusTransport(
                log,
                cloudCredentials,
                listenTopic,
                filterVersion,
                subscriptionName,
                deleteSubscriptionOnDispose,
                "sys.Label = 'Notify'");

            return busTransport;
        }

        public bool IsClosed
        {
            get { return this.listenClient.IsClosed; }
        }

        public void Complete(Pipeline.PipelineContext context)
        {
            var brokMsg = (BrokeredMessage)context.BusObject;
            if (this.listenClient.Mode == ReceiveMode.PeekLock)
                brokMsg.Complete();
        }

        public void Abandon(Pipeline.PipelineContext context)
        {
            var brokMsg = (BrokeredMessage)context.BusObject;
            if (this.listenClient.Mode == ReceiveMode.PeekLock)
                brokMsg.Abandon();
        }

        public void Close()
        {
            this.listenClient.Abort();
            try
            {
                this.listenClient.Close();
            }
            catch (Exception)
            {
                // Ignore any exception
            }

            if (!string.IsNullOrEmpty(this.subscriptionNameToDelete))
            {
                this.log.Trace("Cleanup subscription {0}", this.subscriptionNameToDelete);

                this.namespaceManager.DeleteSubscription(this.listenTopic, this.subscriptionNameToDelete);

                this.subscriptionNameToDelete = null;
            }
        }

        public IAsyncResult BeginReceive(AsyncCallback callback, object state)
        {
            return this.listenClient.BeginReceive(callback, state);
        }

        public Pipeline.PipelineContext EndReceive(IAsyncResult result)
        {
            var brokMsg = this.listenClient.EndReceive(result);
            if (brokMsg == null)
                return null;

            return GetContextFromBrokeredMessage(brokMsg);
        }

        private static TokenProvider GetTokenProvider(ICloudCredentials cloudCredentials)
        {
            return TokenProvider.CreateSharedSecretTokenProvider(
                cloudCredentials.IssuerName,
                cloudCredentials.IssuerSecret);
        }

        public IBusSender CreateBusSender(string destinationTopic, string sessionId)
        {
#if !TRANSPORT_USENETMESSAGING
            var settings = new MessagingFactorySettings
            {
                AmqpTransportSettings = new Microsoft.ServiceBus.Messaging.Amqp.AmqpTransportSettings
                {
                    BatchFlushInterval = TimeSpan.Zero
                },
                TokenProvider = GetTokenProvider(this.cloudCredentials),
                TransportType = TransportType.Amqp
            };
#else
            var settings = new MessagingFactorySettings
            {
                NetMessagingTransportSettings = new Microsoft.ServiceBus.Messaging.NetMessagingTransportSettings
                {
                    BatchFlushInterval = TimeSpan.Zero
                },
                TokenProvider = GetTokenProvider(this.cloudCredentials),
                TransportType = TransportType.NetMessaging
            };
#endif

            var sbUri = ServiceBusEnvironment.CreateServiceUri("sb", this.cloudCredentials.ServiceNamespace, "");
            var factory = MessagingFactory.Create(sbUri, settings);

            var topicClient = factory.CreateTopicClient(destinationTopic);

            return new AzureBusSender(this.log, topicClient, destinationTopic, sessionId);
        }
    }
}
