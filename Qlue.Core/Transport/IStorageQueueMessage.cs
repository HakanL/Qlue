using System;

namespace Qlue.Transport
{
    public interface IStorageQueueMessage
    {
        string Id { get; }

        byte[] AsBytes();

        string AsString();

        void Delete();

        int DequeueCount { get; }

        void UpdateVisibilityTimeout(TimeSpan visibilityTimeout);
    }
}
