using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Qlue.Transport;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Linq;

namespace Qlue.Tests
{
    [TestClass]
    public class NotifyTests
    {
        [TestMethod]
        public void Should_Send_And_Receive_Notify()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage1>(testInstance);

            var serviceChannel = helper.CreateServiceChannel();
            var consumerChannel = helper.CreateRequestChannel();
            var notifyChannel = helper.CreateNotifyChannel(serviceChannel);

            TestMessage1 receivedMessage = null;
            TestNotifyMessage1 notifyMessage = null;
            helper.RegisterTestDispatch(serviceChannel, (request, ctx) =>
            {
                receivedMessage = request;

                var notify = new TestNotifyMessage1
                {
                    StringProp = request.StringProp,
                    IntProp = request.IntProp
                };

                ctx.SendNotify(notify);

            });

            helper.RegisterTestDispatch<TestNotifyMessage1>(notifyChannel, (request, ctx) =>
                {
                    notifyMessage = request;
                });

            helper.NotifyStartReceiving(notifyChannel);
            helper.ServiceStartReceiving(serviceChannel);

            var testMessage = new TestMessage1
            {
                IntProp = 666,
                StringProp = "Hello Hell"
            };
            consumerChannel.SendOneWay(testMessage, helper.Log);

            helper.WaitForServiceDispatch();
            helper.WaitForNotifyDispatch();

            var serviceQueue = helper.GetQueue(helper.ServiceTopic);

            consumerChannel.Dispose();
            serviceChannel.Dispose();
            notifyChannel.Dispose();


            // Checks
            Assert.IsNotNull(receivedMessage);
            Assert.IsNotNull(notifyMessage);

            Assert.AreEqual(testMessage.IntProp, receivedMessage.IntProp);
            Assert.AreEqual(testMessage.StringProp, receivedMessage.StringProp);
            Assert.AreEqual(testMessage.IntProp, notifyMessage.IntProp);
            Assert.AreEqual(testMessage.StringProp, notifyMessage.StringProp);
            Assert.AreEqual(typeof(TestMessage1).AssemblyQualifiedName, serviceQueue[0].ContentType);
            Assert.AreEqual(2, helper.MessagesSent);
            Assert.AreEqual(2, helper.MessagesReceived);
            Assert.AreEqual(2, helper.MessagesDispatched);
            Assert.AreEqual(0, testInstance.BlobGets);
            Assert.AreEqual(0, testInstance.BlobPuts);
            Assert.AreEqual(0, testInstance.BlobStorage.Count);
        }

        [TestMethod]
        public void Should_Send_And_Receive_Notify_Async()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage1>(testInstance);

            var serviceChannel = helper.CreateServiceChannel();
            var consumerChannel = helper.CreateRequestChannel();
            var notifyChannel = helper.CreateNotifyChannel(serviceChannel);

            TestMessage1 receivedMessage = null;
            TestNotifyMessage1 notifyMessage = null;
            helper.RegisterTestDispatch(serviceChannel, (request, ctx) =>
            {
                receivedMessage = request;

                var notify = new TestNotifyMessage1
                {
                    StringProp = request.StringProp,
                    IntProp = request.IntProp
                };

                ctx.SendNotify(notify);

            });

            helper.RegisterAsyncTestDispatch<TestNotifyMessage1>(notifyChannel, (request, ctx) =>
            {
                notifyMessage = request;

                return Task.FromResult(false);
            });

            helper.NotifyStartReceiving(notifyChannel);
            helper.ServiceStartReceiving(serviceChannel);

            var testMessage = new TestMessage1
            {
                IntProp = 666,
                StringProp = "Hello Hell"
            };
            consumerChannel.SendOneWay(testMessage, helper.Log);

            helper.WaitForServiceDispatch();
            helper.WaitForNotifyDispatch();

            var serviceQueue = helper.GetQueue(helper.ServiceTopic);

            consumerChannel.Dispose();
            serviceChannel.Dispose();
            notifyChannel.Dispose();


            // Checks
            Assert.IsNotNull(receivedMessage);
            Assert.IsNotNull(notifyMessage);

