using System;
using System.Threading.Tasks;
using Qlue.Logging;

namespace Qlue
{
    public interface INotifyChannel : IDisposable
    {
        void StartReceiving();

        void RegisterAsyncDispatch<TRequest>(Func<TRequest, InvokeContext, Task> executeAction, ILog logForDispatch = null);

        void RegisterDispatch<TRequest>(Action<TRequest, InvokeContext> executeAction, ILog logForDispatch = null);
    }
}
