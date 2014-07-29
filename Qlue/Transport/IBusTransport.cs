using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Qlue.Transport;
using Qlue.Logging;

namespace Qlue
{
    public interface IBusTransport
    {
        Pipeline.PipelineContext EndReceive(IAsyncResult result);

        bool IsClosed { get; }

        void Complete(Pipeline.PipelineContext context);

        void Abandon(Pipeline.PipelineContext context);

        void Close();

        IAsyncResult BeginReceive(AsyncCallback callback, object state);

        IBusSender CreateBusSender(string destinationTopic, string sessionId);

        string TopicSuffix { get; }
    }
}
