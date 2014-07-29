using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Qlue.DataReporting
{
    public class DataReportingManager : IEventReporting
    {
        private IEventSink eventSink;
        private IEnumerable<KeyValuePair<string, string>> additionalData;

        public DataReportingManager(IEventSink eventSink, object additionalData = null)
        {
            this.eventSink = eventSink;

            if (additionalData != null)
                this.additionalData = GetKvpFromObject(additionalData);
            else
                this.additionalData = new List<KeyValuePair<string, string>>();
        }

        private static IEnumerable<KeyValuePair<string, string>> GetKvpFromObject(object data)
        {
            var list = new List<KeyValuePair<string, string>>();

            foreach (var property in data.GetType().GetProperties())
            {
                object value = property.GetValue(data);

                list.Add(new KeyValuePair<string, string>(property.Name, value.ToString()));
            }

            return list;
        }

        public async Task SendEventAsync(string eventType, IDictionary<string, string> data)
        {
            var list = new List<KeyValuePair<string, string>>(this.additionalData);

            list.AddRange(data);

            try
            {
                await this.eventSink.SendEventAsync(eventType, list);
            }
            catch (Exception)
            {
                // Ignore any errors
            }
        }

        public async Task SendEventAsync(string eventType, object data)
        {
            var list = new List<KeyValuePair<string, string>>(this.additionalData);

            list.AddRange(GetKvpFromObject(data));

            try
            {
                await this.eventSink.SendEventAsync(eventType, list);
            }
            catch (Exception)
            {
                // Ignore any errors
            }
        }
    }
}
