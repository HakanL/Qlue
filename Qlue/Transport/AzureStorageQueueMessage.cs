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
    public class AzureStorageQueueMessage : IStorageQueueMessage
    {
        private CloudQueue queue;
        private CloudQueueMessage message;

        internal AzureStorageQueueMessage(CloudQueue queue, CloudQueueMessage message)
        {
            this.queue = queue;
            this.message = message;
        }

        public void Delete()
        {
            try
            {
                this.queue.DeleteMessage(this.message);
            }
            catch (StorageException)
            {
                // Ignore
            }
        }

        public string Id
        {
            get
            {
                return this.message.Id;
            }
        }

        public byte[] AsBytes()
        {
            return this.message.AsBytes;
        }

        public string AsString()
        {
            return this.message.AsString;
        }

        public int DequeueCount
        {
            get { return this.message.DequeueCount; }
        }

        public void UpdateVisibilityTimeout(TimeSpan visibilityTimeout)
        {
            try
            {
                this.queue.UpdateMessage(this.message, visibilityTimeout, MessageUpdateFields.Visibility);
            }
            catch (StorageException)
            {
                // Ignore
            }
        }
    }
}
