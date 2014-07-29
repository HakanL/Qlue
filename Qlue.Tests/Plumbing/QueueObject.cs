using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Qlue.Transport;

namespace Qlue.Tests.Plumbing
{
    internal class QueueObject
    {
        public byte[] Payload;
        public Dictionary<string, string> Properties = new Dictionary<string, string>();
        public string ContentType { get; set; }
        public string MessageId { get; set; }
        public string From { get; set; }
        public string RelatesTo { get; set; }
        public string SessionId { get; set; }


        public QueueObject(Pipeline.PipelineContext context)
        {
            if (context.Payload != null)
            {
                Payload = new byte[context.Payload.Length];
                context.Payload.Read(Payload, 0, (int)context.Payload.Length);
            }

            ContentType = context.ContentType;
            MessageId = context.MessageId;
            From = context.From;
            RelatesTo = context.RelatesTo;
            SessionId = context.SessionId;

            foreach (var kvp in context.Properties)
                Properties[kvp.Key] = kvp.Value;
        }

        public Stream GetStreamFromPayload()
        {
            if (Payload == null)
                return null;

            var stream = new MemoryStream(Payload.Length);
            stream.Write(Payload, 0, Payload.Length);

            stream.Seek(0, SeekOrigin.Begin);

            return stream;
        }
    }
}
