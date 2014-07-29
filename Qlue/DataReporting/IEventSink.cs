using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Qlue.DataReporting
{
    public interface IEventSink
    {
        Task SendEventAsync(string eventType, IEnumerable<KeyValuePair<string, string>> data);
    }
}
