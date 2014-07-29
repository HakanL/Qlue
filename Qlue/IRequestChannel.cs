using System;
using System.Threading.Tasks;
using Qlue.Logging;

namespace Qlue
{
    public interface IRequestChannel : IDisposable
    {
        TResponse SendWaitResponse<TResponse>(object request, TimeSpan timeout, ILog logForDispatch = null);

        Task<TResponse> SendWaitResponseAsync<TResponse>(object request, TimeSpan timeout, ILog logForDispatch = null);

        string SendOneWay(object request, ILog logForDispatch = null);

        Task<string> SendOneWayAsync(object request, ILog logForDispatch = null);

        Task<string> SendNotifyAsync(object notifyObject, string topicName = null, ILog logForDispatch = null);

        string SendNotify(object notifyObject, string topicName = null, ILog logForDispatch = null);
    }
}
