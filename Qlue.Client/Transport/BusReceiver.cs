using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Qlue.Logging;

namespace Qlue
{
    internal class BusReceiver : IDisposable
    {
        private ILog log;

        private IBusTransport busTransport;
        private CancellationTokenSource cancelSource;
        private Pipeline.PipelineExecutor inboundPipeline;
        private readonly List<Func<Pipeline.PipelineContext, Task<bool>>> observers;

        private BusReceiver(
            ILog log,
            string listenTopic,
            Pipeline.PipelineContext.MessageType messageType,
            Func<Pipeline.PipelineContext, Task<bool>> observer,
            Pipeline.PipelineExecutor inboundPipeline,
            string responseSessionId,
            IBusTransportFactory busTransportFactory,
            string subscriptionName,
            string filterVersion)
        {
            this.log = log;

            log.Info("Creating BusReceiver for topic '{0}' on {2}   Type: {1}", listenTopic, messageType, filterVersion);

            this.inboundPipeline = inboundPipeline;
            this.observers = new List<Func<Pipeline.PipelineContext, Task<bool>>>();
            if(observer != null)
                this.observers.Add(observer);

            switch (messageType)
            {
                case Pipeline.PipelineContext.MessageType.Request:
                    this.busTransport = busTransportFactory.CreateRequestTopic(listenTopic, filterVersion, subscriptionName);
                    break;

                case Pipeline.PipelineContext.MessageType.Response:
                    this.busTransport = busTransportFactory.CreateResponseTopic(listenTopic, responseSessionId, filterVersion);
                    break;

                case Pipeline.PipelineContext.MessageType.Notify:
                    this.busTransport = busTransportFactory.CreateNotifyTopic(listenTopic, filterVersion, subscriptionName);
                    break;

                default:
                    throw new InvalidOperationException();
            }

            this.cancelSource = new CancellationTokenSource();
        }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if(disposing)
            {
                if(this.cancelSource != null)
                {
                    this.cancelSource.Cancel();
                    this.cancelSource.Dispose();
                    this.cancelSource = null;
                }

                if(this.busTransport != null)
                {
                    this.busTransport.Close();
                    this.busTransport = null;
                }

                if(this.inboundPipeline != null)
                {
                    this.inboundPipeline.Dispose();
                    this.inboundPipeline = null;
                }
            }
        }

        public void AddObserver(Func<Pipeline.PipelineContext, Task<bool>> observer)
        {
            this.observers.Add(observer);
        }

        public IBusTransport BusTransport
        {
            get { return this.busTransport; }
        }

        public void StartReceiving()
        {
            this.busTransport.BeginReceive(ReceiveCallback, null);
        }

        public static BusReceiver CreateRequestReceiver(
            ILog log,
            string listenTopic,
            Func<Pipeline.PipelineContext, Task<bool>> observer,
            Pipeline.PipelineExecutor inboundPipeline,
            IBusTransportFactory busTransportFactory,
            string filterVersion,
            string subscriptionName = null)
        {
            return new BusReceiver(
                log,
                listenTopic,
                Pipeline.PipelineContext.MessageType.Request,
                observer,
                inboundPipeline,
                null,
                busTransportFactory,
                subscriptionName,
                filterVersion);
        }

        public static BusReceiver CreateResponseReceiver(
            ILog log,
            string listenTopic,
            Func<Pipeline.PipelineContext, Task<bool>> observer,
            Pipeline.PipelineExecutor inboundPipeline,
            string sessionId,
            IBusTransportFactory busTransportFactory,
            string filterVersion)
        {
            return new BusReceiver(
                log,
                listenTopic,
                Pipeline.PipelineContext.MessageType.Response,
                observer,
                inboundPipeline,
                sessionId,
                busTransportFactory,
                null,
                filterVersion);
        }

        public static BusReceiver CreateNotifyReceiver(
            ILog log,
            string listenTopic,
            Func<Pipeline.PipelineContext, Task<bool>> observer,
            Pipeline.PipelineExecutor inboundPipeline,
            IBusTransportFactory busTransportFactory,
            string subscriptionName,
            string filterVersion)
        {
            return new BusReceiver(
                log,
                listenTopic,
                Pipeline.PipelineContext.MessageType.Notify,
                observer,
                inboundPipeline,
                null,
                busTransportFactory,
                subscriptionName,
                filterVersion);
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                if (this.busTransport == null)
                    return;

                var context = this.busTransport.EndReceive(result);
                if (context != null)
                {
                    Task.Run(async () =>
                    {
                        AsyncContext.StoreKeyValue("MessageId", context.MessageId);
                        AsyncContext.StoreKeyValue("CustomSessionId", context.CustomSessionId);

#if FULL_LOGGING
                        log.Trace("Receive Message, MessageId: {0}   From: {1}", context.MessageId, context.From);
#endif

                        try
                        {
                            await this.inboundPipeline.Execute(context);

                            bool handled = false;
                            foreach(var observer in this.observers)
                            {
                                handled = await observer.Invoke(context);

                                if (handled)
                                    break;
                            }

#if FULL_LOGGING
                            if(!handled)
                                this.log.Trace("Message {0} related to {1} not handled", context.MessageId, context.RelatesTo);
#endif

                            this.busTransport.Complete(context);
                        }
                        catch (Exception)
                        {
                            // Failed to process message
                            this.busTransport.Abandon(context);
                        }
                        finally
                        {
                            AsyncContext.StoreKeyValue("MessageId", null);
                            AsyncContext.StoreKeyValue("CustomSessionId", null);
                        }
                    }, this.cancelSource.Token);
                }

            }
            catch (Microsoft.ServiceBus.Messaging.MessagingException ex)
            {
                if (ex.IsTransient)
                {
                    this.log.Warn("Transient messaging exception in ReceiveCallback: {0}", ex.Message);

                    // Just a little sleep for now
                    System.Threading.Thread.Sleep(3000);
                }
                else
                {
                    log.ErrorException(ex, "Messaging exception in ReceiveCallback: {0}", ex.Message);

                    throw;
                }
            }
            catch (Exception ex)
            {
                log.WarnException(ex, "Unhandled exception ReceiveCallback: {0}", ex.Message);
            }
            finally
            {
                try
                {
                    if (this.busTransport != null && !this.busTransport.IsClosed)
                        this.busTransport.BeginReceive(ReceiveCallback, null);
                }
                catch (OperationCanceledException)
                {
                    // Ignore
                }
                catch (Exception ex)
                {
                    log.ErrorException(ex, "Error in ReceiveCallback/finally: {0}", ex.Message);
                }
            }
        }
    }
}
