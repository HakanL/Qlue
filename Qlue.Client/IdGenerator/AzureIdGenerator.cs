using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;

// Uses SnowMaker from https://github.com/tathamoddie/SnowMaker

namespace Qlue
{
    public class AzureIdGenerator : IIdGenerator
    {
        private SnowMaker.UniqueIdGenerator generator;

        public AzureIdGenerator(ICloudCredentials credentials)
            : this(credentials, "qlue-id-generator")
        {
        }

        public AzureIdGenerator(ICloudCredentials credentials, string containerName)
        {
            var storageAccount = GetCloudStorageAccount(credentials);
            var blobStorage = new SnowMaker.BlobOptimisticDataStore(storageAccount, containerName);
            this.generator = new SnowMaker.UniqueIdGenerator(blobStorage);
        }

        private static CloudStorageAccount GetCloudStorageAccount(ICloudCredentials cloudCredentials)
        {
            var storageCredentials = new StorageCredentials(cloudCredentials.StorageAccountName, cloudCredentials.StorageAccountSecret);

            return new CloudStorageAccount(storageCredentials, true);
        }

        public long GetNextId(string scopeName)
        {
            return this.generator.NextId(scopeName);
        }
    }
}
