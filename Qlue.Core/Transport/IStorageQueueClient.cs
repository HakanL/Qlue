using System;

namespace Qlue.Transport
{
    public interface IStorageQueueClient
    {
        IStorageQueue GetQueueReference(string queueName);
    }
}
