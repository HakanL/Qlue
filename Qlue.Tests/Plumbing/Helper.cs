using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Qlue.Transport;
using Qlue.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Qlue.Tests.Plumbing
{
    internal class Helper<T>
    {
        public readonly string ConsumerTopic = "qlue-website";
        public readonly string ServiceTopic = "qlue-server";

        private ManualResetEvent listenerRunningRequest;
        private ManualResetEvent listenerRunningService;
        private ManualResetEvent listenerRunningNotify;
        private ManualResetEvent serviceDispatchCalled;
        private ManualResetEvent notifyDispatchCalled;
        private TestInstance testInstance;

        public int MessagesSent;
        public int MessagesDispatched;
        public int MessagesReceived;
        private ILog log;


        public Helper(TestInstance testInstance)
        {
            this.testInstance = testInstance;
            this.serviceDispatchCalled = new ManualResetEvent(false);
            this.notifyDispatchCalled = new ManualResetEvent(false);
        }

        public QueueInstance GetQueue(string queueName)
        {
            QueueInstance queueInstance;
            if (!this.testInstance.Queues.TryGetValue(queueName, out queueInstance))
            {
                queueInstance = new QueueInstance();
                this.testInstance.Queues[queueName] = queueInstance;
            }

            return queueInstance;
        }

        public T ExecutePipelineFromQueueObject(QueueObject queueObject)
        {
            var fakePipeline = CreateTestInboundPipeline();
            var inboundContext = CreateContextFromQueueObject(queueObject);
            fakePipeline.Execute(inboundContext).Wait();

            return (T)inboundContext.Body;
        }

        public Pipeline.PipelineContext ExecutePipelineFromRequest(T request)
        {
            var fakePipeline = CreateTestOutboundPipeline();
            var outboundContext = Pipeline.PipelineContext.CreateFromRequest("EMPTY", request, null, null);
            fakePipeline.Execute(outboundContext).Wait();

            return outboundContext;
        }

        public ILog Log
        {
            get
            {
                lock (this)
                {
                    if (this.log == null)
                        this.log = this.testInstance.WireUpLogger();

                    return this.log;
                }
            }
        }
        private Pipeline.PipelineExecutor CreateTestInboundPipeline()
        {
            var mockCloudCredentials = WireupCloudCredentials();
            var mockBlobClient = WireupBlobClient();

            var factory = new Pipeline.PipelineDefaultFactory(this.Log, new MessageSerializer());

            var blobRepo = new BlobRepository(this.Log, mockBlobClient.Object);

            return factory.CreateInboundPipeline(blobRepo);
        }

        private Pipeline.PipelineExecutor CreateTestOutboundPipeline()
        {
            var mockCloudCredentials = WireupCloudCredentials();
            var mockBlobClient = WireupBlobClient();

            var factory = new Pipeline.PipelineDefaultFactory(this.Log, new MessageSerializer());

            var blobRepo = new BlobRepository(this.Log, mockBlobClient.Object);

            var container = blobRepo.GetBlobContainerForStoring();

            var pipeline = new Pipeline.PipelineExecutor(
                new Pipeline.Component.Serialize(this.Log, new MessageSerializer()),
                new Pipeline.Component.Compress(this.Log, System.IO.Compression.CompressionLevel.Fastest),
                new Pipeline.Component.OverflowPut(this.Log, container)
                );

            return pipeline;
        }

        public RequestChannel CreateRequestChannel(int outboundConnections = 1, int successEvery = 1)
        {
            this.listenerRunningRequest = new ManualResetEvent(false);

            var mockBlobClient = WireupBlobClient();
            var mockBusTransport = WireupBusTransport(ConsumerTopic, ServiceTopic, successEvery: successEvery);

            var consumerChannel = new RequestChannel(
                this.testInstance.WireUpLogFactory(),
                ConsumerTopic,
                ServiceTopic,
                outboundConnections,
                mockBusTransport.Object,
                mockBlobClient.Object);

            // Wait for the listener to be running
            listenerRunningRequest.WaitOne(10000);

            return consumerChannel;
        }

        public void RegisterTestDispatch<T2>(ServiceChannel serviceChannel, Func<T, T2> serviceAction)
        {
            serviceChannel.RegisterDispatch<T, T2>((request, ctx) =>
            {
                Interlocked.Increment(ref MessagesDispatched);

                try
                {
                    var response = serviceAction.Invoke(request);

                    return response;
                }
                finally
                {
                    this.serviceDispatchCalled.Set();
                }
            }, this.Log);
        }

        public void RegisterTestDispatch(ServiceChannel serviceChannel, Action<T> serviceAction)
        {
            serviceChannel.RegisterDispatch<T>((request, ctx) =>
            {
                Interlocked.Increment(ref MessagesDispatched);

                try
                {
                    serviceAction.Invoke(request);
                    this.serviceDispatchCalled.Set();
                }
                finally
                {
                    this.serviceDispatchCalled.Set();
                }
            }, this.Log);
        }

        public void RegisterTestDispatch(ServiceChannel serviceChannel, Action<T, InvokeContext> serviceAction)
        {
            serviceChannel.RegisterDispatch<T>((request, ctx) =>
            {
                Interlocked.Increment(ref MessagesDispatched);

                try
                {
                    serviceAction.Invoke(request, ctx);
                }
                finally
                {
                    this.serviceDispatchCalled.Set();
                }
            }, this.Log);
        }

        public void RegisterAsyncTestDispatch(ServiceChannel serviceChannel, Func<T, Task> serviceAction)
        {
            serviceChannel.RegisterAsyncDispatch<T>(async (request, ctx) =>
            {
                Interlocked.Increment(ref MessagesDispatched);

                try
                {
                    await serviceAction(request);
                }
                finally
                {
                    this.serviceDispatchCalled.Set();
                }
            }, this.Log);
        }

        public void RegisterAsyncTestDispatch<T2>(ServiceChannel serviceChannel, Func<T2, Task> serviceAction)
        {
            serviceChannel.RegisterAsyncDispatch<T2>(async (request, ctx) =>
            {
                Interlocked.Increment(ref MessagesDispatched);

                try
                {
                    await serviceAction(request);
                }
                finally
                {
                    this.serviceDispatchCalled.Set();
                }
            }, this.Log);
        }

        public void RegisterTestDispatch<T2>(NotifyChannel notifyChannel, Action<T2, InvokeContext> serviceAction)
        {
            notifyChannel.RegisterDispatch<T2>((request, ctx) =>
            {
                Interlocked.Increment(ref MessagesDispatched);

                try
                {
                    serviceAction.Invoke(request, ctx);
                }
                finally
                {
                    this.notifyDispatchCalled.Set();
                }
            }, this.Log);
        }

        public void RegisterAsyncTestDispatch<T2>(NotifyChannel notifyChannel, Func<T2, InvokeContext, Task> serviceAction)
        {
            notifyChannel.RegisterAsyncDispatch<T2>(async (request, ctx) =>
            {
                Interlocked.Increment(ref MessagesDispatched);

                try
                {
                    await serviceAction(request, ctx);
                }
                finally
                {
                    this.notifyDispatchCalled.Set();
                }
            }, this.Log);
        }

        public void WaitForServiceDispatch(int millisecondsTimeout = 50000)
        {
            Assert.IsTrue(this.serviceDispatchCalled.WaitOne(millisecondsTimeout));
        }

        public void WaitForNotifyDispatch(int millisecondsTimeout = 10000)
        {
            Assert.IsTrue(this.notifyDispatchCalled.WaitOne(millisecondsTimeout));
        }

        public void ResetNotifyDispatchWait()
        {
            this.notifyDispatchCalled.Reset();
        }

        public ServiceChannel CreateServiceChannel()
        {
            this.listenerRunningService = new ManualResetEvent(false);

            var mockBlobClient = WireupBlobClient();
            var mockBusTransport = WireupBusTransport(ServiceTopic, ConsumerTopic);
            var logFactory = this.testInstance.WireUpLogFactory();

            var serviceChannel = new ServiceChannel(logFactory, ServiceTopic, mockBusTransport.Object, mockBlobClient.Object);

            return serviceChannel;
        }

        public NotifyChannel CreateNotifyChannel(ServiceChannel serviceChannel)
        {
            this.listenerRunningNotify = new ManualResetEvent(false);

            var mockBlobClient = WireupBlobClient();
            var mockBusTransport = WireupBusTransport(ServiceTopic + "_notify", null);
            var logFactory = this.testInstance.WireUpLogFactory();

            var notifyChannel = new NotifyChannel(logFactory, ServiceTopic, mockBusTransport.Object, mockBlobClient.Object, serviceChannel);

            return notifyChannel;
        }

        public void ServiceStartReceiving(ServiceChannel serviceChannel)
        {
            serviceChannel.StartReceiving();

            // Wait for the listener to be running
            listenerRunningService.WaitOne(10000);
        }

        public void NotifyStartReceiving(NotifyChannel notifyChannel)
        {
            notifyChannel.StartReceiving();

            // Wait for the listener to be running
            listenerRunningNotify.WaitOne(10000);
        }

        public Mock<ICloudCredentials> WireupCloudCredentials()
        {
            var mock = new Mock<ICloudCredentials>();

            mock.Setup(x => x.GetServiceBusConnectionString())
                .Returns("TEST");

            return mock;
        }

        public Mock<IBlobClient> WireupBlobClient()
        {
            var mockBlobClient = new Mock<IBlobClient>();

            var blobContainer = new BlobContainer(this.testInstance);

            mockBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                .Returns(blobContainer);

            mockBlobClient.Setup(x => x.ContainerNameForStoring)
                .Returns("TEST");

            return mockBlobClient;
        }

        public Mock<IBusTransportFactory> WireupBusTransport(string listenTopic, string destinationTopic, int successEvery = 1)
        {
            var mockBusTransportFactory = new Mock<IBusTransportFactory>();
            var mockBusTransport = new Mock<IBusTransport>();
            var mockBusSender = new Mock<IBusSender>();

            mockBusSender.Setup(x => x.SendAsync(It.IsAny<Pipeline.PipelineContext>()))
                .Callback<Pipeline.PipelineContext>(x =>
                {
                    if (x.Type == Pipeline.PipelineContext.MessageType.Notify)
                        GetQueue(listenTopic + "_notify").Add(x);
                    else
                        GetQueue(destinationTopic).Add(x);
                    Interlocked.Increment(ref MessagesSent);
                })
                .Returns(() => Task.FromResult(MessagesSent % successEvery == 0));

            mockBusTransport.Setup(x => x.IsClosed)
                .Returns(false);

            mockBusTransport.Setup(x => x.CreateBusSender(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockBusSender.Object);

            mockBusTransportFactory.Setup(x => x.CreateRequestTopic(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockBusTransport.Object);
            mockBusTransportFactory.Setup(x => x.CreateResponseTopic(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockBusTransport.Object);
            mockBusTransportFactory.Setup(x => x.CreateNotifyTopic(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockBusTransport.Object);

            var mockAsyncResult = new Mock<IAsyncResult>();

            mockBusTransport.Setup(x => x.BeginReceive(It.IsAny<AsyncCallback>(), It.IsAny<object>()))
                .Callback<AsyncCallback, object>((a, b) =>
                    {
                        mockAsyncResult.Setup(x => x.AsyncState).Returns(b);

                        Task.Run(() =>
                            {
                                var queue = GetQueue(listenTopic);
                                if (this.listenerRunningRequest != null)
                                    this.listenerRunningRequest.Set();
                                if (this.listenerRunningService != null)
                                    this.listenerRunningService.Set();
                                if (this.listenerRunningNotify != null)
                                    this.listenerRunningNotify.Set();

                                while (!queue.WaitForNewItemInQueue()) ;
                                a.Invoke(mockAsyncResult.Object);
                            });
                    })
                .Returns(new Mock<IAsyncResult>().Object);

            mockBusTransport.Setup(x => x.EndReceive(It.IsAny<IAsyncResult>()))
                .Returns<IAsyncResult>(x =>
                    {
                        Interlocked.Increment(ref MessagesReceived);

                        var queue = GetQueue(listenTopic);
                        var item = queue.Dequeue();
                        var context = CreateContextFromQueueObject(item);
                        return context;
                    });

            return mockBusTransportFactory;
        }

        public Pipeline.PipelineContext CreateContextFromQueueObject(QueueObject queueObject)
        {
            return Pipeline.PipelineContext.CreateFromInboundMessage(
                queueObject.GetStreamFromPayload(),
                queueObject.ContentType,
                queueObject.MessageId,
                queueObject.From,
                queueObject.RelatesTo,
                queueObject.SessionId,
                null,
                null,
                null,
                queueObject.Properties
                );
        }
    }
}
