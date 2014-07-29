using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Qlue.Logging;
using Qlue.Transport;

namespace Qlue.Processor
{
    public abstract class RobustQueueProcessor<T> : IDisposable
    {
        private readonly TimeSpan PollingDelay = TimeSpan.FromSeconds(5);
        private readonly TimeSpan VisibilityTimeout = TimeSpan.FromMinutes(5);
        private const int MaxRetries = 3;

        private IStorageQueue storageQueue;
        private ILog log;
        private CancellationTokenSource cancellationTokenSource;
        private Dictionary<string, IStorageQueueMessage> activeMessages;

        protected RobustQueueProcessor(
            ILog log,
            IStorageQueue storageQueue)
        {
            this.log = log;
            this.storageQueue = storageQueue;

            this.cancellationTokenSource = new CancellationTokenSource();
            this.activeMessages = new Dictionary<string, Transport.IStorageQueueMessage>();
        }

        public void Start()
        {
            var cancellationToken = this.cancellationTokenSource.Token;

            Task.Run(() => QueueWorker(cancellationToken), cancellationToken);

            Task.Run(() =>
            {
                // Update visiblity on messages that we are currently processing
                // If we get killed then the messages will automatically get put back in
                // the queue within 5 minutes. But if we still process for a long time
                // then we're keep pinging the messages forever.
                while (!cancellationToken.IsCancellationRequested)
                {
                    lock (this.activeMessages)
                    {
                        foreach (var message in this.activeMessages.Values)
                        {
                            try
                            {
                                message.UpdateVisibilityTimeout(VisibilityTimeout);
                            }
                            catch (Exception ex)
                            {
                                this.log.ErrorException(ex, "Failed to update visibility on message id {0}", message.Id);
                            }
                        }
                    }

                    cancellationToken.WaitHandle.WaitOne(TimeSpan.FromTicks(VisibilityTimeout.Ticks / 2));
                }
            }, cancellationToken);
        }

        public void Stop()
        {
            this.cancellationTokenSource.Cancel();
        }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Call Stop if it hasn't stopped yet
                Stop();

                if (this.cancellationTokenSource != null)
                {
                    this.cancellationTokenSource.Dispose();
                    this.cancellationTokenSource = null;
                }
            }
        }

        private void QueueWorker(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var queueMessage = this.storageQueue.GetMessage(VisibilityTimeout);

                    if (queueMessage == null)
                    {
                        // Sleep
                        cancellationToken.WaitHandle.WaitOne(PollingDelay);

                        continue;
                    }

                    if (queueMessage.DequeueCount > MaxRetries)
                    {
                        // Only retry X times
                        queueMessage.Delete();
                        continue;
                    }

                    this.log.Trace("Received message id {0}, dequeue count {1}", queueMessage.Id, queueMessage.DequeueCount);

                    lock (this.activeMessages)
                    {
                        if (this.activeMessages.ContainsKey(queueMessage.Id))
                            // Skip messages that are already active
                            continue;

                        this.activeMessages.Add(queueMessage.Id, queueMessage);
                    }

                    T message;
                    try
                    {
                        message = Deserialize(queueMessage);
                    }
                    catch
                    {
                        // Failed to deserialize, remove from queue
                        queueMessage.Delete();
                        throw;
                    }

                    // Check if we should process the message
                    if (!ShouldProcessMessage(message))
                    {
                        // Remove from queue
                        queueMessage.Delete();
                        continue;
                    }

                    // Invoke
                    Task.Run<bool>(async () => await MessageHandler(message))
                        .ContinueWith(t =>
                            {
                                lock (this.activeMessages)
                                    this.activeMessages.Remove(queueMessage.Id);

                                MessageCompleted(message);

                                if (t.IsFaulted)
                                {
                                    t.Exception.Handle(ex =>
                                    {
                                        this.log.ErrorException("Error while invoking message handler for RobustQueueProcessor", ex);

                                        return false;
                                    });

                                    // Remove from queue
                                    queueMessage.Delete();
                                }
                                else
                                {
                                    if (t.Result)
                                    {
                                        // Remove from queue
                                        queueMessage.Delete();
                                    }
                                }
                            });
                }
                catch (Exception ex)
                {
                    this.log.ErrorException("Exception in RobustQueueProcessor/QueueWorker", ex);
                }
            }
        }

        /// <summary>
        /// Deserialize the IStorageQueueMessage into type T
        /// </summary>
        /// <param name="message">The incoming message</param>
        /// <returns>The deserialized message</returns>
        protected abstract T Deserialize(IStorageQueueMessage message);

        /// <summary>
        /// Called before a message is processed to determine if it should be processed or not
        /// </summary>
        /// <param name="message">The message</param>
        /// <returns>True if message should be processed/invoked</returns>
        protected virtual bool ShouldProcessMessage(T message)
        {
            return true;
        }

        /// <summary>
        /// Message either failed or was successful
        /// </summary>
        /// <param name="message">The message</param>
        protected virtual void MessageCompleted(T message)
        {
        }

        /// <summary>
        /// Main message handler
        /// </summary>
        /// <param name="message">The message</param>
        /// <returns>True if message processed successfully. False if we should retry the message</returns>
        protected abstract Task<bool> MessageHandler(T message);
    }
}
