using System;

namespace Qlue
{
    public interface ICloudCredentials
    {
        string ServiceNamespace { get; }

        string IssuerName { get; }

        string IssuerSecret { get; }

        string StorageAccountName { get; }

        string StorageAccountSecret { get; }

        string GetServiceBusConnectionString();
    }
}
