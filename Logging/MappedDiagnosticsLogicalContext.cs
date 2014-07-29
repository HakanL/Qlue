using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;

namespace NLog
{
    public static class MappedDiagnosticsLogicalContext
    {
        private const string LogicalContextDictKey = "NLog.MappedDiagnosticsLogicalContext";

        private static IDictionary<string, string> LogicalContextDict
        {
            get
            {
                var dict = CallContext.LogicalGetData(LogicalContextDictKey) as ConcurrentDictionary<string, string>;
                if (dict == null)
                {
                    dict = new ConcurrentDictionary<string, string>();
                    CallContext.LogicalSetData(LogicalContextDictKey, dict);
                }
                return dict;
            }
        }

        public static void Set(string item, string value)
        {
            LogicalContextDict[item] = value;
        }

        public static string Get(string item)
        {
            string s;

            if (!LogicalContextDict.TryGetValue(item, out s))
            {
                s = string.Empty;
            }

            return s;
        }

        public static Dictionary<string, string> All
        {
            get
            {
                return new Dictionary<string, string>(LogicalContextDict);
            }
        }
    }
}
