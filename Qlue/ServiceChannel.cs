using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Qlue.Logging;
using Qlue.Transport;
using System.Globalization;

namespace Qlue
{
    public class ServiceChannel : IServiceChannel, IDisposable
    {
        private ILog log;

        private Dictionary<string, Pipeline.PipelineExecutor> channelCache =
            new Dictionary<string, Pipeline.PipelineExecutor>(StringComparer.OrdinalIgnoreCase);
        private string endpoint;
        private Dictionary<Type, Dispatch> dispatchers;
        private readonly IBlobContainer sendingBlobContainer;
        private BusReceiver messageReceiver;
        private readonly Pipeline.PipelineDefaultFactory pipelineFactory;
        private readonly string sessionId;
        private readonly IBusTransport busTransport;
        private readonly List<Func<Exception, Exception>> exceptionHandlers;
        private readonly string version;

        public ServiceChannel(
            ILogFactory logFactory,
            string listenTopic,
            IBusTransportFactory busTransportFactory,
            IBlobClient blobClient,
            string version = null,
            string subscriptionName = null)
        {
            this.log = logFactory.GetLogger("Qlue");

            this.log.Info("Creating ServiceChannel for '{0}' topic, version {1}", listenTopic, version);

            if (string.IsNullOrEmpty(subscriptionName))
                subscriptionName = "service";

            this.endpoint = listenTopic;
            this.version = version;

            this.sessionId = Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
            this.dispatchers = new Dictionary<Type, Dispatch>();

            this.exceptionHandlers = new List<Func<Exception, Exception>>();
            var blobRepository = new BlobRepository(this.log, blobClient);
            this.sendingBlobContainer = blobRepository.GetBlobContainerForStoring();

            var serializer = new MessageSerializer();
            this.pipelineFactory = new Pipeline.PipelineDefaultFactory(this.log, serializer);

            Func<Pipeline.PipelineContext, Task<bool>> incomingObserver = async ctx =>
            {
                AsyncContext.StoreKeyValue("MessageId", ctx.MessageId);
                AsyncContext.StoreKeyValue("CustomSessionId", ctx.CustomSessionId);

                ILog dispatchLog = this.log;
                IDisposable ndc = null;

                try
                {
                    ExceptionWrapper responseException = null;

                    try
                    {
                        Dispatch dispatch = null;
                        lock (this.dispatchers)
                        {
                            this.dispatchers.TryGetValue(ctx.BodyType, out dispatch);
                        }

                        if (dispatch != null)
                        {
                            if (dispatch.Log != null)
                                dispatchLog = dispatch.Log;

                            var invokeContext = new InvokeContext(ctx.MessageId, ctx.CustomSessionId, Notify, dispatchLog);

                            ndc = dispatchLog.Context(ctx.BodyType.Name);
                            dispatchLog.Trace("Dispatching message id {0} from custom session {1}, topic {2}", ctx.MessageId, ctx.CustomSessionId, ctx.From);

                            await dispatch.ExecuteAction(ctx.Body, invokeContext, async r =>
                                {
                                    if (r != null)
                                    {
                                        await Response(ctx.From, ctx.MessageId, ctx.SessionId, r, dispatchLog);
                                    }
                                });
                        }
                        else
                        {
                            this.log.Warn("Dispatching message id {0}, type {1} from custom session {2}, topic {3} not found in dispatchers",
                                ctx.MessageId, ctx.BodyType.Name, ctx.CustomSessionId, ctx.From);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is Qlue.WarningException)
                            dispatchLog.WarnException("Exception in IncomingObserver", ex);
                        else
                            dispatchLog.ErrorException("Exception in IncomingObserver", ex);

                        Exception newException = ex;
                        // See if we should handle this exception in the exception handler
                        foreach (var exceptionHandler in this.exceptionHandlers)
                            newException = exceptionHandler.Invoke(newException);

                        ExceptionWrapper wrappedException;
                        if (ex != newException)
                        {
                            dispatchLog.Trace("Exception remapped to {0}", newException.GetType().Name);
                            wrappedException = new ExceptionWrapper(newException, ex.StackTrace);
                        }
                        else
                            wrappedException = new ExceptionWrapper(ex);

                        responseException = wrappedException;
                    }

                    if (responseException != null)
                    {
                        // Send to client
                        try
                        {
                            await Response(ctx.From, ctx.MessageId, ctx.SessionId, responseException, dispatchLog);
                        }
                        catch (Exception ex)
                        {
                            dispatchLog.ErrorException("Failed to send exception back", ex);
                        }
                    }

                }
                finally
                {
                    if (ndc != null)
                        ndc.Dispose();

                    AsyncContext.StoreKeyValue("MessageId", null);
                    AsyncContext.StoreKeyValue("CustomSessionId", null);
                }

                return true;
            };

            var inboundPipeline = this.pipelineFactory.CreateInboundPipeline(blobRepository);

            this.messageReceiver = BusReceiver.CreateRequestReceiver(
                this.log,
                listenTopic,
                incomingObserver,
                inboundPipeline,
                busTransportFactory,
                this.version,
                subscriptionName);

            this.busTransport = this.messageReceiver.BusTransport;
        }

