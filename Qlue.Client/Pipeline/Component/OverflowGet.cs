using System;
using System.IO;
using System.Threading.Tasks;
using Qlue.Logging;

namespace Qlue.Pipeline.Component
{
    public class OverflowGet : IPipelineComponent
    {
        private ILog log;

        private BlobRepository blobRepository;

        public OverflowGet(ILog log, BlobRepository blobRepository)
        {
            this.log = log;
            this.blobRepository = blobRepository;
        }

        public async Task Execute(PipelineContext context)
        {
            string isOverflow;
            if (context.Properties.TryGetValue(OverflowConstants.OverflowKey, out isOverflow) && bool.Parse(isOverflow))
            {
                string overflowBlobname = context.Properties[OverflowConstants.OverflowBlobnameKey];
                string overflowContainer = context.Properties[OverflowConstants.OverflowContainernameKey];

                // Retrieve from overflow storage

                var blobContainer = this.blobRepository.GetBlobContainer(overflowContainer);
                var blobRef = blobContainer.GetBlockBlobReference(overflowBlobname);

                var bodyStream = new MemoryStream();

                log.Trace("Retrieve message id {0} from overflow storage as blob id {1}", context.MessageId, overflowBlobname);

                await blobRef.DownloadToStreamAsync(bodyStream)
                    .ConfigureAwait(false);

                log.Warn("Retrieved message id {0}, size {1:N1}kB", context.MessageId, bodyStream.Length / 1024.0);

                bodyStream.Seek(0, SeekOrigin.Begin);

                context.Payload = bodyStream;

                await blobRef.DeleteAsync();
            }
        }
    }
}
