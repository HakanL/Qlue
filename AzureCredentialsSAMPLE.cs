using System;

namespace Qlue.Shared
{
    internal static class AzureCredentials
    {
        public static CloudCredentials GetCloudCredentials()
        {
            return new CloudCredentials
            {
                ServiceNamespace = "[SERVICEBUSNAMESPACE]",
                IssuerName = "owner",
                IssuerSecret = "[SERVICEBUSSECRET]",
                StorageAccountName = "[STORAGEACCOUNTNAME]",
                StorageAccountSecret = "[STORAGESECRET]"
            };
        }
    }
}
