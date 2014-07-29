using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Qlue.Transport
{
    public class AzureStorageQueueClient : IStorageQueueClient
    {
        private CloudQueueClient queueClient;

        public AzureStorageQueueClient(ICloudCredentials cloudCredentials)
        {
            var storageAccount = GetCloudStorageAccount(cloudCredentials);
            this.queueClient = storageAccount.CreateCloudQueueClient();
        }

        private static CloudStorageAccount GetCloudStorageAccount(ICloudCredentials cloudCredentials)
        {
            var secret = Convert.FromBase64String(cloudCredentials.StorageAccountSecret);
            var storageCredentials = new StorageCredentials(cloudCredentials.StorageAccountName, secret);

            return new CloudStorageAccount(storageCredentials, true);
        }

        public IStorageQueue GetQueueReference(string queueName)
        {
            CloudQueue queue = this.queueClient.GetQueueReference(queueName);

            queue.CreateIfNotExists();

            return new AzureStorageQueue(queue);
        }
    }
}
