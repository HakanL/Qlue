using Microsoft.VisualStudio.TestTools.UnitTesting;
using Qlue.Transport;
using System;
using System.Threading.Tasks;

namespace Qlue.Tests
{
    [TestClass]
    public class AzureIntegrationTests
    {
        private const string consumerTopic = "qlue-unittest";
        private const string serviceTopic = "qlue-unittest";
        private const string overflowContainer = "qlueunittest";

        [TestCleanup]
        public void Cleanup()
        {
            // We'll keep the topics/storage to not cause race conditions when running parallel unit tests

            //var logFactory = new Qlue.Logging.NullLogFactoryProvider();
            //var log = logFactory.GetLogger("Test");

            //var cloudCredentials = Shared.AzureCredentials.GetCloudCredentials();
            //var busTransport = new AzureBusTransportFactory(cloudCredentials, logFactory);
            //busTransport.Log = log;
            //var blobClient = new AzureBlobClient(cloudCredentials, overflowContainer);

            //busTransport.DeleteTopic(consumerTopic);
            //busTransport.DeleteTopic(serviceTopic);

            // It's slow to delete a container from Azure, we should probably not do this every time
            //blobClient.DeleteContainer(overflowContainer);
        }

        [TestMethod]
        [TestCategory("IntegrationTests")]
        public void Send_And_Receive_Small_Message()
        {
            var logFactory = new Qlue.Logging.NullLogFactoryProvider();
            var log = logFactory.GetLogger("Test");

            var cloudCredentials = Shared.AzureCredentials.GetCloudCredentials();
            var busTransportService = new AzureBusTransportFactory(cloudCredentials, logFactory);
            var blobClient = new AzureBlobClient(cloudCredentials, overflowContainer);

            // Simulate service
            var serviceChannel = new ServiceChannel(logFactory, serviceTopic, busTransportService, blobClient);

            serviceChannel.RegisterDispatch<TestMessage1, TestMessage2>((request, ctx) =>
            {
                var responseMessage = new TestMessage2
                {
                    IntProp = 666,
                    StringProp = "Test"
                };

                return responseMessage;
            }, log);

            serviceChannel.StartReceiving();


            var busTransportConsumer = new AzureBusTransportFactory(cloudCredentials, logFactory);
            // Simulate consumer (web site)
            var consumerChannel = new RequestChannel(logFactory, consumerTopic, serviceTopic, 1, busTransportConsumer, blobClient);

            // Create the new messages to send
            var requestMessage = new TestMessage1()
            {
                IntProp = 42,
                StringProp = "Data"
            };

            var syncResponse = consumerChannel.SendWaitResponse<TestMessage2>(requestMessage, TimeSpan.FromSeconds(10), log);

            Assert.AreEqual(666, syncResponse.IntProp);
            Assert.AreEqual("Test", syncResponse.StringProp);

            consumerChannel.Dispose();
            serviceChannel.Dispose();
        }

        [TestMethod]
        [TestCategory("IntegrationTests")]
        public void Send_And_Receive_Large_Message()
        {
            string consumerTopic = "qlue-unittest";
            string serviceTopic = "qlue-unittest";

            var logFactory = new Qlue.Logging.NullLogFactoryProvider();
            var log = logFactory.GetLogger("Test");

            var cloudCredentials = Shared.AzureCredentials.GetCloudCredentials();
            var busTransportService = new AzureBusTransportFactory(cloudCredentials, logFactory);
            var blobClient = new AzureBlobClient(cloudCredentials, overflowContainer);
            var random = new Random();

            // Simulate service
            var serviceChannel = new ServiceChannel(logFactory, serviceTopic, busTransportService, blobClient);

            serviceChannel.RegisterAsyncDispatch<TestMessage1, TestMessage2>((request, ctx) =>
            {
                var responseMessage = new TestMessage2
                {
                    IntProp = 666,
                    StringProp = "Test"
                };

                responseMessage.Data = new byte[100 * 1024];
                random.NextBytes(responseMessage.Data); 
                
                return Task.FromResult(responseMessage);
            }, log);

            serviceChannel.StartReceiving();


            var busTransportConsumer = new AzureBusTransportFactory(cloudCredentials, logFactory);
            // Simulate consumer (web site)
            var consumerChannel = new RequestChannel(logFactory, consumerTopic, serviceTopic, 1, busTransportConsumer, blobClient);

            // Create the new messages to send
            var requestMessage = new TestMessage1()
            {
                IntProp = 42,
                StringProp = "Data"
            };
            requestMessage.Data = new byte[100 * 1024];
            random.NextBytes(requestMessage.Data);

            var syncResponse = consumerChannel.SendWaitResponse<TestMessage2>(requestMessage, TimeSpan.FromSeconds(10), log);

            Assert.AreEqual(666, syncResponse.IntProp);
            Assert.AreEqual("Test", syncResponse.StringProp);

            consumerChannel.Dispose();
            serviceChannel.Dispose();
        }

