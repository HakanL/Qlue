using System;
using System.IO;
using System.Threading.Tasks;
using Qlue.Logging;

namespace Qlue.Pipeline.Component
{
    public class Serialize : IPipelineComponent
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        private ILog log;

        private IMessageSerializer serializer;

        public Serialize(ILog log, IMessageSerializer serializer)
        {
            this.log = log;
            this.serializer = serializer;
        }

        public Task Execute(PipelineContext context)
        {
            var target = new MemoryStream();
            this.serializer.Serialize(context.Body, target);
            target.Seek(0, SeekOrigin.Begin);

            context.Body = null;

            context.Payload = target;
            context.ContentType = context.BodyType.AssemblyQualifiedName;

#if FULL_LOGGING
            log.Debug("Serialize type {0} size {1:N1}kB", context.BodyType.Name, context.Payload.Length / 1024.0);
#endif

            return Task.FromResult(false);
        }
    }
}
