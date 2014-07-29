using System;
using System.Threading.Tasks;
using Qlue.Logging;

namespace Qlue.Pipeline.Component
{
    public class Deserialize : IPipelineComponent
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        private ILog log;

        private IMessageSerializer serializer;

        public Deserialize(ILog log, IMessageSerializer serializer)
        {
            this.log = log;
            this.serializer = serializer;
        }

        public Task Execute(PipelineContext context)
        {
            context.BodyType = Type.GetType(context.ContentType, true);

#if FULL_LOGGING
            this.log.Debug("Deserialize type {0} size {1:N1}kB", context.BodyType.Name, context.Payload.Length / 1024.0);
#endif

            context.Body = this.serializer.Deserialize(context.Payload, context.BodyType);

            context.Payload.Dispose();
            context.Payload = null;

            return Task.FromResult(false);
        }
    }
}
