using System;
using System.Threading.Tasks;
using Qlue.Logging;
using Qlue.Transport;

namespace Qlue.Pipeline.Component
{
    internal class MessageSender : IDisposable, IPipelineComponent
    {
        private const int maxRetries = 3;

        private ILog log;
        private readonly string destinationTopic;
        private int nextSender = -1;
        private readonly IBusSender[] sendClients;

        public MessageSender(ILog log, string destinationTopic, int connections, string sessionId, IBusTransport busTransport)
        {
            this.log = log;

            this.destinationTopic = destinationTopic + busTransport.TopicSuffix;

            this.log.Info("Creating BusSender for topic '{0}'", this.destinationTopic);

            this.sendClients = new IBusSender[connections];
            for (int i = 0; i < connections; i++)
            {
                this.sendClients[i] = busTransport.CreateBusSender(this.destinationTopic, sessionId);
            }
        }

        public void Dispose()
        {
            foreach (var client in this.sendClients)
            {
                try
                {
                    client.Dispose();
                }
                catch (Exception)
                {
                    // Ignore shutdown exceptions
                }
            }
        }

        private IBusSender GetTopicClient()
        {
            int sender = System.Threading.Interlocked.Increment(ref this.nextSender);

            return this.sendClients[sender % this.sendClients.Length];
        }

        public async Task Execute(PipelineContext context)
        {
#if FULL_LOGGING
            var watch = System.Diagnostics.Stopwatch.StartNew();
            this.log.Trace("Send message id {0}", context.MessageId);
#endif

            var topicClient = GetTopicClient();

#if FULL_LOGGING
            this.log.Trace("Got topic client for sending to {0}", this.destinationTopic);
#endif

            bool successful = false;
            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    bool result = await topicClient.SendAsync(context)
                        .ConfigureAwait(false);

                    if (result)
                    {
                        successful = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    throw new BusSendException("Failed to send message", ex);
                }

                System.Threading.Thread.Sleep(1000 + 1000 * retry);
            }

            if (!successful)
                throw new BusSendException(string.Format("Failed to send message within {0} retries", maxRetries));

#if FULL_LOGGING
            watch.Stop();
            this.log.Trace("Completed send for message id {0}, duration {1:N} ms", context.MessageId, watch.ElapsedMilliseconds);
#endif
        }
    }
}
