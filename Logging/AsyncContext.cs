using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Remoting.Messaging;

// For reference: http://www.wintellect.com/blogs/jeffreyr/logical-call-context-flowing-data-across-threads-appdomains-and-processes

namespace Qlue.Logging
{
    public static class AsyncContext
    {
        private static readonly Dictionary<Guid, AsyncData> sharedAsyncData = new Dictionary<Guid, AsyncData>();
        private static readonly object sharedAsyncLock = new object();

        private static readonly string NameSharedData = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        public static void StoreKeyValue(string key, string value)
        {
            AsyncData asyncData = AsyncData;

            asyncData.AddKeyValue(key, value);
        }

        public static string GetKeyValue(string key)
        {
            AsyncData asyncData = AsyncData;

            return asyncData.GetKeyValue(key);
        }

        public static IDictionary<string, string> AllKeyValues
        {
            get
            {
                var dict = new Dictionary<string, string>();

                AsyncData asyncData = AsyncData;
                foreach (var kvp in asyncData.GetAllKeyValues())
                    dict[kvp.Key] = kvp.Value;

                return dict;
            }
        }

        internal static IDisposable Push(string loggerName, string context)
        {
            AsyncData asyncData = AsyncData;
            asyncData.PushStack(loggerName, context);
            return new PopWhenDisposed(loggerName);
        }

        internal static Guid DataSetId
        {
            get
            {
                Guid? dataSetId = CallContext.LogicalGetData(NameSharedData) as Guid?;
                if (!dataSetId.HasValue)
                {
                    dataSetId = Guid.NewGuid();
                    CallContext.LogicalSetData(NameSharedData, dataSetId);
                }

                return dataSetId.Value;
            }
        }

        internal static void ClearAsyncData()
        {
            CallContext.FreeNamedDataSlot(NameSharedData);
        }

        internal static AsyncData AsyncData
        {
            get
            {
                Guid dataSetId = DataSetId;

                AsyncData asyncData;
                lock (sharedAsyncLock)
                {
                    if (!sharedAsyncData.TryGetValue(dataSetId, out asyncData))
                    {
                        asyncData = new AsyncData();
                        sharedAsyncData[dataSetId] = asyncData;
                    }
                }

                return asyncData;
            }
        }

        private static void Pop(string loggerName)
        {
            AsyncData asyncData = AsyncData;

            asyncData.PopStack(loggerName);
        }

        public static string GetStackTrace(ILog logger)
        {
            AsyncData asyncData = AsyncData;

            return string.Join("/", asyncData.GetStack(logger.Name));
        }

        private sealed class PopWhenDisposed : IDisposable
        {
            private bool disposed;
            private string loggerName;

            public PopWhenDisposed(string loggerName)
            {
                this.loggerName = loggerName;
            }

            public void Dispose()
            {
                if (this.disposed)
                    return;

                Pop(this.loggerName);

                this.disposed = true;
            }
        }
    }

    internal class AsyncData
    {
        private readonly object lockObject = new object();

        //private DateTime lastAccess;
        private readonly Dictionary<string, Stack<string>> stackPerLogger;

        private readonly Dictionary<string, string> keyValue;

        public AsyncData()
        {
            //this.lastAccess = DateTime.Now;
            this.stackPerLogger = new Dictionary<string, Stack<string>>();
            this.keyValue = new Dictionary<string, string>();
        }

        // TODO: Clean up AsyncData
        //public TimeSpan SinceLastAccess
        //{
        //    get
        //    {
        //        return DateTime.Now - this.lastAccess;
        //    }
        //}

        public void AddKeyValue(string key, string value)
        {
            lock (this.lockObject)
            {
                this.keyValue[key] = value;
            }
        }

        public string GetKeyValue(string key)
        {
            lock (this.lockObject)
            {
                string value;
                if (this.keyValue.TryGetValue(key, out value))
                    return value;

                return null;
            }
        }

        public IDictionary<string, string> GetAllKeyValues()
        {
            lock (this.lockObject)
            {
                var dict = new Dictionary<string, string>();

                foreach (var kvp in this.keyValue)
                    dict[kvp.Key] = kvp.Value;

                return dict;
            }
        }

        public void PushStack(string loggerName, string value)
        {
            lock (this.lockObject)
            {
                Stack<string> stack;
                if (!this.stackPerLogger.TryGetValue(loggerName, out stack))
                {
                    stack = new Stack<string>();
                    this.stackPerLogger[loggerName] = stack;
                }

                stack.Push(value);
            }
        }

        public void PopStack(string loggerName)
        {
            lock (this.lockObject)
            {
                Stack<string> stack;
                if (this.stackPerLogger.TryGetValue(loggerName, out stack))
                {
                    stack.Pop();

                    if (stack.Count == 0)
                        // Remove from dictionary
                        this.stackPerLogger.Remove(loggerName);
                }
            }
        }

        public string[] GetStack(string loggerName)
        {
            lock (this.lockObject)
            {
                Stack<string> stack;
                if (this.stackPerLogger.TryGetValue(loggerName, out stack))
                {
                    return stack.Reverse().ToArray();
                }
                else
                    return new string[0];
            }
        }
    }
}
