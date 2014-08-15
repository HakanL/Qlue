using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Qlue.DataReporting
{
    public interface IEventReporting
    {
        Task SendEventAsync(string eventType, IDictionary<string, string> data);

        Task SendEventAsync(string eventType, object data);
    }
}
