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
    public class ServiceTests
    {
        [TestMethod]
        public void Should_Receive_Small_Message_And_Invoke_Dispatch()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage1>(testInstance);
            var serviceChannel = helper.CreateServiceChannel();

            TestMessage1 receivedMessage = null;
            helper.RegisterAsyncTestDispatch(serviceChannel, x =>
                {
                    receivedMessage = x;

                    return Task.FromResult(false);
                });
            helper.ServiceStartReceiving(serviceChannel);

            var testMessage = new TestMessage1
            {
                IntProp = 666,
                StringProp = "Hello Hell"
            };

            var testContext = helper.ExecutePipelineFromRequest(testMessage);

            var serviceQueue = helper.GetQueue(helper.ServiceTopic);
            serviceQueue.Add(testContext);

            helper.WaitForServiceDispatch();

            serviceChannel.Dispose();


            // Checks
            Assert.IsNotNull(receivedMessage);

            Assert.AreEqual(testMessage.IntProp, receivedMessage.IntProp);
            Assert.AreEqual(testMessage.StringProp, receivedMessage.StringProp);
            Assert.AreEqual(typeof(TestMessage1).AssemblyQualifiedName, serviceQueue[0].ContentType);
            Assert.AreEqual(0, helper.MessagesSent);
            Assert.AreEqual(1, helper.MessagesDispatched);
            Assert.AreEqual(0, testInstance.BlobGets);
            Assert.AreEqual(0, testInstance.BlobPuts);
            Assert.AreEqual(0, testInstance.BlobStorage.Count);
        }

        [TestMethod]
        public void Should_Receive_Small_Message_And_Invoke_Dispatch_Misconfigured()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage2>(testInstance);
            var serviceChannel = helper.CreateServiceChannel();

            helper.RegisterAsyncTestDispatch<TestMessage1>(serviceChannel, x =>
            {
                Assert.Fail("We should never get here");

                return Task.FromResult(false);
            });
            helper.ServiceStartReceiving(serviceChannel);

            var testMessage = new TestMessage2
            {
                IntProp = 666,
                StringProp = "Hello Hell"
            };

            var testContext = helper.ExecutePipelineFromRequest(testMessage);

            var serviceQueue = helper.GetQueue(helper.ServiceTopic);
            serviceQueue.Add(testContext);

            testInstance.LogTriggerString = "type TestMessage2 from custom session , topic EMPTY not found in dispatchers";
            testInstance.WaitForLogTriggerReceived();

            serviceChannel.Dispose();


            // Checks
            Assert.AreEqual(0, helper.MessagesSent);
            Assert.AreEqual(0, helper.MessagesDispatched);
            Assert.AreEqual(0, testInstance.BlobGets);
            Assert.AreEqual(0, testInstance.BlobPuts);
            Assert.AreEqual(0, testInstance.BlobStorage.Count);

            testInstance.ShouldHaveOne("WARN", "type TestMessage2 from custom session , topic EMPTY not found in dispatchers");
        }

        [TestMethod]
        public void Should_Throw_Exception_In_Dispatch()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage1>(testInstance);
            var serviceChannel = helper.CreateServiceChannel();

            helper.RegisterAsyncTestDispatch(serviceChannel, x =>
            {
                throw new ArgumentException();
            });
            helper.ServiceStartReceiving(serviceChannel);

            var testMessage = new TestMessage1
            {
                IntProp = 666,
                StringProp = "Hello Hell"
            };

            var testContext = helper.ExecutePipelineFromRequest(testMessage);

            var serviceQueue = helper.GetQueue(helper.ServiceTopic);
            serviceQueue.Add(testContext);

            helper.WaitForServiceDispatch();
            testInstance.WaitForErrorReceived();

            serviceChannel.Dispose();


            // Checks
            testInstance.ShouldHaveOne("ERROR", "ArgumentException/Exception in IncomingObserver");
        }

        [TestMethod]
        public void Should_Send_And_Receive_Small_Message_And_Invoke_Dispatch()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage1>(testInstance);

            var serviceChannel = helper.CreateServiceChannel();
            var consumerChannel = helper.CreateRequestChannel();

            TestMessage1 receivedMessage = null;
            helper.RegisterTestDispatch(serviceChannel, x =>
            {
                receivedMessage = x;
            });
            helper.ServiceStartReceiving(serviceChannel);

            var testMessage = new TestMessage1
            {
                IntProp = 666,
                StringProp = "Hello Hell"
            };
            consumerChannel.SendOneWay(testMessage, helper.Log);

            helper.WaitForServiceDispatch();

            var serviceQueue = helper.GetQueue(helper.ServiceTopic);

            consumerChannel.Dispose();
            serviceChannel.Dispose();


            // Checks
            Assert.IsNotNull(receivedMessage);

            Assert.AreEqual(testMessage.IntProp, receivedMessage.IntProp);
            Assert.AreEqual(testMessage.StringProp, receivedMessage.StringProp);
            Assert.AreEqual(typeof(TestMessage1).AssemblyQualifiedName, serviceQueue[0].ContentType);
            Assert.AreEqual(1, helper.MessagesSent);
            Assert.AreEqual(1, helper.MessagesReceived);
            Assert.AreEqual(1, helper.MessagesDispatched);
            Assert.AreEqual(0, testInstance.BlobGets);
            Assert.AreEqual(0, testInstance.BlobPuts);
            Assert.AreEqual(0, testInstance.BlobStorage.Count);
        }

        [TestMethod]
        public void Should_Send_And_Receive_Large_Message_And_Invoke_Dispatch()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage1>(testInstance);

            var serviceChannel = helper.CreateServiceChannel();

            TestMessage1 receivedMessage = null;
            helper.RegisterTestDispatch(serviceChannel, x =>
            {
                receivedMessage = x;
            });
            helper.ServiceStartReceiving(serviceChannel);


            var consumerChannel = helper.CreateRequestChannel();
            var testMessage = new TestMessage1
            {
                IntProp = 666,
                StringProp = "Hello Hell"
            };
            testMessage.Data = new byte[100 * 1024];
            var randomReq = new Random();
            randomReq.NextBytes(testMessage.Data);

            consumerChannel.SendOneWay(testMessage, helper.Log);


            helper.WaitForServiceDispatch();

            var serviceQueue = helper.GetQueue(helper.ServiceTopic);

            consumerChannel.Dispose();
            serviceChannel.Dispose();


            // Checks
            Assert.IsNotNull(receivedMessage);

            Assert.AreEqual(testMessage.IntProp, receivedMessage.IntProp);
            Assert.AreEqual(testMessage.StringProp, receivedMessage.StringProp);
            Assert.AreEqual(typeof(TestMessage1).AssemblyQualifiedName, serviceQueue[0].ContentType);
            Assert.AreEqual(1, helper.MessagesSent);
            Assert.AreEqual(1, helper.MessagesReceived);
            Assert.AreEqual(1, helper.MessagesDispatched);
            Assert.AreEqual(1, testInstance.BlobGets);
            Assert.AreEqual(1, testInstance.BlobPuts);
            Assert.AreEqual(1, testInstance.BlobStorage.Count);
        }

        [TestMethod]
        public void Should_Send_And_Receive_Large_Message_Using_SendAndWait()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage1>(testInstance);

            var serviceChannel = helper.CreateServiceChannel();

            TestMessage1 receivedMessage = null;
            helper.RegisterTestDispatch<TestMessage2>(serviceChannel, x =>
            {
                receivedMessage = x;

                var response = new TestMessage2
                {
                    IntProp = 42,
                    StringProp = "Response"
                };

                return response;
            });
            helper.ServiceStartReceiving(serviceChannel);


            var consumerChannel = helper.CreateRequestChannel();
            var testMessage = new TestMessage1
            {
                IntProp = 666,
                StringProp = "Hello Hell"
            };
            testMessage.Data = new byte[100 * 1024];
            var randomReq = new Random();
            randomReq.NextBytes(testMessage.Data);

            var testMessage2 = consumerChannel.SendWaitResponse<TestMessage2>(testMessage, TimeSpan.FromSeconds(5), helper.Log);

            var serviceQueue = helper.GetQueue(helper.ServiceTopic);

            consumerChannel.Dispose();
            serviceChannel.Dispose();


            // Checks
            Assert.IsNotNull(receivedMessage);

            Assert.AreEqual(testMessage.IntProp, receivedMessage.IntProp);
            Assert.AreEqual(testMessage.StringProp, receivedMessage.StringProp);
            Assert.AreEqual(typeof(TestMessage1).AssemblyQualifiedName, serviceQueue[0].ContentType);
            Assert.AreEqual(2, helper.MessagesSent);
            Assert.AreEqual(2, helper.MessagesReceived);
            Assert.AreEqual(1, helper.MessagesDispatched);
            Assert.AreEqual(1, testInstance.BlobGets);
            Assert.AreEqual(1, testInstance.BlobPuts);
            Assert.AreEqual(1, testInstance.BlobStorage.Count);
        }

        [TestMethod]
        public void Should_Send_And_Receive_Small_Message_Using_SendAndWait()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage1>(testInstance);

            var serviceChannel = helper.CreateServiceChannel();

            TestMessage1 receivedMessage = null;
            helper.RegisterTestDispatch<TestMessage2>(serviceChannel, x =>
            {
                receivedMessage = x;

                var response = new TestMessage2
                {
                    IntProp = 42,
                    StringProp = "Response"
                };

                return response;
            });
            helper.ServiceStartReceiving(serviceChannel);


            var consumerChannel = helper.CreateRequestChannel();
            var testMessage = new TestMessage1
            {
                IntProp = 666,
                StringProp = "Hello Hell"
            };
            var testMessage2 = consumerChannel.SendWaitResponse<TestMessage2>(testMessage, TimeSpan.FromSeconds(10), helper.Log);

            var serviceQueue = helper.GetQueue(helper.ServiceTopic);

            consumerChannel.Dispose();
            serviceChannel.Dispose();


            // Checks
            Assert.IsNotNull(receivedMessage);

            Assert.AreEqual(testMessage.IntProp, receivedMessage.IntProp);
            Assert.AreEqual(testMessage.StringProp, receivedMessage.StringProp);
            Assert.AreEqual(typeof(TestMessage1).AssemblyQualifiedName, serviceQueue[0].ContentType);
            Assert.AreEqual(2, helper.MessagesSent);
            Assert.AreEqual(2, helper.MessagesReceived);
            Assert.AreEqual(1, helper.MessagesDispatched);
            Assert.AreEqual(0, testInstance.BlobGets);
            Assert.AreEqual(0, testInstance.BlobPuts);
            Assert.AreEqual(0, testInstance.BlobStorage.Count);
        }

        [TestMethod]
        public void Should_Send_And_Timeout()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage1>(testInstance);

            var serviceChannel = helper.CreateServiceChannel();

            TestMessage1 receivedMessage = null;
            helper.RegisterTestDispatch<TestMessage2>(serviceChannel, x =>
            {
                receivedMessage = x;

                var response = new TestMessage2
                {
                    IntProp = 42,
                    StringProp = "Response"
                };

                Thread.Sleep(300);
                return response;
            });
            helper.ServiceStartReceiving(serviceChannel);


            var consumerChannel = helper.CreateRequestChannel();
            var testMessage = new TestMessage1
            {
                IntProp = 666,
                StringProp = "Hello Hell"
            };
            try
            {
                var testMessage2 = consumerChannel.SendWaitResponse<TestMessage2>(testMessage, TimeSpan.FromSeconds(0.2), helper.Log);
                Assert.Fail("Should throw TimeoutException");
            }
            catch (TimeoutException)
            {
            }
        }

        [TestMethod]
        public void Should_Receive_Small_Message_And_Throw_System_Exception_Back_To_Consumer()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage1>(testInstance);

            var serviceChannel = helper.CreateServiceChannel();

            TestMessage1 receivedMessage = null;
            helper.RegisterTestDispatch<TestMessage2>(serviceChannel, x =>
            {
                receivedMessage = x;

                throw new ArgumentException("Test exception");
            });
            helper.ServiceStartReceiving(serviceChannel);


            var consumerChannel = helper.CreateRequestChannel();
            var testMessage = new TestMessage1
            {
                IntProp = 666,
                StringProp = "Hello Hell"
            };
            try
            {
                var testMessage2 = consumerChannel.SendWaitResponse<TestMessage2>(testMessage, TimeSpan.FromSeconds(10), helper.Log);
                Assert.Fail("Should throw ArgumentException");
            }
            catch (ArgumentException)
            {
            }
        }

        [TestMethod]
        public void Should_Receive_Small_Message_And_Throw_Custom_Exception_Back_To_Consumer()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage1>(testInstance);

            var serviceChannel = helper.CreateServiceChannel();

            TestMessage1 receivedMessage = null;
            helper.RegisterTestDispatch<TestMessage2>(serviceChannel, x =>
            {
                receivedMessage = x;

                throw new TestException("Test exception");
            });
            helper.ServiceStartReceiving(serviceChannel);


            var consumerChannel = helper.CreateRequestChannel();
            var testMessage = new TestMessage1
            {
                IntProp = 666,
                StringProp = "Hello Hell"
            };
            try
            {
                var testMessage2 = consumerChannel.SendWaitResponse<TestMessage2>(testMessage, TimeSpan.FromSeconds(10), helper.Log);
                Assert.Fail("Should throw TestException");
            }
            catch (TestException)
            {
            }
        }

        [TestMethod]
        public void Should_Receive_Small_Message_And_Throw_Invalid_Exception_Back_To_Consumer()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage1>(testInstance);

            var serviceChannel = helper.CreateServiceChannel();

            TestMessage1 receivedMessage = null;
            helper.RegisterTestDispatch<TestMessage2>(serviceChannel, x =>
            {
                receivedMessage = x;

                throw new InvalidTestException("Test exception");
            });
            helper.ServiceStartReceiving(serviceChannel);


            var consumerChannel = helper.CreateRequestChannel();
            var testMessage = new TestMessage1
            {
                IntProp = 666,
                StringProp = "Hello Hell"
            };
            try
            {
                var testMessage2 = consumerChannel.SendWaitResponse<TestMessage2>(testMessage, TimeSpan.FromSeconds(10), helper.Log);
                Assert.Fail("Should throw ServiceException");
            }
            catch (ServiceException)
            {
            }
        }

        [TestMethod]
        public void Should_Receive_Small_Message_And_Throw_Invalid2_Exception_Back_To_Consumer()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage1>(testInstance);

            var serviceChannel = helper.CreateServiceChannel();

            TestMessage1 receivedMessage = null;
            helper.RegisterTestDispatch<TestMessage2>(serviceChannel, x =>
            {
                receivedMessage = x;

                throw new InvalidTestException2("Test exception");
            });
            helper.ServiceStartReceiving(serviceChannel);


            var consumerChannel = helper.CreateRequestChannel();
            var testMessage = new TestMessage1
            {
                IntProp = 666,
                StringProp = "Hello Hell"
            };
            try
            {
                var testMessage2 = consumerChannel.SendWaitResponse<TestMessage2>(testMessage, TimeSpan.FromSeconds(10), helper.Log);
                Assert.Fail("Should throw ServiceException");
            }
            catch (ServiceException)
            {
            }
        }
    }
}