        [TestMethod]
        [TestCategory("IntegrationTests")]
        public void Send_And_Receive_Small_Message_And_Notify()
        {
            var logFactory = new Qlue.Logging.NullLogFactoryProvider();
            var log = logFactory.GetLogger("Test");

            var cloudCredentials = Shared.AzureCredentials.GetCloudCredentials();
            var blobClient = new AzureBlobClient(cloudCredentials, overflowContainer);

            // Simulate service
            var busTransportService = new AzureBusTransportFactory(cloudCredentials, logFactory);
            var serviceChannel = new ServiceChannel(logFactory, serviceTopic, busTransportService, blobClient);

            serviceChannel.RegisterDispatch<TestMessage1, TestMessage2>((request, ctx) =>
            {
                var responseMessage = new TestMessage2
                {
                    IntProp = 666,
                    StringProp = "Test"
                };

                var notify = new TestNotifyMessage1
                {
                    IntProp = 123,
                    StringProp = "NotifyTest"
                };
                ctx.SendNotify(notify);

                return responseMessage;
            }, log);

            serviceChannel.StartReceiving();


            // Simulate notify
            var busTransportNotify= new AzureBusTransportFactory(cloudCredentials, logFactory);
            var notifyChannel = new NotifyChannel(logFactory, serviceTopic, busTransportNotify, blobClient, serviceChannel);

            TestNotifyMessage1 receivedNotify = null;
            notifyChannel.RegisterDispatch<TestNotifyMessage1>((request, ctx) =>
            {
                receivedNotify = request;
                Assert.IsNotNull(request);
            }, log);

            notifyChannel.StartReceiving();


            // Simulate consumer (web site)
            var busTransportConsumer = new AzureBusTransportFactory(cloudCredentials, logFactory);
            var consumerChannel = new RequestChannel(logFactory, consumerTopic, serviceTopic, 1, busTransportConsumer, blobClient);

            // Create the new messages to send
            var requestMessage = new TestMessage1()
            {
                IntProp = 42,
                StringProp = "Data"
            };

            var syncResponse = consumerChannel.SendWaitResponse<TestMessage2>(requestMessage, TimeSpan.FromSeconds(10), log);

            Assert.AreEqual(666, syncResponse.IntProp);
            Assert.AreEqual("Test", syncResponse.StringProp);
            Assert.AreEqual(123, receivedNotify.IntProp);
            Assert.AreEqual("NotifyTest", receivedNotify.StringProp);

            consumerChannel.Dispose();
            serviceChannel.Dispose();
            notifyChannel.Dispose();
        }

        [TestMethod]
        [TestCategory("IntegrationTests")]
        public void Send_Notify()
        {
            var logFactory = new Qlue.Logging.NullLogFactoryProvider();
            var log = logFactory.GetLogger("Test");

            var cloudCredentials = Shared.AzureCredentials.GetCloudCredentials();
            var busTransportService = new AzureBusTransportFactory(cloudCredentials, logFactory);
            var blobClient = new AzureBlobClient(cloudCredentials, overflowContainer);

            // Simulate service
            var serviceChannel = new ServiceChannel(logFactory, serviceTopic, busTransportService, blobClient);

            serviceChannel.RegisterDispatch<TestMessage1, TestMessage2>((request, ctx) =>
            {
                var responseMessage = new TestMessage2
                {
                    IntProp = 666,
                    StringProp = "Test"
                };

                return responseMessage;
            }, log);

            serviceChannel.StartReceiving();


            var busTransportConsumer = new AzureBusTransportFactory(cloudCredentials, logFactory);
            // Simulate consumer (web site)
            var consumerChannel = new RequestChannel(logFactory, consumerTopic, serviceTopic, 1, busTransportConsumer, blobClient);

            // Create the new messages to send
            var requestMessage = new TestMessage1()
            {
                IntProp = 42,
                StringProp = "Data"
            };

            var syncResponse = consumerChannel.SendWaitResponse<TestMessage2>(requestMessage, TimeSpan.FromSeconds(10), log);

            Assert.AreEqual(666, syncResponse.IntProp);
            Assert.AreEqual("Test", syncResponse.StringProp);

            consumerChannel.Dispose();
            serviceChannel.Dispose();
        }

    }
}
