using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Qlue.DataReporting
{
    public class NewRelicInsights : IEventSink
    {
        private string apiKey;
        private Uri eventUri;

        public NewRelicInsights(string accountId, string apiKey)
        {
            this.apiKey = apiKey;
            this.eventUri = new Uri(string.Format(CultureInfo.InvariantCulture, "https://insights.newrelic.com/beta_api/accounts/{0}/events", accountId));
        }

        private async Task<bool> SendAsync(string jsonData)
        {
            var httpWebRequest = HttpWebRequest.Create(this.eventUri);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";
            httpWebRequest.Headers.Add("X-Insert-Key", this.apiKey);

            using (var streamWriter = new StreamWriter(await httpWebRequest.GetRequestStreamAsync().ConfigureAwait(false)))
            {
                streamWriter.Write(jsonData);
                streamWriter.Flush();
                streamWriter.Close();

                var httpResponse = await httpWebRequest.GetResponseAsync().ConfigureAwait(false);
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();

                    int indexSuccess = result.IndexOf("\"success\":", StringComparison.CurrentCulture);
                    if (indexSuccess > -1)
                    {
                        string successValue = result.Substring(indexSuccess + 10);

                        if (successValue.StartsWith("true", StringComparison.Ordinal))
                            return true;
                    }

                    return false;
                }
            }
        }

        public async Task SendEventAsync(string eventType, IEnumerable<KeyValuePair<string, string>> data)
        {
            var jsonData = new StringBuilder("{\"eventType\":\"" + eventType + "\"");

            foreach (var kvp in data)
            {
                double dummy;
                if (double.TryParse(kvp.Value, out dummy))
                    jsonData.AppendFormat(",\"{0}\":{1}", kvp.Key, kvp.Value);
                else
                    jsonData.AppendFormat(",\"{0}\":\"{1}\"", kvp.Key, kvp.Value);
            }
            jsonData.Append('}');

            bool result = await this.SendAsync(jsonData.ToString()).ConfigureAwait(false);

            if (!result)
                throw new DataReportingException("Failed to process event data");
        }
    }
}
