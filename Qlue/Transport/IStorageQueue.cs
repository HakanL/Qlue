using System;

namespace Qlue.Transport
{
    public interface IStorageQueue
    {
        IStorageQueueMessage GetMessage(TimeSpan visibilityTimeout);

        string QueueName { get; }
    }
}
