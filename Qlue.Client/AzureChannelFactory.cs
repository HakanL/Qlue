using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using Qlue.Logging;
using Qlue.Transport;

namespace Qlue
{
    public class AzureChannelFactory : IChannelFactory
    {
        private ILogFactory logFactory;
        private IBlobClient blobClient;
        private BlobRepository blobRepository;
        private ICloudCredentials cloudCredentials;
        private string queuePrefix;
        private string version;
        private readonly string sessionId;
        private IDictionary<string, BusReceiver> busReceivers;

        public AzureChannelFactory(
            ILogFactory logFactory,
            IBlobClient blobClient,
            IConfig config,
            IDeploymentVersionResolver versionResolver)
        {
            this.queuePrefix = config.QueuePrefix;
            this.cloudCredentials = config.GetCloudCredentials();
            this.logFactory = logFactory;
            this.blobClient = blobClient;
            this.sessionId = Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
            this.busReceivers = new Dictionary<string, BusReceiver>();

            if (System.Diagnostics.Debugger.IsAttached)
            {
                if (string.IsNullOrEmpty(this.queuePrefix))
                {
                    try
                    {
                        if (versionResolver != null)
                            this.version = versionResolver.GetLatestDeploymentVersion();
                        else
                            this.version = string.Empty;
                    }
                    catch (Exception)
                    {
                        this.version = string.Empty;
                    }
                }
                else
                {
                    // Make sure we don't filter on version when running in debug with queue prefix
                    this.version = string.Empty;
                }
            }
            else
                this.version = config.DeploymentVersion;

            // Register the deployment version and environment with the log factory
            this.logFactory.SetGlobalProperty("Version", string.IsNullOrEmpty(this.version) ? "DEBUG" : this.version);
            this.logFactory.SetGlobalProperty("Environment", config.DeploymentEnvironment);

            // Set the maximum number of concurrent connections
            ServicePointManager.DefaultConnectionLimit = 300;

            // The Azure Load Balancer drops idle connections without warning (typically after 4 mins),
            // so set up TCP keep alive to make sure we do not get dropped
            ServicePointManager.SetTcpKeepAlive(true, 1 * 60 * 1000, 500);

            // This sets it globally for all new ServicePoints
            ServicePointManager.UseNagleAlgorithm = false;
        }

        private BusReceiver GetRequestBusReceiver(string listenTopic)
        {
            lock (this.busReceivers)
            {
                if (this.blobRepository == null)
                    this.blobRepository = new BlobRepository(this.logFactory.GetLogger("Qlue"), this.blobClient);

                BusReceiver busReceiver;
                if (!this.busReceivers.TryGetValue(listenTopic, out busReceiver))
                {
                    var log = this.logFactory.GetLogger("Qlue");

                    var serializer = new MessageSerializer();
                    var pipelineFactory = new Pipeline.PipelineDefaultFactory(log, serializer);

                    var inboundPipeline = pipelineFactory.CreateInboundPipeline(this.blobRepository);

                    busReceiver = BusReceiver.CreateResponseReceiver(
                        log,
                        listenTopic,
                        null,
                        inboundPipeline,
                        this.sessionId,
                        new AzureBusTransportFactory(this.cloudCredentials, this.logFactory),
                        this.version);

                    this.busReceivers.Add(listenTopic, busReceiver);
                }

                return busReceiver;
            }
        }

        public IRequestChannel CreateRequestChannel(
            string listenTopic,
            string destinationTopic,
            int outboundConnections)
        {
            var busReceiver = GetRequestBusReceiver(this.queuePrefix + listenTopic);

            return new RequestChannel(
                this.logFactory,
                this.queuePrefix + listenTopic,
                this.queuePrefix + destinationTopic,
                outboundConnections,
                this.blobRepository,
                busReceiver,
                this.sessionId,
                this.version);
        }

        public IServiceChannel CreateServiceChannel(
            string listenTopic,
            string subscriptionName)
        {
            return new ServiceChannel(
                this.logFactory,
                this.queuePrefix + listenTopic,
                new AzureBusTransportFactory(this.cloudCredentials, this.logFactory),
                this.blobClient,
                this.version,
                subscriptionName);
        }

        public INotifyChannel CreateNotifyChannel(
            string listenTopic,
            IServiceChannel serviceChannel,
            string subscriptionName)
        {
            return new NotifyChannel(
                this.logFactory,
                this.queuePrefix + listenTopic,
                new AzureBusTransportFactory(this.cloudCredentials, this.logFactory),
                this.blobClient,
                serviceChannel,
                subscriptionName,
                this.version);
        }
    }
}
