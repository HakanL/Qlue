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
        private AzureBusSettings settings;

        private AzureBusTransport(
            ILog log,
            ICloudCredentials cloudCredentials,
            string listenTopic,
            string filterVersion,
            string subscriptionName,
            bool deleteSubscriptionOnDispose,
            string subscriptionFilter,
            AzureBusSettings settings)
        {
            this.log = log;
            this.cloudCredentials = cloudCredentials;
            this.settings = settings;

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
                    EnableBatchedOperations = false,
                    AutoDeleteOnIdle = settings.AutoDeleteOnIdle,
                    // Limit to a total of 100 topics with partitioning enabled
                    EnablePartitioning = settings.Partitioning
                };

                if (settings.Express)
                {
                    topicDescription.EnableExpress = true;
                    topicDescription.RequiresDuplicateDetection = false;
                }
                else
                {
                    topicDescription.EnableExpress = false;
                    topicDescription.RequiresDuplicateDetection = true;
                    topicDescription.DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(10);
                }

                this.namespaceManager.CreateTopic(topicDescription);
            }

            if (!this.namespaceManager.SubscriptionExists(this.listenTopic, subscriptionName))
            {
                var subscriptionDescription = new SubscriptionDescription(this.listenTopic, subscriptionName)
                {
                    EnableBatchedOperations = false,
                    EnableDeadLetteringOnFilterEvaluationExceptions = true,
                    DefaultMessageTimeToLive = TimeSpan.FromDays(7),
                    AutoDeleteOnIdle = TimeSpan.FromDays(3),
                    RequiresSession = false
                };

                this.namespaceManager.CreateSubscription(subscriptionDescription, new SqlFilter(subscriptionFilter));
            }

            this.listenClient = SubscriptionClient.CreateFromConnectionString(
                this.cloudCredentials.GetServiceBusConnectionString(),
                this.listenTopic,
                subscriptionName,
                ReceiveMode.ReceiveAndDelete);
        }

        private Pipeline.PipelineContext GetContextFromBrokeredMessage(BrokeredMessage brokMsg)
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

            TimeSpan age = DateTime.UtcNow - brokMsg.EnqueuedTimeUtc;
            this.log.Trace("Incoming message id {0} enqueued at {1:s}, age {2:N1} ms",
                brokMsg.MessageId, brokMsg.EnqueuedTimeUtc, age.TotalMilliseconds);

            var context = Pipeline.PipelineContext.CreateFromInboundMessage(
                payload: brokMsg.GetBody<Stream>(),
                contentType: brokMsg.ContentType,
                messageId: brokMsg.MessageId,
                from: brokMsg.ReplyTo,
                relatesTo: brokMsg.CorrelationId,
                sessionId: brokMsg.SessionId,
                busObject: brokMsg,
                customSessionId: customSessionId,
                version: version,
                enqueuedTimeUtc: brokMsg.EnqueuedTimeUtc,
                properties: properties);

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
            string subscriptionName,
            AzureBusSettings settings)
        {
            var busTransport = new AzureBusTransport(
                log,
                cloudCredentials,
                listenTopic,
                filterVersion,
                subscriptionName,
                false,
                "sys.Label = 'Request'",
                settings);

            return busTransport;
        }

        public static AzureBusTransport CreateResponseTopic(
            ILog log,
            ICloudCredentials cloudCredentials,
            string listenTopic,
            string responseSessionId,
            string filterVersion,
            AzureBusSettings settings)
        {
            string subscriptionName = string.Format(CultureInfo.InvariantCulture, "client-{0}", responseSessionId);

            var busTransport = new AzureBusTransport(
                log,
                cloudCredentials,
                listenTopic,
                filterVersion,
                subscriptionName,
                true,
                string.Format(CultureInfo.InvariantCulture, "sys.Label = 'Response-{0}'", responseSessionId),
                settings);

            return busTransport;
        }

        public static AzureBusTransport CreateNotifyTopic(
            ILog log,
            ICloudCredentials cloudCredentials,
            string listenTopic,
            string filterVersion,
            string subscriptionName,
            AzureBusSettings settings)
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
                "sys.Label = 'Notify'",
                settings);

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
            return this.listenClient.ReceiveAsync().AsApm(callback, state);
        }

        public Pipeline.PipelineContext EndReceive(IAsyncResult result)
        {
            var brokMsg = ((Task<BrokeredMessage>)result).Result;
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
            MessagingFactorySettings factorySettings;

            if (this.settings.UseAmqp)
            {
                factorySettings = new MessagingFactorySettings
                {
                    AmqpTransportSettings = new Microsoft.ServiceBus.Messaging.Amqp.AmqpTransportSettings
                    {
                        BatchFlushInterval = TimeSpan.Zero
                    },
                    TokenProvider = GetTokenProvider(this.cloudCredentials),
                    TransportType = TransportType.Amqp
                };
            }
            else
            {
                factorySettings = new MessagingFactorySettings
                {
                    NetMessagingTransportSettings = new Microsoft.ServiceBus.Messaging.NetMessagingTransportSettings
                    {
                        BatchFlushInterval = TimeSpan.Zero
                    },
                    TokenProvider = GetTokenProvider(this.cloudCredentials),
                    TransportType = TransportType.NetMessaging
                };
            }

            var sbUri = ServiceBusEnvironment.CreateServiceUri("sb", this.cloudCredentials.ServiceNamespace, "");
            var factory = MessagingFactory.Create(sbUri, factorySettings);

            var topicClient = factory.CreateTopicClient(destinationTopic);

            return new AzureBusSender(this.log, topicClient, destinationTopic, sessionId);
        }
    }
}
