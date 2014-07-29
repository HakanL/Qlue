using System;
using System.Globalization;

namespace Qlue
{
    public class CloudCredentials : ICloudCredentials
    {
        public string ServiceNamespace { get; set; }

        public string IssuerName { get; set; }

        public string IssuerSecret { get; set; }

        public string StorageAccountName { get; set; }

        public string StorageAccountSecret { get; set; }

        public string GetServiceBusConnectionString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}={1}://{2}.{3}/;{4}={5};{6}={7}", new object[]
				{
					"Endpoint",
					"sb",
					this.ServiceNamespace,
					"servicebus.windows.net",
					"SharedSecretIssuer",
					this.IssuerName,
					"SharedSecretValue",
					this.IssuerSecret
				});
        }
    }
}
