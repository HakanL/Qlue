using System;
using System.Globalization;
using System.Threading.Tasks;
using Qlue.Logging;
using Qlue.Transport;

namespace Qlue.Pipeline.Component
{
    public class OverflowPut : IPipelineComponent
    {
        private ILog log;
        private IBlobContainer container;

        public OverflowPut(ILog log, IBlobContainer container)
        {
            this.log = log;
            this.container = container;
        }

        public async Task Execute(PipelineContext context)
        {
            if (context.Payload.Length > 65535)
            {
                string blobName = Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
                var blobRef = this.container.GetBlockBlobReference(blobName);

                log.Warn("Storing message id {0} in overflow storage as blob id {1}   (size {2:N1}kB)", context.MessageId, blobName, context.Payload.Length / 1024.0);

                await blobRef.UploadFromStreamAsync(context.Payload)
                    .ConfigureAwait(false);

                context.Payload.Dispose();
                context.Payload = null;

                context.Properties[OverflowConstants.OverflowKey] = true.ToString();
                context.Properties[OverflowConstants.OverflowBlobnameKey] = blobName;
                context.Properties[OverflowConstants.OverflowContainernameKey] = this.container.Name;
            }
        }
    }
}
