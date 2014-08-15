using System;
using System.Threading.Tasks;
using Qlue.Logging;

namespace Qlue
{
    public class InvokeContext
    {
        private Func<object, string, ILog, Task<string>> notifyFunction;

        private ILog log;

        public string MessageId { get; private set; }

        public string CustomSessionId { get; private set; }

        public InvokeContext(string messageId, string customSessionId, Func<object, string, ILog, Task<string>> notifyFunction, ILog log)
        {
            this.MessageId = messageId;
            this.CustomSessionId = customSessionId;
            this.notifyFunction = notifyFunction;
            this.log = log;
        }

        public void SendNotify(object notifyObject, string topicName = null)
        {
            this.notifyFunction(notifyObject, topicName, this.log);
        }

        public Task<string> SendNotifyAsync(object notifyObject, string topicName = null)
        {
            return this.notifyFunction(notifyObject, topicName, this.log);
        }
    }
}
