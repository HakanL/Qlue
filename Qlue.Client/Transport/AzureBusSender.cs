using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Qlue;
using Qlue.Pipeline;
using Qlue.Logging;
using System.Globalization;

namespace Qlue.Transport
{
    public class AzureBusSender : IBusSender
    {
        private TopicClient topicClient;
        private readonly string destinationTopic;
        private readonly string sessionId;
        private readonly ILog log;

        public AzureBusSender(ILog log, TopicClient topicClient, string destinationTopic, string sessionId)
        {
            this.topicClient = topicClient;
            this.destinationTopic = destinationTopic;
            this.sessionId = sessionId;
            this.log = log;
        }

        private BrokeredMessage CreateBrokeredMessage(PipelineContext context)
        {
            var brokMsg = new BrokeredMessage(context.Payload, true);
            brokMsg.MessageId = context.MessageId;
            brokMsg.ContentType = context.ContentType;
            brokMsg.ReplyTo = context.From;
            brokMsg.CorrelationId = context.RelatesTo;
            brokMsg.To = this.destinationTopic;
            brokMsg.SessionId = this.sessionId;
            switch (context.Type)
            {
                case PipelineContext.MessageType.Response:
                    brokMsg.Label = string.Format(CultureInfo.InvariantCulture, "Response-{0}", context.SessionId);
                    break;

                case PipelineContext.MessageType.Request:
                    brokMsg.Label = "Request";
                    break;

                case PipelineContext.MessageType.Notify:
                    brokMsg.Label = "Notify";
                    break;
            }

            // Copy properties
            foreach (var kvp in context.Properties)
                brokMsg.Properties[kvp.Key] = kvp.Value;

            if (!string.IsNullOrEmpty(context.CustomSessionId))
                brokMsg.Properties["CustomSessionId"] = context.CustomSessionId;
            if (!string.IsNullOrEmpty(context.Version))
                brokMsg.Properties["Version"] = context.Version;

            return brokMsg;
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
                if (this.topicClient != null)
                {
                    this.topicClient.Close();
                    this.topicClient = null;
                }
            }
        }

        public async Task<bool> SendAsync(PipelineContext context)
        {
            var brokMsg = CreateBrokeredMessage(context);

            try
            {
                await this.topicClient.SendAsync(brokMsg);

                return true;
            }
            catch (Microsoft.ServiceBus.Messaging.MessagingException ex)
            {
                if (ex.IsTransient)
                {
                    this.log.Warn("Transient messaging exception in ReceiveCallback: {0}", ex.Message);

                    return false;
                }

                throw;
            }
        }
    }
}
