using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Qlue.Transport;
using System.Threading.Tasks;
using System.IO;

namespace Qlue.Tests
{
    [TestClass]
    public class General
    {
        [TestMethod]
        public void CloudCredentials_Should_Return_Valid_Connection_String()
        {
            var testInstance = new Plumbing.TestInstance();

            var helper = new Plumbing.Helper<object>(testInstance);

            var mockCloudCredentials = helper.WireupCloudCredentials();

            var cloudCredentials = new CloudCredentials
            {
                ServiceNamespace = "NAMESPACE",
                IssuerName = "OWNER",
                IssuerSecret = "U2VjcmV0",
                StorageAccountName = "ACCOUNTNAME",
                StorageAccountSecret = "QWNjb3VudFNlY3JldA=="
            };

            Assert.AreEqual("Endpoint=sb://NAMESPACE.servicebus.windows.net/;SharedSecretIssuer=OWNER;SharedSecretValue=U2VjcmV0",
                cloudCredentials.GetServiceBusConnectionString());
        }
    }
}