            Assert.AreEqual(testMessage.IntProp, receivedMessage.IntProp);
            Assert.AreEqual(testMessage.StringProp, receivedMessage.StringProp);
            Assert.AreEqual(testMessage.IntProp, notifyMessage.IntProp);
            Assert.AreEqual(testMessage.StringProp, notifyMessage.StringProp);
            Assert.AreEqual(typeof(TestMessage1).AssemblyQualifiedName, serviceQueue[0].ContentType);
            Assert.AreEqual(2, helper.MessagesSent);
            Assert.AreEqual(2, helper.MessagesReceived);
            Assert.AreEqual(2, helper.MessagesDispatched);
            Assert.AreEqual(0, testInstance.BlobGets);
            Assert.AreEqual(0, testInstance.BlobPuts);
            Assert.AreEqual(0, testInstance.BlobStorage.Count);
        }

        [TestMethod]
        public void Should_Send_And_Receive_Notify_Two_Levels()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage1>(testInstance);

            var serviceChannel = helper.CreateServiceChannel();
            var consumerChannel = helper.CreateRequestChannel();
            var notifyChannel = helper.CreateNotifyChannel(serviceChannel);

            TestMessage1 receivedMessage = null;
            TestNotifyMessage1 notifyMessage = null;
            TestNotifyMessage2 notifyMessage2 = null;
            helper.RegisterTestDispatch(serviceChannel, (request, ctx) =>
            {
                receivedMessage = request;

                var notify = new TestNotifyMessage1
                {
                    StringProp = request.StringProp,
                    IntProp = request.IntProp
                };

                ctx.SendNotify(notify);
            });

            var level2Wait = new ManualResetEvent(false);
            helper.RegisterAsyncTestDispatch<TestNotifyMessage1>(notifyChannel, (request, ctx) =>
            {
                notifyMessage = request;

                var notify2 = new TestNotifyMessage2
                {
                    StringProp = request.StringProp + "2",
                    IntProp = request.IntProp + 2
                };
                level2Wait.Set();
                ctx.SendNotify(notify2);

                return Task.FromResult(false);
            });

            helper.RegisterAsyncTestDispatch<TestNotifyMessage2>(notifyChannel, (request, ctx) =>
            {
                notifyMessage2 = request;

                return Task.FromResult(false);
            });

            helper.NotifyStartReceiving(notifyChannel);
            helper.ServiceStartReceiving(serviceChannel);

            var testMessage = new TestMessage1
            {
                IntProp = 666,
                StringProp = "Hello Hell"
            };
            consumerChannel.SendOneWay(testMessage, helper.Log);

            helper.WaitForServiceDispatch();
            helper.WaitForNotifyDispatch();
            helper.ResetNotifyDispatchWait();
            level2Wait.WaitOne(10000);
            helper.WaitForNotifyDispatch();

            var serviceQueue = helper.GetQueue(helper.ServiceTopic);

            consumerChannel.Dispose();
            serviceChannel.Dispose();
            notifyChannel.Dispose();


            // Checks
            Assert.IsNotNull(receivedMessage);
            Assert.IsNotNull(notifyMessage);
            Assert.IsNotNull(notifyMessage2);

            Assert.AreEqual(testMessage.IntProp, receivedMessage.IntProp);
            Assert.AreEqual(testMessage.StringProp, receivedMessage.StringProp);
            Assert.AreEqual(testMessage.IntProp, notifyMessage.IntProp);
            Assert.AreEqual(testMessage.StringProp, notifyMessage.StringProp);
            Assert.AreEqual(testMessage.IntProp + 2, notifyMessage2.IntProp);
            Assert.AreEqual(testMessage.StringProp + "2", notifyMessage2.StringProp);
            Assert.AreEqual(typeof(TestMessage1).AssemblyQualifiedName, serviceQueue[0].ContentType);
            Assert.AreEqual(3, helper.MessagesSent);
            Assert.AreEqual(3, helper.MessagesReceived);
            Assert.AreEqual(3, helper.MessagesDispatched);
            Assert.AreEqual(0, testInstance.BlobGets);
            Assert.AreEqual(0, testInstance.BlobPuts);
            Assert.AreEqual(0, testInstance.BlobStorage.Count);
        }

        [TestMethod]
        public void Should_Send_And_Receive_Notify_Two_Levels_Misconfigured()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage1>(testInstance);

            var serviceChannel = helper.CreateServiceChannel();
            var consumerChannel = helper.CreateRequestChannel();
            var notifyChannel = helper.CreateNotifyChannel(null);

            TestMessage1 receivedMessage = null;
            TestNotifyMessage1 notifyMessage = null;
            helper.RegisterTestDispatch(serviceChannel, (request, ctx) =>
            {
                receivedMessage = request;

                var notify = new TestNotifyMessage1
                {
                    StringProp = request.StringProp,
                    IntProp = request.IntProp
                };

                ctx.SendNotify(notify);
            });

            var level2Wait = new ManualResetEvent(false);
            helper.RegisterAsyncTestDispatch<TestNotifyMessage1>(notifyChannel, (request, ctx) =>
            {
                helper.ResetNotifyDispatchWait();
                notifyMessage = request;

                var notify2 = new TestNotifyMessage2
                {
                    StringProp = request.StringProp + "2",
                    IntProp = request.IntProp + 2
                };
                level2Wait.Set();
                ctx.SendNotify(notify2);

                return Task.FromResult(false);
            });

            helper.NotifyStartReceiving(notifyChannel);
            helper.ServiceStartReceiving(serviceChannel);

            var testMessage = new TestMessage1
            {
                IntProp = 666,
                StringProp = "Hello Hell"
            };
            consumerChannel.SendOneWay(testMessage, helper.Log);

            helper.WaitForServiceDispatch();
            helper.WaitForNotifyDispatch();
            Assert.IsTrue(level2Wait.WaitOne(10000));
            helper.WaitForNotifyDispatch();
            testInstance.WaitForErrorReceived();

            var serviceQueue = helper.GetQueue(helper.ServiceTopic);

            consumerChannel.Dispose();
            serviceChannel.Dispose();
            notifyChannel.Dispose();


            // Checks
            Assert.IsNotNull(receivedMessage);
            Assert.IsNotNull(notifyMessage);

            Assert.AreEqual(testMessage.IntProp, receivedMessage.IntProp);
            Assert.AreEqual(testMessage.StringProp, receivedMessage.StringProp);
            Assert.AreEqual(testMessage.IntProp, notifyMessage.IntProp);
            Assert.AreEqual(testMessage.StringProp, notifyMessage.StringProp);
            Assert.AreEqual(typeof(TestMessage1).AssemblyQualifiedName, serviceQueue[0].ContentType);
            Assert.AreEqual(2, helper.MessagesSent);
            Assert.AreEqual(2, helper.MessagesReceived);
            Assert.AreEqual(2, helper.MessagesDispatched);
            Assert.AreEqual(0, testInstance.BlobGets);
            Assert.AreEqual(0, testInstance.BlobPuts);
            Assert.AreEqual(0, testInstance.BlobStorage.Count);

            testInstance.ShouldHaveOne("ERROR", "InvalidOperationException/Exception in IncomingObserver");
        }

        [TestMethod]
        public void Should_Throw_Exception_Observer()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage1>(testInstance);

            var serviceChannel = helper.CreateServiceChannel();
            var consumerChannel = helper.CreateRequestChannel();
            var notifyChannel = helper.CreateNotifyChannel(serviceChannel);

            TestMessage1 receivedMessage = null;
            helper.RegisterTestDispatch(serviceChannel, (request, ctx) =>
            {
                receivedMessage = request;

                var notify = new TestNotifyMessage1
                {
                    StringProp = request.StringProp,
                    IntProp = request.IntProp
                };

                ctx.SendNotify(notify);
            });

            helper.RegisterTestDispatch<TestNotifyMessage1>(notifyChannel, (request, ctx) =>
            {
                throw new ArgumentException("Test");
            });

            helper.NotifyStartReceiving(notifyChannel);
            helper.ServiceStartReceiving(serviceChannel);

            var testMessage = new TestMessage1
            {
                IntProp = 666,
                StringProp = "Hello Hell"
            };
            consumerChannel.SendOneWay(testMessage, helper.Log);

            helper.WaitForServiceDispatch();
            helper.WaitForNotifyDispatch();
            testInstance.WaitForErrorReceived();

            var serviceQueue = helper.GetQueue(helper.ServiceTopic);

            consumerChannel.Dispose();
            serviceChannel.Dispose();
            notifyChannel.Dispose();

            testInstance.ShouldHaveOne("ERROR", "ArgumentException/Exception in IncomingObserver");
        }

    }
}
