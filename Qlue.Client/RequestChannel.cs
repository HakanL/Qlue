using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Qlue.Logging;
using Qlue.Transport;
using System.Globalization;

namespace Qlue
{
    public class RequestChannel : IRequestChannel
    {
        private ILog log;

        private readonly string endpoint;
        private readonly Dictionary<string, WaitingFor> waitingFor;
        private BusReceiver messageReceiver;
        private Pipeline.PipelineExecutor outboundPipeline;
        private readonly string channelSessionId;
        private readonly string version;

        private RequestChannel(
            ILogFactory logFactory,
            string listenTopic,
            string destinationTopic,
            int outboundConnections,
            string version,
            string sessionId)
        {
            this.log = logFactory.GetLogger("Qlue");

            this.log.Info("Creating RequestChannel listening on '{0}', sending to '{1}' and {2} outbound connections, version {3}",
                listenTopic, destinationTopic, outboundConnections, version);

            this.endpoint = listenTopic;
            this.version = version;

            this.waitingFor = new Dictionary<string, WaitingFor>();

            if (string.IsNullOrEmpty(sessionId))
                this.channelSessionId = Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
            else
                this.channelSessionId = sessionId;
            this.log.Info("Session Id {0}", this.channelSessionId);
        }

        public RequestChannel(
            ILogFactory logFactory,
            string listenTopic,
            string destinationTopic,
            int outboundConnections,
            IBusTransportFactory busTransportFactory,
            IBlobClient blobClient,
            string sessionId = null,
            string version = null)
            : this(logFactory, listenTopic, destinationTopic, outboundConnections, version, sessionId)
        {
            var blobRepository = new BlobRepository(this.log, blobClient);
            var sendingBlobContainer = blobRepository.GetBlobContainerForStoring();

            var serializer = new MessageSerializer();
            var pipelineFactory = new Pipeline.PipelineDefaultFactory(this.log, serializer);

            var inboundPipeline = pipelineFactory.CreateInboundPipeline(blobRepository);

            this.messageReceiver = BusReceiver.CreateResponseReceiver(
                this.log,
                listenTopic,
                ResponseObserver,
                inboundPipeline,
                this.channelSessionId,
                busTransportFactory,
                this.version);

            this.outboundPipeline = pipelineFactory.CreateOutboundPipeline(
                destinationTopic,
                sendingBlobContainer,
                outboundConnections,
                this.channelSessionId,
                this.messageReceiver.BusTransport);

            this.messageReceiver.StartReceiving();
        }

        // This constructor is to support multiple RequestChannels with one BusReceiver
        internal RequestChannel(
            ILogFactory logFactory,
            string listenTopic,
            string destinationTopic,
            int outboundConnections,
            BlobRepository blobRepository,
            BusReceiver busReceiver,
            string sessionId,
            string version)
            : this(logFactory, listenTopic, destinationTopic, outboundConnections, version, sessionId)
        {
            var sendingBlobContainer = blobRepository.GetBlobContainerForStoring();

            var serializer = new MessageSerializer();
            var pipelineFactory = new Pipeline.PipelineDefaultFactory(this.log, serializer);

            this.messageReceiver = busReceiver;
            busReceiver.AddObserver(ResponseObserver);

            this.outboundPipeline = pipelineFactory.CreateOutboundPipeline(
                destinationTopic,
                sendingBlobContainer,
                outboundConnections,
                this.channelSessionId,
                this.messageReceiver.BusTransport);

            this.messageReceiver.StartReceiving();
        }

        private Task<bool> ResponseObserver(Pipeline.PipelineContext ctx)
        {
            WaitingFor waitFor = null;
            lock (waitingFor)
            {
                if (waitingFor.TryGetValue(ctx.RelatesTo, out waitFor))
                {
                    waitingFor.Remove(ctx.RelatesTo);
                }
            }
            if (waitFor != null)
            {
                ILog waitForLog = waitFor.Log ?? this.log;

                waitForLog.Trace("Received response related to {0}, took {1:N0}ms", ctx.RelatesTo, waitFor.ElapsedTime.TotalMilliseconds);

                waitFor.Completed(ctx.Body);

                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.outboundPipeline != null)
                {
                    this.outboundPipeline.Dispose();
                    this.outboundPipeline = null;
                }

                if (this.messageReceiver != null)
                {
                    this.messageReceiver.Dispose();
                    this.messageReceiver = null;
                }
            }
        }

        /// <summary>
        /// Synchronously request and wait for response
        /// </summary>
        /// <typeparam name="TResponse">Response object type</typeparam>
        /// <param name="request">Request object</param>
        /// <param name="timeout">Max time to wait for response</param>
        /// <param name="logForDispatch">Logger</param>
        /// <returns>Response object</returns>
        public TResponse SendWaitResponse<TResponse>(object request, TimeSpan timeout, ILog logForDispatch)
        {
            try
            {
                var task = Task.Run<TResponse>(async () => { return await SendWaitResponseAsync<TResponse>(request, timeout, logForDispatch).ConfigureAwait(false); });
                return task.Result;
            }
            catch (AggregateException ex)
            {
                throw ex.InnerException;
            }
        }

