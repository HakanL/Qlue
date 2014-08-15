using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Qlue.Transport
{
    public class AzureStorageQueue : IStorageQueue
    {
        private CloudQueue cloudQueue;

        internal AzureStorageQueue(CloudQueue cloudQueue)
        {
            this.cloudQueue = cloudQueue;
        }

        public IStorageQueueMessage GetMessage(TimeSpan visibilityTimeout)
        {
            CloudQueueMessage message = this.cloudQueue.GetMessage(visibilityTimeout);

            if (message == null)
                return null;

            return new AzureStorageQueueMessage(this.cloudQueue, message);
        }

        public string QueueName
        {
            get { return this.cloudQueue.Name; }
        }
    }
}
