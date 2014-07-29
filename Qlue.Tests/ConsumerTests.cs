using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Qlue.Transport;
using System.Threading.Tasks;
using System.IO;
using System.Linq;

namespace Qlue.Tests
{
    [TestClass]
    public class Consumer
    {
        [TestMethod]
        public void Should_Send_Small_OneWay_Message_And_Deserialize_It_Back()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage1>(testInstance);
            var consumerChannel = helper.CreateRequestChannel();


            var testMessage = new TestMessage1
            {
                IntProp = 666,
                StringProp = "Hello Hell"
            };

            consumerChannel.SendOneWay(testMessage, helper.Log);

            consumerChannel.Dispose();


            // Checks
            var srvQueue = helper.GetQueue(helper.ServiceTopic);
            var queueObject = srvQueue[0];
            var testBody = helper.ExecutePipelineFromQueueObject(queueObject);

            Assert.AreEqual(testMessage.IntProp, testBody.IntProp);
            Assert.AreEqual(testMessage.StringProp, testBody.StringProp);
            Assert.AreEqual(typeof(TestMessage1).AssemblyQualifiedName, queueObject.ContentType);
            Assert.AreEqual(1, srvQueue.Count);
            Assert.AreEqual(0, testInstance.BlobGets);
            Assert.AreEqual(0, testInstance.BlobPuts);
            Assert.AreEqual(0, testInstance.BlobStorage.Count);
        }

        [TestMethod]
        public void Should_Send_Large_Highly_Compressability_OneWay_Message_And_Deserialize_It_Back()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage1>(testInstance);
            var consumerChannel = helper.CreateRequestChannel();


            var testMessage = new TestMessage1
            {
                IntProp = 666,
                StringProp = "Hello Hell"
            };
            testMessage.Data = new byte[100 * 1024];

            consumerChannel.SendOneWay(testMessage, helper.Log);

            consumerChannel.Dispose();


            // Checks
            var srvQueue = helper.GetQueue(helper.ServiceTopic);
            var queueObject = srvQueue[0];
            var testBody = helper.ExecutePipelineFromQueueObject(queueObject);

            Assert.AreEqual(testMessage.IntProp, testBody.IntProp);
            Assert.AreEqual(testMessage.StringProp, testBody.StringProp);
            Assert.AreEqual(typeof(TestMessage1).AssemblyQualifiedName, queueObject.ContentType);
            Assert.AreEqual(1, srvQueue.Count);
            Assert.AreEqual(0, testInstance.BlobGets);
            Assert.AreEqual(0, testInstance.BlobPuts);
            Assert.AreEqual(0, testInstance.BlobStorage.Count);
            Assert.IsTrue(queueObject.Payload.Length < 5000);
        }

        [TestMethod]
        public void Should_Send_Large_Highly_Compressability_OneWay_Message_And_Deserialize_It_Back_With_Retries()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage1>(testInstance);
            var consumerChannel = helper.CreateRequestChannel(successEvery: 2);

            var testMessage = new TestMessage1
            {
                IntProp = 666,
                StringProp = "Hello Hell"
            };
            testMessage.Data = new byte[100 * 1024];

            consumerChannel.SendOneWay(testMessage, helper.Log);

            consumerChannel.Dispose();


            // Checks
            var srvQueue = helper.GetQueue(helper.ServiceTopic);
            var queueObject = srvQueue[0];
            var testBody = helper.ExecutePipelineFromQueueObject(queueObject);

            Assert.AreEqual(testMessage.IntProp, testBody.IntProp);
            Assert.AreEqual(testMessage.StringProp, testBody.StringProp);
            Assert.AreEqual(typeof(TestMessage1).AssemblyQualifiedName, queueObject.ContentType);
            Assert.AreEqual(2, srvQueue.Count);
            Assert.AreEqual(0, testInstance.BlobGets);
            Assert.AreEqual(0, testInstance.BlobPuts);
            Assert.AreEqual(0, testInstance.BlobStorage.Count);
            Assert.IsTrue(queueObject.Payload.Length < 5000);
        }

        [TestMethod]
        [ExpectedException(typeof(AggregateException))]
        public void Should_Fail_The_Send_On_Retries()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage1>(testInstance);
            var consumerChannel = helper.CreateRequestChannel(successEvery: 4);

            var testMessage = new TestMessage1
            {
                IntProp = 666,
                StringProp = "Hello Hell"
            };
            testMessage.Data = new byte[100];

            consumerChannel.SendOneWay(testMessage, helper.Log);
        }

        [TestMethod]
        public void Should_Send_Large_Low_Compressability_OneWay_Message_And_Deserialize_It_Back()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<TestMessage1>(testInstance);
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

            consumerChannel.Dispose();


            // Checks
            var srvQueue = helper.GetQueue(helper.ServiceTopic);
            var queueObject = srvQueue[0];
            var blobObject = testInstance.BlobStorage.Values.First();
            var testBody = helper.ExecutePipelineFromQueueObject(queueObject);

            Assert.AreEqual(testMessage.IntProp, testBody.IntProp);
            Assert.AreEqual(testMessage.StringProp, testBody.StringProp);
            Assert.AreEqual(typeof(TestMessage1).AssemblyQualifiedName, queueObject.ContentType);
            Assert.AreEqual(1, srvQueue.Count);
            // By the fake pipeline
            Assert.AreEqual(1, testInstance.BlobGets);
            Assert.AreEqual(1, testInstance.BlobPuts);
            Assert.AreEqual(1, testInstance.BlobStorage.Count);
            Assert.IsTrue(blobObject.Length > 50000);
            Assert.IsNull(queueObject.Payload);
        }
    }
}
