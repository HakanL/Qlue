using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Qlue.Pipeline;

namespace Qlue.Transport
{
    public interface IBusSender : IDisposable
    {
        /// <summary>
        /// Send the message
        /// </summary>
        /// <param name="context">Pipeline context</param>
        /// <returns>True if successfully send, False if it failed, but can be retried (exception if it permanentely failed)</returns>
        Task<bool> SendAsync(PipelineContext context);
    }
}