        /// <summary>
        /// Asynchronously request and wait for response
        /// </summary>
        /// <typeparam name="TResponse">Response object type</typeparam>
        /// <param name="request">Request object</param>
        /// <param name="timeout">Max time to wait for response</param>
        /// <param name="logForDispatch">Logger</param>
        /// <returns>Response object</returns>
        public async Task<TResponse> SendWaitResponseAsync<TResponse>(object request, TimeSpan timeout, ILog logForDispatch)
        {
            ILog logger = logForDispatch ?? this.log;

            var context = Pipeline.PipelineContext.CreateFromRequest(
                this.endpoint,
                request,
                logger.GetProperty("CustomSessionId", this.channelSessionId),
                this.version);

            AsyncContext.StoreKeyValue("MessageId", context.MessageId);
            AsyncContext.StoreKeyValue("CustomSessionId", context.CustomSessionId);
            logger.Trace("Sending request and waiting for response, MessageId: {0}, CustomSessionId: {1}, Version: {2}",
                context.MessageId, context.CustomSessionId, this.version);

            using (var waitingForObject = new WaitingFor(timeout, logForDispatch))
            {
                lock (waitingFor)
                    waitingFor.Add(context.MessageId, waitingForObject);

                Exception serviceException = null;

                try
                {
                    // Send
                    await this.outboundPipeline.Execute(context).ConfigureAwait(false);

                    logger.Trace("Message {0} sent, waiting for reply (timeout {1:N0} s)", context.MessageId, timeout.TotalSeconds);

                    // Wait for reply
                    await waitingForObject.Task;

                    var resultObject = waitingForObject.Task.Result;

                    var wrappedException = resultObject as ExceptionWrapper;
                    if (wrappedException != null)
                    {
                        // Exception from service
                        logger.Warn("Exception {0} from service, message: {1}   Stacktrace: {2}",
                            wrappedException.ExceptionType, wrappedException.Message, wrappedException.StackTrace);

                        AsyncContext.StoreKeyValue("ExceptionMessageId", context.MessageId);
                        AsyncContext.StoreKeyValue("ExceptionCustomSessionId", context.CustomSessionId);

                        serviceException = wrappedException.Unwrap();
                    }
                    else
                        return (TResponse)resultObject;
                }
                catch (Exception ex)
                {
                    // Make sure we remove it from the waiting list if we failed to publish it
                    lock (waitingFor)
                        waitingFor.Remove(context.MessageId);

                    AsyncContext.StoreKeyValue("ExceptionMessageId", context.MessageId);
                    AsyncContext.StoreKeyValue("ExceptionCustomSessionId", context.CustomSessionId);

                    if (ex is TaskCanceledException)
                        throw new TimeoutException(string.Format("Timed out waiting for message id {0}", context.MessageId));

                    throw;
                }
                finally
                {
                    AsyncContext.StoreKeyValue("MessageId", null);
                    AsyncContext.StoreKeyValue("CustomSessionId", null);
                }

                throw serviceException;
            }
        }

        /// <summary>
        /// Send a OneWay message, will wait for it to be delivered to the bus
        /// </summary>
        /// <param name="request">Request payload/message to deliver</param>
        /// <param name="logForDispatch">Logger</param>
        /// <returns>Message Id</returns>
        public string SendOneWay(object request, ILog logForDispatch)
        {
            var task = Task.Run<string>(async () => { return await SendOneWayAsync(request, logForDispatch).ConfigureAwait(false); });

            return task.Result;
        }

        /// <summary>
        /// Send a OneWay message asynchronously
        /// </summary>
        /// <param name="request">Request payload/message to deliver</param>
        /// <param name="logForDispatch">Logger</param>
        /// <returns>Message Id</returns>
        public async Task<string> SendOneWayAsync(object request, ILog logForDispatch)
        {
            ILog logger = logForDispatch ?? this.log;

            var context = Pipeline.PipelineContext.CreateFromRequest(
                this.endpoint,
                request,
                logger.GetProperty("CustomSessionId", this.channelSessionId),
                this.version);

            logger.Trace("Sending one-way request, MessageId: {0}, CustomSessionId: {1}", context.MessageId, context.CustomSessionId);

            await this.outboundPipeline.Execute(context);

            return context.MessageId;
        }

        public async Task<string> SendNotifyAsync(object notifyObject, string topicName, ILog logForDispatch)
        {
            ILog logger = logForDispatch ?? this.log;

            var context = Pipeline.PipelineContext.CreateFromNotify(this.endpoint, this.channelSessionId, notifyObject, this.version);

            logger.Trace("Send notify object {3} with id {0} in session {1}, CustomSessionId: {2}",
                context.MessageId,
                this.channelSessionId,
                context.CustomSessionId,
                notifyObject.GetType().Name);

            await this.outboundPipeline.Execute(context);

            return context.MessageId;
        }

        public string SendNotify(object notifyObject, string topicName, ILog logForDispatch)
        {
            var task = Task.Run<string>(async () => { return await SendNotifyAsync(notifyObject, topicName, logForDispatch).ConfigureAwait(false); });

            return task.Result;
        }
    }
}