        public void StartReceiving()
        {
            this.messageReceiver.StartReceiving();
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
                if (this.channelCache != null)
                {
                    foreach (var pipeline in this.channelCache.Values)
                        pipeline.Dispose();

                    this.channelCache = null;
                }

                if (this.messageReceiver != null)
                {
                    this.messageReceiver.Dispose();
                    this.messageReceiver = null;
                }
            }
        }

        private Pipeline.PipelineExecutor GetChannel(string topicName, int outboundConnections = 1)
        {
            lock (channelCache)
            {
                Pipeline.PipelineExecutor outboundPipeline;
                if (!channelCache.TryGetValue(topicName, out outboundPipeline))
                {
                    outboundPipeline = this.pipelineFactory.CreateOutboundPipeline(topicName, this.sendingBlobContainer, outboundConnections, this.sessionId, busTransport);
                    channelCache.Add(topicName, outboundPipeline);

                    log.Info("Create outbound pipeline for topic '{0}' using {1} connections", topicName, outboundConnections);
                }

                return outboundPipeline;
            }
        }

        public void RegisterDispatch<TRequest, TResponse>(Func<TRequest, InvokeContext, TResponse> executeAction, ILog logForDispatch)
        {
            lock (this.dispatchers)
            {
                this.dispatchers.Add(typeof(TRequest), new Dispatch<TRequest, TResponse>((r, ctx) =>
                    {
                        // Execute synchronously
                        return Task.FromResult(executeAction(r, ctx));
                    }, logForDispatch));
            }
        }

        public void RegisterAsyncDispatch<TRequest, TResponse>(Func<TRequest, InvokeContext, Task<TResponse>> executeAction, ILog logForDispatch)
        {
            lock (this.dispatchers)
            {
                this.dispatchers.Add(typeof(TRequest), new Dispatch<TRequest, TResponse>(executeAction, logForDispatch));
            }
        }

        public void RegisterAsyncDispatch<TRequest>(Func<TRequest, InvokeContext, Task> executeAction, ILog logForDispatch)
        {
            lock (this.dispatchers)
            {
                this.dispatchers.Add(typeof(TRequest), new Dispatch<TRequest>(executeAction, logForDispatch));
            }
        }

        public void RegisterDispatch<TRequest>(Action<TRequest, InvokeContext> executeAction, ILog logForDispatch)
        {
            lock (this.dispatchers)
            {
                this.dispatchers.Add(typeof(TRequest), new Dispatch<TRequest>((r, ctx) =>
                {
                    // Execute synchronously
                    executeAction(r, ctx);

                    return Task.FromResult(false);
                }, logForDispatch));
            }
        }

        internal async Task<string> Notify(object notifyObject, string topicName, ILog logForDispatch)
        {
            var context = Pipeline.PipelineContext.CreateFromNotify(this.endpoint, this.sessionId, notifyObject, this.version);

            logForDispatch.Trace("Send notify object {2} with id {0} in session {1}", context.MessageId, this.sessionId, notifyObject.GetType().Name);

            var responseChannel = GetChannel(string.IsNullOrEmpty(topicName) ? this.endpoint : topicName);

            await responseChannel.Execute(context);

            return context.MessageId;
        }

        protected async Task<string> Response(string replyTo, string relatesTo, string responseSessionId, object responseObject, ILog logForDispatch)
        {
            var context = Pipeline.PipelineContext.CreateFromResponse(this.endpoint, relatesTo, responseSessionId, responseObject);

            logForDispatch.Trace("Return response object id {0} in reply to {1} in session {2}", context.MessageId, relatesTo, responseSessionId);

            var responseChannel = GetChannel(replyTo);

            await responseChannel.Execute(context);

            return context.MessageId;
        }

        public void RegisterExceptionHandler(Func<Exception, Exception> exceptionHandler)
        {
            this.exceptionHandlers.Add(exceptionHandler);
        }
    }
}
