using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Qlue.Logging;

namespace Qlue.Pipeline.Component
{
    public class Decompress : IPipelineComponent
    {
        private ILog log;

        public Decompress(ILog log)
        {
            this.log = log;
        }

        public async Task Execute(PipelineContext context)
        {
            string compressValue;
            if (context.Properties.TryGetValue(CompressionConstants.CompressKey, out compressValue))
            {
                if (compressValue != CompressionConstants.CompressDeflate)
                    throw new ArgumentOutOfRangeException("Unknown compression type");

                var target = new MemoryStream();
                using (DeflateStream deflateStream = new DeflateStream(context.Payload, CompressionMode.Decompress, true))
                {
                    await deflateStream.CopyToAsync(target);
                }
                target.Seek(0, SeekOrigin.Begin);

                log.Debug("Decompress message id {0}, originally {1:N1}kB, now {2:N1}kB", context.MessageId, context.Payload.Length / 1024.0, target.Length / 1024.0);

                context.Payload.Dispose();
                context.Payload = target;
            }
        }
    }
}
