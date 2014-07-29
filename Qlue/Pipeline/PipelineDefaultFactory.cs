using System;
using Qlue.Logging;
using Qlue.Pipeline.Component;
using Qlue.Transport;

namespace Qlue.Pipeline
{
    public class PipelineDefaultFactory
    {
        private ILog log;
        private IMessageSerializer serializer;

        public PipelineDefaultFactory(ILog log, IMessageSerializer serializer)
        {
            this.log = log;
            this.serializer = serializer;
        }

        public PipelineExecutor CreateOutboundPipeline(
            string destinationTopic,
            IBlobContainer sendingBlobContainer,
            int connections,
            string sessionId,
            IBusTransport busTransport)
        {
            var pipeline = new PipelineExecutor(
                new Serialize(this.log, this.serializer),
                new Compress(this.log, System.IO.Compression.CompressionLevel.Fastest),
                new OverflowPut(this.log, sendingBlobContainer),
                new MessageSender(this.log, destinationTopic, connections, sessionId, busTransport)
                );

            return pipeline;
        }

        public PipelineExecutor CreateInboundPipeline(BlobRepository blobRepository)
        {
            var pipeline = new PipelineExecutor(
                new OverflowGet(this.log, blobRepository),
                new Decompress(this.log),
                new Deserialize(this.log, this.serializer)
                );

            return pipeline;
        }
    }
}
