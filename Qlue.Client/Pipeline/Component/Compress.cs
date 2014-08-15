using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Qlue.Logging;

namespace Qlue.Pipeline.Component
{
    public class Compress : IPipelineComponent
    {
        private ILog log;

        private CompressionLevel compressionLevel;

        public Compress(ILog log, CompressionLevel compressionLevel)
        {
            this.log = log;
            this.compressionLevel = compressionLevel;
        }

        public async Task Execute(PipelineContext context)
        {
            if (context.Payload.Length > 4096)
            {
                var target = new MemoryStream();
                using (DeflateStream deflateStream = new DeflateStream(target, compressionLevel, true))
                {
                    await context.Payload.CopyToAsync(deflateStream);
                }
                target.Seek(0, SeekOrigin.Begin);

                log.Debug("Compress message id {0}, originally {1:N1}kB, now {2:N1}kB", context.MessageId, context.Payload.Length / 1024.0, target.Length / 1024.0);

                context.Payload.Dispose();
                context.Payload = target;
                context.Properties.Add(CompressionConstants.CompressKey, CompressionConstants.CompressDeflate);
            }
        }
    }
}
