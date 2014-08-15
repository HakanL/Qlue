using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Qlue.Pipeline
{
    public class PipelineContext
    {
        public enum MessageType
        {
            Unknown = 0,
            Request,
            Response,
            Notify
        }

        public object BusObject { get; set; }

        public string MessageId { get; private set; }

        public string RelatesTo { get; private set; }

        public string From { get; private set; }

        public object Body { get; set; }

        public Type BodyType { get; set; }

        public string ContentType { get; set; }

        public Stream Payload { get; set; }

        public MessageType Type { get; private set; }

        public string SessionId { get; private set; }

        public string CustomSessionId { get; private set; }

        public Dictionary<string, string> Properties { get; private set; }

        public string Version { get; private set; }

        private PipelineContext()
        {
            this.Properties = new Dictionary<string, string>();
        }

        private PipelineContext(Dictionary<string, string> properties)
        {
            this.Properties = properties;
        }

        public static PipelineContext CreateFromInboundMessage(Stream payload, string contentType, string messageId, string from,
            string relatesTo, string sessionId, object busObject, string customSessionId, string version, Dictionary<string, string> properties)
        {
            var context = new PipelineContext(properties);

            context.Payload = payload;
            context.ContentType = contentType;
            context.MessageId = messageId;
            context.From = from;
            context.RelatesTo = relatesTo;
            context.SessionId = sessionId;
            context.BusObject = busObject;
            context.CustomSessionId = customSessionId;
            context.Version = version;

            return context;
        }

        public static PipelineContext CreateFromRequest(string from, object request, string customSessionId, string version)
        {
            var context = new PipelineContext();

            context.Body = request;
            context.BodyType = (request != null) ? request.GetType() : typeof(object);
            context.MessageId = Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
            context.From = from;
            context.Type = MessageType.Request;
            context.CustomSessionId = customSessionId;
            context.Version = version;

            return context;
        }

        public static PipelineContext CreateFromResponse(string from, string relatesTo, string sessionId, object response)
        {
            var context = new PipelineContext();

            context.Body = response;
            context.BodyType = (response != null) ? response.GetType() : typeof(object);
            context.MessageId = Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
            context.From = from;
            context.RelatesTo = relatesTo;
            context.SessionId = sessionId;
            context.Type = MessageType.Response;

            return context;
        }

        public static PipelineContext CreateFromNotify(string from, string sessionId, object response, string version)
        {
            var context = new PipelineContext();

            context.Body = response;
            context.BodyType = (response != null) ? response.GetType() : typeof(object);
            context.MessageId = Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
            context.From = from;
            context.SessionId = sessionId;
            context.Type = MessageType.Notify;
            context.Version = version;

            return context;
        }
    }
}
