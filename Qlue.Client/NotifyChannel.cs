using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Qlue.Logging;
using Qlue.Transport;

namespace Qlue
{
    public class NotifyChannel : INotifyChannel, IDisposable
    {
        private ILog log;

        private Dictionary<Type, Dispatch> dispatchers;
        private BusReceiver messageReceiver;
        private readonly Pipeline.PipelineDefaultFactory pipelineFactory;
        private readonly IServiceChannel serviceChannel;
        private readonly string version;

        public NotifyChannel(
            ILogFactory logFactory,
            string listenTopic,
            IBusTransportFactory busTransportFactory,
            IBlobClient blobClient,
            IServiceChannel serviceChannel,
            string subscriptionName = null,
            string version = null)
        {
            this.log = logFactory.GetLogger("Qlue");

            this.log.Info("Creating NotifyChannel for '{0}' topic, version {1}", listenTopic, version);

            this.serviceChannel = serviceChannel;
            this.version = version;

            this.dispatchers = new Dictionary<Type, Dispatch>();

            var blobRepository = new BlobRepository(this.log, blobClient);

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

                        await dispatch.ExecuteAction(ctx.Body, invokeContext, r => Task.FromResult(false));
                    }
#if FULL_LOGGING
                    else
                        this.log.Debug("Notify type {0} not found in dispatchers for subscription {1}", ctx.BodyType.Name, subscriptionName);
#endif
                }
                catch (Exception ex)
                {
                    dispatchLog.ErrorException("Exception in IncomingObserver", ex);
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

            this.messageReceiver = BusReceiver.CreateNotifyReceiver(
                this.log,
                listenTopic,
                incomingObserver,
                inboundPipeline,
                busTransportFactory,
                subscriptionName,
                this.version);
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
                if (this.messageReceiver != null)
                {
                    this.messageReceiver.Dispose();
                    this.messageReceiver = null;
                }
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

        protected Task<string> Notify(object notifyObject, string topicName, ILog logForDispatch)
        {
            var sendChannel = this.serviceChannel as ServiceChannel;
            if (sendChannel == null)
                throw new InvalidOperationException("ServiceChannel is not configured");

            return sendChannel.Notify(notifyObject, topicName, logForDispatch);
        }
    }
}
