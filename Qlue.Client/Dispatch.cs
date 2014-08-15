using System;
using System.Threading.Tasks;
using Qlue.Logging;

namespace Qlue
{
    public abstract class Dispatch
    {
        public abstract Task ExecuteAction(object request, InvokeContext invokeContext, Func<object, Task> sendResponse);

        public ILog Log { get; set; }
    }

    public class Dispatch<TRequest, TResponse> : Dispatch
    {
        private Func<TRequest, InvokeContext, Task<TResponse>> executeAction;

        public Dispatch(Func<TRequest, InvokeContext, Task<TResponse>> executeAction, ILog log)
        {
            this.executeAction = executeAction;
            this.Log = log;
        }

        public async override Task ExecuteAction(object request, InvokeContext invokeContext, Func<object, Task> sendResponse)
        {
            var response = await this.executeAction((TRequest)request, invokeContext);

            await sendResponse(response);
        }
    }

    public class Dispatch<TRequest> : Dispatch
    {
        private Func<TRequest, InvokeContext, Task> executeAction;

        public Dispatch(Func<TRequest, InvokeContext, Task> executeAction, ILog log)
        {
            this.executeAction = executeAction;
            this.Log = log;
        }

        public async override Task ExecuteAction(object request, InvokeContext invokeContext, Func<object, Task> sendResponse)
        {
            await executeAction((TRequest)request, invokeContext);
        }
    }
}
